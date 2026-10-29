using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Security;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public class SyncStatusChangedEventArgs : EventArgs
    {
        public string StatusMessage { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public DateTime LastSyncTime { get; set; }
    }

    /// <summary>
    /// خدمة المزامنة الخلفية الفعالة (SyncService)
    /// تعمل عبر PeriodicTimer (كل 5 إلى 10 ثوانٍ) وتستعلم عبر changes.list
    /// تدعم العمل دون اتصال (Offline-first)، رفع المقترحات المحلية، وإعادة المحاولة بـ Exponential Backoff.
    /// </summary>
    public class SyncService : IDisposable
    {
        private static SyncService? _instance;
        public static SyncService Instance => _instance ??= new SyncService();

        private readonly Func<AppDbContext> _dbFactory;
        private readonly ICollabStore? _collabStore;
        private readonly AuthService? _authService;

        private PeriodicTimer? _timer;
        private CancellationTokenSource? _cts;
        private Task? _syncTask;
        private bool _isRunning;
        private int _consecutiveErrors;
        private readonly TimeSpan _defaultInterval = TimeSpan.FromSeconds(7);
        private readonly TimeSpan _maxBackoff = TimeSpan.FromSeconds(60);

        public bool IsRunning => _isRunning;
        public bool IsOnline { get; private set; } = true;
        public DateTime LastSyncTime { get; private set; } = DateTime.MinValue;
        public string CurrentStatus { get; private set; } = "جاهز";

        public event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;

        public SyncService()
        {
            _dbFactory = () => new AppDbContext();
            _collabStore = null;
            _authService = null;
        }

        public SyncService(ICollabStore collabStore, Func<AppDbContext> dbFactory, AuthService? authService = null)
        {
            _collabStore = collabStore ?? throw new ArgumentNullException(nameof(collabStore));
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _authService = authService;
        }

        private ICollabStore? GetCollabStore() => _collabStore ?? AuthService.Instance.CollabStore;
        private AuthService GetAuthService() => _authService ?? AuthService.Instance;

        public void Start(TimeSpan? interval = null)
        {
            if (_isRunning) return;

            _cts = new CancellationTokenSource();
            _timer = new PeriodicTimer(interval ?? _defaultInterval);
            _isRunning = true;
            _consecutiveErrors = 0;

            _syncTask = Task.Run(async () =>
            {
                // مزامنة مبدئية فور التشغيل
                await ExecuteSyncCycleAsync(_cts.Token);

                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (await _timer.WaitForNextTickAsync(_cts.Token))
                        {
                            await ExecuteSyncCycleAsync(_cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        HandleNetworkFailure(ex);
                    }
                }
            }, _cts.Token);

            NotifyStatus("تم بدء خدمة المزامنة الخلفية.");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts?.Cancel();
            _timer?.Dispose();
            _timer = null;

            NotifyStatus("تم إيقاف المزامنة.");
        }

        /// <summary>
        /// دورة المزامنة الكاملة: رفع المعلقات المحلية ثم جلب التعديلات السحابية
        /// </summary>
        public async Task ExecuteSyncCycleAsync(CancellationToken cancellationToken = default)
        {
            var store = GetCollabStore();
            if (store == null)
            {
                NotifyStatus("بانتظار تسجيل الدخول إلى Google Drive...");
                return;
            }

            try
            {
                NotifyStatus("جارٍ المزامنة...");

                // 1. PUSH: رفع المقترحات والأصوات المحلية التي أنشئت دون اتصال
                await PushPendingLocalContributionsAsync(store, cancellationToken);
                await PushPendingLocalVotesAsync(store, cancellationToken);

                // 2. PULL: استعلام تزايدي خفيف عبر Changes.List
                await PullIncrementalChangesAsync(store, cancellationToken);

                // 3. Espace d'équipe (ADR 23) : intégration/publication sur le poste officiel, veille sur les autres postes
                await ProcessTeamWorkspaceAsync(cancellationToken);

                // إعادة تعيين مؤشرات النجاح
                _consecutiveErrors = 0;
                IsOnline = true;
                LastSyncTime = DateTime.UtcNow;
                NotifyStatus("متزامن ومحدّث");
            }
            catch (Exception ex)
            {
                HandleNetworkFailure(ex);
            }
        }

        #region Team workspace (version officielle)

        private static readonly TimeSpan OfficialVersionCheckInterval = TimeSpan.FromMinutes(5);
        private DateTime _lastOfficialCheckUtc = DateTime.MinValue;
        private bool _officialSnapshotChanged;

        /// <summary>Version officielle plus récente que celle du poste (poste membre), ou null.</summary>
        public OfficialSnapshotInfo? NewOfficialVersion { get; private set; }

        /// <summary>Déclenché quand une nouvelle version officielle est disponible pour ce poste.</summary>
        public event EventHandler<OfficialSnapshotInfo>? OfficialVersionAvailable;

        private static readonly TimeSpan SafetyPullInterval = TimeSpan.FromMinutes(5);
        private DateTime _lastSafetyPullUtc = DateTime.MinValue;

        /// <summary>
        /// Filet de sécurité : le flux « changes » de Drive peut omettre des fichiers déposés par d'autres comptes
        /// dans un dossier partagé ; on relit donc périodiquement les propositions modifiées depuis la dernière lecture.
        /// </summary>
        private async Task SafetyPullAsync(ICollabStore store, CancellationToken cancellationToken)
        {
            if (DateTime.UtcNow - _lastSafetyPullUtc < SafetyPullInterval) return;
            var since = _lastSafetyPullUtc == DateTime.MinValue ? DateTime.UtcNow.AddDays(-30) : _lastSafetyPullUtc.AddMinutes(-2);
            var startedAt = DateTime.UtcNow;

            var proposals = await store.DownloadProposalsModifiedSinceAsync(since, cancellationToken);
            if (proposals.Count > 0)
            {
                using var db = _dbFactory();
                foreach (var dto in proposals) await MergeRemoteProposalToLocalDbAsync(db, dto, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            _lastSafetyPullUtc = startedAt;
        }

        private async Task ProcessTeamWorkspaceAsync(CancellationToken cancellationToken)
        {
            // Les tests injectent leur propre stockage : pas de poste officiel ni de version publiée
            if (_collabStore != null) return;

            var auth = GetAuthService();
            var team = TeamWorkspaceService.Instance;
            if (auth.CollabStore != null) await SafetyPullAsync(auth.CollabStore, cancellationToken);

            if (auth.IsOfficialStation)
            {
                var results = await MergeService.Instance.ApplyAcceptedProposalsAsync(cancellationToken);
                if (results.Any(r => r.OfficialDataChanged)) team.PublishRequested = true;
                if (await team.AutoPublishIfNeededAsync(cancellationToken))
                    NotifyStatus("Version officielle publiée.");
                return;
            }

            if (!_officialSnapshotChanged && DateTime.UtcNow - _lastOfficialCheckUtc < OfficialVersionCheckInterval) return;
            _officialSnapshotChanged = false;
            _lastOfficialCheckUtc = DateTime.UtcNow;

            var newer = await team.GetNewerOfficialVersionAsync(cancellationToken);
            if (newer != null && (NewOfficialVersion == null || newer.PublishedAtUtc > NewOfficialVersion.PublishedAtUtc))
            {
                NewOfficialVersion = newer;
                OfficialVersionAvailable?.Invoke(this, newer);
            }
            else if (newer == null)
            {
                NewOfficialVersion = null;
            }
        }

        #endregion

        #region Push Local Offline Changes

        private async Task PushPendingLocalContributionsAsync(ICollabStore store, CancellationToken cancellationToken)
        {
            using var db = _dbFactory();
            // البحث عن المقترحات المحلية التي لم ترفع بعد (DriveFileId فارغ)
            var pendingList = await db.Contributions
                .Where(c => string.IsNullOrEmpty(c.DriveFileId) && c.Status == ContributionStatuses.Proposed)
                .ToListAsync(cancellationToken);

            foreach (var contrib in pendingList)
            {
                try
                {
                    string fileId = await store.UploadProposalAsync(ProposalMapper.ToDto(contrib), cancellationToken);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        contrib.DriveFileId = fileId;
                        db.Contributions.Update(contrib);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(SyncService), $"Envoi de la proposition locale {contrib.Id} impossible.", ex);
                }
            }
        }

        private async Task PushPendingLocalVotesAsync(ICollabStore store, CancellationToken cancellationToken)
        {
            using var db = _dbFactory();
            var pendingVotes = await db.Votes
                .Where(v => string.IsNullOrEmpty(v.DriveFileId))
                .ToListAsync(cancellationToken);

            foreach (var vote in pendingVotes)
            {
                try
                {
                    var dto = new VoteDto
                    {
                        Id = vote.Id,
                        ProposalId = vote.ContributionId,
                        VoterEmail = vote.VoterEmail,
                        VoterName = vote.VoterName,
                        Approve = vote.Approve,
                        Comment = vote.Comment,
                        At = vote.At
                    };

                    string fileId = await store.UploadVoteAsync(dto, cancellationToken);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        vote.DriveFileId = fileId;
                        db.Votes.Update(vote);
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(SyncService), $"Envoi du vote local {vote.Id} impossible.", ex);
                }
            }
        }

        #endregion

        #region Pull Incremental Changes

        public string? PageToken { get; set; }

        private async Task PullIncrementalChangesAsync(ICollabStore store, CancellationToken cancellationToken)
        {
            string pageToken = PageToken ?? SettingsService.GetSetting("Collab_ChangesPageToken", "");

            // إذا لم يكن هناك PageToken بعد، نقوم بتهيئة التوكن وعمل جلب أولي شامل
            if (string.IsNullOrEmpty(pageToken))
            {
                pageToken = await store.GetStartPageTokenAsync(cancellationToken) ?? "";
                PageToken = pageToken;
                if (!string.IsNullOrEmpty(pageToken))
                {
                    SettingsService.SaveSetting("Collab_ChangesPageToken", pageToken);
                }

                // جلب مبدئي لكافة المقترحات والأصوات السحابية
                await InitialFullPullAsync(store, cancellationToken);
                return;
            }

            // الاستعلام التزايدي الخفيف
            var changes = await store.GetChangesAsync(pageToken, cancellationToken);

            if (!string.IsNullOrEmpty(changes.NewPageToken))
            {
                PageToken = changes.NewPageToken;
                SettingsService.SaveSetting("Collab_ChangesPageToken", changes.NewPageToken);
            }

            // دمج المقترحات الواردة في قاعدة البيانات المحلية
            if (changes.ModifiedProposals.Any())
            {
                using var db = _dbFactory();
                foreach (var dto in changes.ModifiedProposals)
                {
                    await MergeRemoteProposalToLocalDbAsync(db, dto, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }

            // دمج الأصوات الواردة في قاعدة البيانات المحلية
            if (changes.ModifiedVotes.Any())
            {
                using var db = _dbFactory();
                foreach (var vDto in changes.ModifiedVotes)
                {
                    await MergeRemoteVoteToLocalDbAsync(db, vDto, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }

            // تحديث تكوين الفريق إن تغير team.json
            if (changes.HasTeamConfigUpdate)
            {
                var newTeamConfig = await store.GetTeamConfigAsync(cancellationToken);
                if (newTeamConfig != null)
                {
                    var auth = GetAuthService();
                    if (auth.CurrentTeamConfig != null)
                    {
                        auth.CurrentTeamConfig.Members = newTeamConfig.Members;
                        auth.CurrentTeamConfig.QuorumRule = newTeamConfig.QuorumRule;
                        auth.CurrentTeamConfig.QuorumThreshold = newTeamConfig.QuorumThreshold;
                        if (auth.CurrentUser != null) auth.CurrentUser.Role = auth.CurrentTeamConfig.RoleOf(auth.CurrentUser.Email);
                    }
                    WeakReferenceMessenger.Default.Send(new TeamConfigUpdatedMessage(newTeamConfig.TeamId));
                }
            }

            if (changes.HasOfficialSnapshotUpdate) _officialSnapshotChanged = true;
        }

        private async Task InitialFullPullAsync(ICollabStore store, CancellationToken cancellationToken)
        {
            try
            {
                var cloudProposals = await store.DownloadProposalsAsync(cancellationToken);
                var cloudVotes = await store.DownloadAllVotesAsync(cancellationToken);

                using var db = _dbFactory();

                foreach (var pDto in cloudProposals)
                {
                    await MergeRemoteProposalToLocalDbAsync(db, pDto, cancellationToken);
                }

                foreach (var vDto in cloudVotes)
                {
                    await MergeRemoteVoteToLocalDbAsync(db, vDto, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(SyncService), "Récupération initiale des propositions impossible.", ex);
            }
        }

        /// <summary>
        /// Intègre une proposition lue sur Drive dans la base locale, en n'acceptant que des informations attestées :
        /// - l'auteur déclaré doit être le propriétaire du fichier (créateur) ;
        /// - une décision (approuvée, rejetée…) n'est retenue que si le fichier a été modifié en dernier par un administrateur ;
        /// - une décision locale définitive (intégrée / rejetée) n'est jamais remplacée par un état distant plus ancien.
        /// </summary>
        private async Task MergeRemoteProposalToLocalDbAsync(AppDbContext db, ProposalDto dto, CancellationToken cancellationToken)
        {
            bool driveAttested = dto.DriveOwnerEmail != null || dto.DriveLastModifiedBy != null;
            if (driveAttested && !string.Equals(dto.DriveOwnerEmail, dto.AuthorEmail, StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Warn(nameof(SyncService), $"Proposition {dto.Id} ignorée : auteur déclaré {dto.AuthorEmail}, fichier créé par {dto.DriveOwnerEmail ?? "inconnu"}.");
                return;
            }

            bool decisionTrusted = !driveAttested || IsDecider(dto.DriveLastModifiedBy);
            string remoteStatus = dto.Status == ContributionStatuses.Proposed || decisionTrusted ? dto.Status : ContributionStatuses.Proposed;
            if (remoteStatus != dto.Status)
                AppLogger.Warn(nameof(SyncService), $"Décision « {dto.Status} » sur {dto.Id} ignorée : fichier modifié par {dto.DriveLastModifiedBy}, qui n'est pas administrateur.");

            var local = await db.Contributions.FirstOrDefaultAsync(c => c.Id == dto.Id, cancellationToken);
            if (local == null)
            {
                local = ProposalMapper.ToEntity(dto);
                local.Status = remoteStatus;
                if (!decisionTrusted)
                {
                    local.ReviewedByEmail = null;
                    local.ReviewNotes = null;
                }
                db.Contributions.Add(local);
                WeakReferenceMessenger.Default.Send(new ProposalCreatedMessage(local));
                return;
            }

            bool localIsFinal = local.Status is ContributionStatuses.Approved or ContributionStatuses.Rejected;
            if (localIsFinal || remoteStatus == local.Status && !decisionTrusted) return;
            if (!decisionTrusted && remoteStatus == ContributionStatuses.Proposed && local.Status != ContributionStatuses.Proposed) return;

            var effective = ProposalMapper.ToEntity(dto);
            effective.Status = remoteStatus;
            if (ProposalMapper.ApplyReview(local, effective))
            {
                db.Contributions.Update(local);
                WeakReferenceMessenger.Default.Send(new ProposalUpdatedMessage(local));
            }
        }

        private bool IsDecider(string? email)
        {
            var auth = GetAuthService();
            return Security.SecurityConstants.IsSuperAdmin(email) || (auth.CurrentTeamConfig?.CanDecide(email) ?? false);
        }

        private async Task MergeRemoteVoteToLocalDbAsync(AppDbContext db, VoteDto vDto, CancellationToken cancellationToken)
        {
            var localVote = await db.Votes.FirstOrDefaultAsync(v => v.ContributionId == vDto.ProposalId && v.VoterEmail == vDto.VoterEmail, cancellationToken);
            if (localVote == null)
            {
                localVote = new Vote
                {
                    Id = vDto.Id == Guid.Empty ? Guid.NewGuid() : vDto.Id,
                    ContributionId = vDto.ProposalId,
                    VoterEmail = vDto.VoterEmail,
                    VoterName = vDto.VoterName,
                    Approve = vDto.Approve,
                    Comment = vDto.Comment,
                    At = vDto.At,
                    DriveFileId = vDto.DriveFileId
                };
                db.Votes.Add(localVote);
                WeakReferenceMessenger.Default.Send(new VoteSubmittedMessage(localVote));
            }
            else
            {
                if (localVote.Approve != vDto.Approve || localVote.Comment != vDto.Comment)
                {
                    localVote.Approve = vDto.Approve;
                    localVote.Comment = vDto.Comment;
                    localVote.At = vDto.At;
                    db.Votes.Update(localVote);
                    WeakReferenceMessenger.Default.Send(new VoteSubmittedMessage(localVote));
                }
            }
        }

        #endregion

        #region Offline & Exponential Backoff

        private void HandleNetworkFailure(Exception ex)
        {
            _consecutiveErrors++;
            IsOnline = false;

            // حساب التراجع الأسي (Exponential Backoff: 5s, 10s, 20s, 40s, 60s)
            int delaySeconds = Math.Min(5 * (1 << (_consecutiveErrors - 1)), (int)_maxBackoff.TotalSeconds);
            NotifyStatus($"دون اتصال ({ex.Message}) — إعادة المحاولة خلال {delaySeconds} ثانية.");

            // تعديل مؤقت PeriodicTimer ليعكس التراجع الأسي
            try
            {
                _timer?.Dispose();
                _timer = new PeriodicTimer(TimeSpan.FromSeconds(delaySeconds));
            }
            catch { }
        }

        private void NotifyStatus(string message)
        {
            CurrentStatus = message;
            SyncStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
            {
                StatusMessage = message,
                IsOnline = IsOnline,
                LastSyncTime = LastSyncTime
            });
        }

        public void Dispose()
        {
            Stop();
        }

        #endregion
    }
}

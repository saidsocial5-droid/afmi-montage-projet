using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Security;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>
    /// خدمة إدارة المساهمات والمقترحات النصية (Contribution Service)
    /// تحول التعديلات إلى مقترحات للأعضاء، تتيح التعديل المباشر للمدير،
    /// وتتولى تخزين المقترحات محلياً ورفعها سحابياً بنمط Append-Only.
    /// </summary>
    public class ContributionService
    {
        private static ContributionService? _instance;
        public static ContributionService Instance => _instance ??= new ContributionService();

        private readonly Func<AppDbContext> _dbFactory;
        private readonly ICollabStore? _collabStore;
        private readonly AuthService? _authService;

        public ContributionService()
        {
            _dbFactory = () => new AppDbContext();
            _collabStore = null; // سيتم جلبه ديناميكياً من AuthService.Instance
            _authService = null;
        }

        public ContributionService(Func<AppDbContext> dbFactory, ICollabStore? collabStore, AuthService? authService = null)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _collabStore = collabStore;
            _authService = authService;
        }

        private ICollabStore? GetCollabStore() => _collabStore ?? AuthService.Instance.CollabStore;
        private AuthService GetAuthService() => _authService ?? AuthService.Instance;

        /// <summary>
        /// إنشاء مقترح مساهمة جديد وحفظه محلياً ورفعه سحابياً
        /// </summary>
        public async Task<Contribution> CreateProposalAsync(
            string entityType,
            string entityId,
            string fieldName,
            string? baseValue,
            string? proposedValue,
            int baseVersion,
            CancellationToken cancellationToken = default,
            string? entityLabel = null,
            string? projectGlobalId = null)
        {
            var auth = GetAuthService();
            string authorEmail = auth.CurrentUser?.Email ?? "offline_user@local";
            string authorName = auth.CurrentUser?.DisplayName ?? "مستخدم محلي";

            // 1. التحقق من صلاحيات الدور (Reviewer et Guest ne peuvent pas proposer)
            if (auth.CurrentTeamConfig != null && !auth.CurrentTeamConfig.CanPropose(authorEmail))
            {
                throw new UnauthorizedAccessException($"Le compte {authorEmail} n'a pas le rôle de contributeur dans l'équipe : il ne peut pas envoyer de propositions.");
            }

            var contribution = new Contribution
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                FieldName = fieldName,
                BaseValue = baseValue,
                ProposedValue = proposedValue,
                BaseVersion = baseVersion,
                AuthorEmail = authorEmail,
                AuthorName = authorName,
                Status = ContributionStatuses.Proposed,
                CreatedAt = DateTime.UtcNow,
                EntityLabel = entityLabel,
                ProjectGlobalId = projectGlobalId
            };

            // 2. الحفظ في قاعدة البيانات المحلية SQLite
            using (var db = _dbFactory())
            {
                db.Contributions.Add(contribution);
                await db.SaveChangesAsync(cancellationToken);
            }

            // 3. الرفع إلى مساحة العمل السحابية (Append-Only Proposal)
            await UploadAsync(contribution, cancellationToken);

            // 4. التوثيق والإشعار
            AuditLogger.Log("مقترح مساهمة جديد", $"تم اقتراح تعديل على {entityType}.{fieldName} بواسطة {authorEmail}", authorEmail);
            WeakReferenceMessenger.Default.Send(new ProposalCreatedMessage(contribution));

            return contribution;
        }

        /// <summary>Suggestion libre (texte) d'un membre sur un projet : aucune donnée n'est modifiée à l'approbation.</summary>
        public Task<Contribution> CreateNoteAsync(string projectGlobalId, string projectTitle, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("La suggestion est vide.", nameof(text));
            return CreateProposalAsync("Project", projectGlobalId, ProposalFields.Note, null, text.Trim(), 0, cancellationToken,
                entityLabel: $"Projet : {projectTitle}", projectGlobalId: projectGlobalId);
        }

        /// <summary>
        /// Retire les modifications déjà envoyées (même élément, même champ, même valeur proposée, proposition non rejetée).
        /// </summary>
        public async Task<List<DetectedChange>> FilterUnsentAsync(IEnumerable<DetectedChange> changes, CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            var sent = await db.Contributions
                .Where(c => c.Status != ContributionStatuses.Rejected)
                .Select(c => new { c.EntityType, c.EntityId, c.FieldName, c.ProposedValue })
                .ToListAsync(cancellationToken);
            var keys = new HashSet<string>(sent.Select(s => Key(s.EntityType, s.EntityId, s.FieldName, s.ProposedValue)));
            return changes.Where(c => !keys.Contains(Key(c.EntityType, c.EntityId, c.FieldName, c.NewValue))).ToList();
        }

        private static string Key(string type, string id, string field, string? value) => $"{type}\u001f{id}\u001f{field}\u001f{value}";

        /// <summary>Transforme les modifications locales sélectionnées en propositions (déjà envoyées : ignorées).</summary>
        public async Task<int> SubmitDetectedChangesAsync(IEnumerable<DetectedChange> changes, CancellationToken cancellationToken = default)
        {
            var toSend = await FilterUnsentAsync(changes, cancellationToken);

            // Les créations d'abord, parents avant enfants (ordre de détection), pour faciliter l'intégration
            int count = 0;
            foreach (var change in toSend.OrderByDescending(c => c.IsCreation))
            {
                await CreateProposalAsync(change.EntityType, change.EntityId, change.FieldName, change.BaseValue, change.NewValue,
                    change.BaseVersion, cancellationToken, change.EntityLabel, change.ProjectGlobalId);
                count++;
            }
            return count;
        }

        private async Task UploadAsync(Contribution contribution, CancellationToken cancellationToken)
        {
            var store = GetCollabStore();
            if (store == null) return; // sera envoyée par SyncService à la prochaine connexion

            try
            {
                string fileId = await store.UploadProposalAsync(ProposalMapper.ToDto(contribution), cancellationToken);
                if (!string.IsNullOrEmpty(fileId))
                {
                    contribution.DriveFileId = fileId;
                    using var db = _dbFactory();
                    var tracked = await db.Contributions.FindAsync(new object[] { contribution.Id }, cancellationToken);
                    if (tracked != null)
                    {
                        tracked.DriveFileId = fileId;
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(ContributionService), $"Proposition {contribution.Id} enregistrée localement, envoi différé.", ex);
            }
        }

        /// <summary>
        /// تسجيل تصويت جديد على مقترح مساهمة
        /// </summary>
        public async Task<Vote> SubmitVoteAsync(Guid contributionId, bool approve, string? comment = null, CancellationToken cancellationToken = default)
        {
            var auth = GetAuthService();
            string voterEmail = auth.CurrentUser?.Email ?? "offline_user@local";
            string voterName = auth.CurrentUser?.DisplayName ?? "مستخدم محلي";

            // التحقق من صلاحية التصويت في team.json
            if (auth.CurrentTeamConfig != null && !auth.CurrentTeamConfig.CanVote(voterEmail))
            {
                throw new UnauthorizedAccessException($"المستخدم {voterEmail} غير مسجل كعضو نشط في فريق العمل team.json ولا يحق له التصويت.");
            }

            Vote? vote;
            using (var db = _dbFactory())
            {
                vote = await db.Votes.FirstOrDefaultAsync(v => v.ContributionId == contributionId && v.VoterEmail == voterEmail, cancellationToken);
                if (vote == null)
                {
                    vote = new Vote
                    {
                        Id = Guid.NewGuid(),
                        ContributionId = contributionId,
                        VoterEmail = voterEmail,
                        VoterName = voterName,
                        Approve = approve,
                        Comment = comment,
                        At = DateTime.UtcNow
                    };
                    db.Votes.Add(vote);
                }
                else
                {
                    // تحديث صوت موجود لنفس المستخدم
                    vote.Approve = approve;
                    vote.Comment = comment;
                    vote.At = DateTime.UtcNow;
                    db.Votes.Update(vote);
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            // الرفع سحابياً كملف صوت مستقل
            var store = GetCollabStore();
            if (store != null)
            {
                try
                {
                    var voteDto = new VoteDto
                    {
                        Id = vote.Id,
                        ProposalId = vote.ContributionId,
                        VoterEmail = vote.VoterEmail,
                        VoterName = vote.VoterName,
                        Approve = vote.Approve,
                        Comment = vote.Comment,
                        At = vote.At
                    };

                    string fileId = await store.UploadVoteAsync(voteDto, cancellationToken);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        vote.DriveFileId = fileId;
                        using var db = _dbFactory();
                        var tracked = await db.Votes.FindAsync(new object[] { vote.Id }, cancellationToken);
                        if (tracked != null)
                        {
                            tracked.DriveFileId = fileId;
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(ContributionService), $"Vote {vote.Id} enregistré localement, envoi différé.", ex);
                }
            }

            AuditLogger.Log("تصويت على مقترح", $"قام {voterEmail} بالتصويت بـ {(approve ? "موافق" : "رفض")} على المقترح {contributionId}", voterEmail);
            WeakReferenceMessenger.Default.Send(new VoteSubmittedMessage(vote));

            return vote;
        }

        /// <summary>
        /// استرجاع كافة المقترحات المعلقة التي تحتاج لمراجعة أو تصويت
        /// </summary>
        public async Task<List<Contribution>> GetPendingProposalsAsync(CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            return await db.Contributions
                .Include(c => c.Votes)
                .Where(c => c.Status == ContributionStatuses.Proposed || c.Status == ContributionStatuses.NeedsReview)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        /// <summary>Propositions envoyées par un membre (toutes, les plus récentes d'abord).</summary>
        public async Task<List<Contribution>> GetProposalsByAuthorAsync(string authorEmail, int take = 200, CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            return await db.Contributions
                .Where(c => c.AuthorEmail == authorEmail)
                .OrderByDescending(c => c.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// فحص ما إذا كان هناك مقترح معلق لحقل معين داخل كيان (لتغذية شارات الواجهة Badges)
        /// </summary>
        public async Task<bool> HasPendingProposalForFieldAsync(string entityType, string entityId, string fieldName, CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            return await db.Contributions.AnyAsync(c =>
                c.EntityType == entityType &&
                c.EntityId == entityId &&
                c.FieldName == fieldName &&
                (c.Status == ContributionStatuses.Proposed || c.Status == ContributionStatuses.NeedsReview),
                cancellationToken);
        }

        /// <summary>
        /// استرجاع عدد المقترحات المعلقة لكيان كامل (لتغذية شارة الكيان الرئيسية)
        /// </summary>
        public async Task<int> GetPendingProposalCountForEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
        {
            using var db = _dbFactory();
            return await db.Contributions.CountAsync(c =>
                c.EntityType == entityType &&
                c.EntityId == entityId &&
                (c.Status == ContributionStatuses.Proposed || c.Status == ContributionStatuses.NeedsReview),
                cancellationToken);
        }
    }
}

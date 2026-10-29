using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Security;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>
    /// تطبيق Google Drive لواجهة ICollabStore.
    /// يلتزم بنمط Append-Only التام حيث يكتب كل عضو ملفاته المستقلة،
    /// مما يمنع حدوث أي تعارض كتابة على السحابة (Write Conflicts).
    /// L'espace d'équipe est le dossier « AssociationPM_Workspace » appartenant au compte officiel
    /// (SecurityConstants.WorkspaceOwnerEmail) : il est retrouvé par son propriétaire, qu'il soit dans
    /// « Mon Drive » (poste officiel) ou dans « Partagés avec moi » (membres). Seul le compte officiel le crée (ADR 23).
    /// </summary>
    public class GoogleDriveCollabStore : ICollabStore
    {
        public const string WorkspaceFolderName = "AssociationPM_Workspace";
        private const string FolderMime = "application/vnd.google-apps.folder";
        private const string OfficialSnapshotFileName = "afmi_official.db";

        private readonly DriveService _driveService;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _currentUserEmail;
        private readonly string _ownerEmail;

        // معرفات المجلدات المحفوظة في الذاكرة لتفادي الاستعلام المتكرر
        private string? _workspaceRootFolderId;
        private string? _systemFolderId;
        private string? _collabFolderId;
        private string? _syncFolderId;
        private string? _proposalsFolderId;
        private string? _votesFolderId;
        private string? _snapshotsFolderId;
        private string? _officialFolderId;

        public GoogleDriveCollabStore(DriveService driveService, string currentUserEmail, string? knownWorkspaceFolderId = null, string? ownerEmail = null)
        {
            _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
            _currentUserEmail = currentUserEmail ?? string.Empty;
            _ownerEmail = ownerEmail ?? SecurityConstants.WorkspaceOwnerEmail;
            _workspaceRootFolderId = string.IsNullOrWhiteSpace(knownWorkspaceFolderId) ? null : knownWorkspaceFolderId;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }

        /// <summary>Identifiant du dossier racine de l'espace d'équipe (après <see cref="EnsureWorkspaceStructureAsync"/>).</summary>
        public string? WorkspaceFolderId => _workspaceRootFolderId;

        #region Folder Structure Management

        public async Task<bool> EnsureWorkspaceStructureAsync(CancellationToken cancellationToken = default)
        {
            // 1. المجلد الرئيسي لمساحة العمل (propriété du compte officiel)
            _workspaceRootFolderId = await ResolveWorkspaceRootAsync(cancellationToken);

            // 2. مجلد النظام الداخلي
            _systemFolderId = await GetOrCreateFolderAsync("_system", _workspaceRootFolderId, cancellationToken);

            // 3. مجلد المزامنة (الخاص بالهاتف واللقطات)
            _syncFolderId = await GetOrCreateFolderAsync("sync", _systemFolderId, cancellationToken);

            // 4. مجلد التعاون الجماعي
            _collabFolderId = await GetOrCreateFolderAsync("collab", _systemFolderId, cancellationToken);

            // 5. المجلدات الفرعية للمقترحات والأصوات واللقطات
            _proposalsFolderId = await GetOrCreateFolderAsync("proposals", _collabFolderId, cancellationToken);
            _votesFolderId = await GetOrCreateFolderAsync("votes", _collabFolderId, cancellationToken);
            _snapshotsFolderId = await GetOrCreateFolderAsync("snapshots", _collabFolderId, cancellationToken);

            return true;
        }

        private async Task<string> ResolveWorkspaceRootAsync(CancellationToken cancellationToken)
        {
            // Identifiant mémorisé : vérifier qu'il est toujours accessible et appartient bien au compte officiel
            if (!string.IsNullOrEmpty(_workspaceRootFolderId))
            {
                try
                {
                    var get = _driveService.Files.Get(_workspaceRootFolderId);
                    get.Fields = "id, trashed, owners(emailAddress)";
                    var folder = await get.ExecuteAsync(cancellationToken);
                    if (folder.Trashed != true && IsOwnedByOfficialAccount(folder)) return folder.Id;
                }
                catch (Google.GoogleApiException ex)
                {
                    AppLogger.Warn(nameof(GoogleDriveCollabStore), "Dossier d'équipe mémorisé inaccessible, nouvelle recherche.", ex);
                }
            }

            // Recherche par nom et propriétaire : couvre « Mon Drive » et « Partagés avec moi »
            var listReq = _driveService.Files.List();
            listReq.Q = $"name = '{Escape(WorkspaceFolderName)}' and mimeType = '{FolderMime}' and '{Escape(_ownerEmail)}' in owners and trashed = false";
            listReq.Spaces = "drive";
            listReq.Fields = "files(id, name, createdTime)";
            listReq.OrderBy = "createdTime";
            var result = await listReq.ExecuteAsync(cancellationToken);
            var existing = result.Files?.FirstOrDefault();
            if (existing != null) return existing.Id;

            if (!string.Equals(_currentUserEmail, _ownerEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkspaceAccessException(
                    $"L'espace d'équipe de {_ownerEmail} n'est pas encore partagé avec {_currentUserEmail}. " +
                    "Demandez à un administrateur de vous inviter depuis « Espace d'équipe › Équipe », puis acceptez l'invitation reçue par e-mail.");
            }

            // Compte officiel : création initiale dans « Mon Drive »
            var created = await CreateFolderAsync(WorkspaceFolderName, "root", cancellationToken);
            AppLogger.Info(nameof(GoogleDriveCollabStore), $"Espace d'équipe créé dans le Drive de {_ownerEmail}.");
            return created;
        }

        private bool IsOwnedByOfficialAccount(Google.Apis.Drive.v3.Data.File file) =>
            file.Owners?.Any(o => string.Equals(o.EmailAddress, _ownerEmail, StringComparison.OrdinalIgnoreCase)) == true;

        private async Task<string?> FindFolderAsync(string folderName, string parentId, CancellationToken cancellationToken)
        {
            var listReq = _driveService.Files.List();
            listReq.Q = $"name = '{Escape(folderName)}' and mimeType = '{FolderMime}' and '{Escape(parentId)}' in parents and trashed = false";
            listReq.Spaces = "drive";
            listReq.Fields = "files(id, name)";
            listReq.OrderBy = "createdTime";
            var result = await listReq.ExecuteAsync(cancellationToken);
            return result.Files?.FirstOrDefault()?.Id;
        }

        private async Task<string> GetOrCreateFolderAsync(string folderName, string parentId, CancellationToken cancellationToken = default)
        {
            return await FindFolderAsync(folderName, parentId, cancellationToken)
                   ?? await CreateFolderAsync(folderName, parentId, cancellationToken);
        }

        private async Task<string> CreateFolderAsync(string folderName, string parentId, CancellationToken cancellationToken)
        {
            var newFolder = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = FolderMime,
                Parents = new List<string> { parentId }
            };

            var createReq = _driveService.Files.Create(newFolder);
            createReq.Fields = "id";
            var created = await createReq.ExecuteAsync(cancellationToken);
            return created.Id;
        }

        #endregion

        #region Team Config & Members

        public async Task<TeamConfig?> GetTeamConfigAsync(CancellationToken cancellationToken = default)
        {
            await EnsureCollabFolderAsync(cancellationToken);

            var file = await FindFileInFolderAsync("team.json", _collabFolderId!, cancellationToken, "files(id, name, lastModifyingUser(emailAddress))");
            if (file == null) return null;

            // Tous les membres peuvent écrire dans le dossier : team.json n'est fiable que s'il a été écrit par un administrateur principal
            string? modifiedBy = file.LastModifyingUser?.EmailAddress;
            if (!SecurityConstants.IsSuperAdmin(modifiedBy))
            {
                AppLogger.Warn(nameof(GoogleDriveCollabStore), $"team.json ignoré : dernière modification par {modifiedBy ?? "inconnu"}, qui n'est pas administrateur.");
                return null;
            }

            string json = await DownloadFileContentAsync(file.Id, cancellationToken);
            if (string.IsNullOrWhiteSpace(json)) return null;

            return JsonSerializer.Deserialize<TeamConfig>(json, _jsonOptions);
        }

        public async Task SaveTeamConfigAsync(TeamConfig config, CancellationToken cancellationToken = default)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            await EnsureCollabFolderAsync(cancellationToken);

            config.LastUpdatedAt = DateTime.UtcNow;
            string json = JsonSerializer.Serialize(config, _jsonOptions);

            await UploadOrUpdateFileAsync("team.json", _collabFolderId!, json, cancellationToken);
        }

        public async Task<bool> InviteMemberAsync(string email, string role, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureWorkspaceStructureAsync(cancellationToken);

                // مشاركة مجلد مساحة العمل على Google Drive بصلاحية محرر (Editor/Writer)
                var permission = new Permission
                {
                    Type = "user",
                    Role = "writer",
                    EmailAddress = email
                };

                var permReq = _driveService.Permissions.Create(permission, _workspaceRootFolderId);
                permReq.SendNotificationEmail = true;
                permReq.EmailMessage = "Vous êtes invité(e) à contribuer aux projets de l'Association Forêt Modèle d'Ifrane. " +
                                       "Ouvrez AFMI Montage Projet › Espace d'équipe et connectez-vous avec ce compte Google.";
                await permReq.ExecuteAsync(cancellationToken);

                // تحديث ملف team.json لإضافة العضو
                var teamConfig = await GetTeamConfigAsync(cancellationToken) ?? new TeamConfig { QuorumRule = QuorumRules.AdminDecision };
                var existing = teamConfig.FindMember(email);
                if (existing != null)
                {
                    existing.Role = role;
                }
                else
                {
                    teamConfig.Members.Add(new TeamMemberDto
                    {
                        Email = email,
                        Name = email.Split('@')[0],
                        Role = role,
                        AddedAt = DateTime.UtcNow
                    });
                }

                teamConfig.UpdatedByEmail = _currentUserEmail;
                await SaveTeamConfigAsync(teamConfig, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(GoogleDriveCollabStore), $"Invitation de {email} impossible.", ex);
                return false;
            }
        }

        public async Task<bool> RemoveMemberAsync(string email, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureWorkspaceStructureAsync(cancellationToken);

                var listReq = _driveService.Permissions.List(_workspaceRootFolderId);
                listReq.Fields = "permissions(id, emailAddress, role)";
                var perms = await listReq.ExecuteAsync(cancellationToken);
                foreach (var perm in perms.Permissions?.Where(p => string.Equals(p.EmailAddress, email, StringComparison.OrdinalIgnoreCase) && p.Role != "owner")
                                     ?? Enumerable.Empty<Permission>())
                {
                    await _driveService.Permissions.Delete(_workspaceRootFolderId, perm.Id).ExecuteAsync(cancellationToken);
                }

                var teamConfig = await GetTeamConfigAsync(cancellationToken);
                if (teamConfig != null)
                {
                    teamConfig.Members.RemoveAll(m => string.Equals(m.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
                    teamConfig.UpdatedByEmail = _currentUserEmail;
                    await SaveTeamConfigAsync(teamConfig, cancellationToken);
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(GoogleDriveCollabStore), $"Retrait de {email} impossible.", ex);
                return false;
            }
        }

        #endregion

        #region Proposals (Append-Only)

        public async Task<string> UploadProposalAsync(ProposalDto proposal, CancellationToken cancellationToken = default)
        {
            if (proposal == null) throw new ArgumentNullException(nameof(proposal));
            await EnsureProposalsFolderAsync(cancellationToken);

            string fileName = $"{proposal.Id}.json";
            string json = JsonSerializer.Serialize(proposal, _jsonOptions);

            // نرفع الملف دائماً كملف جديد أو نحدثه إن كان ملك لنفس الكاتب
            string fileId = await UploadOrUpdateFileAsync(fileName, _proposalsFolderId!, json, cancellationToken);
            proposal.DriveFileId = fileId;
            return fileId;
        }

        public Task<List<ProposalDto>> DownloadProposalsAsync(CancellationToken cancellationToken = default) =>
            DownloadProposalsCoreAsync(null, cancellationToken);

        public Task<List<ProposalDto>> DownloadProposalsModifiedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
            DownloadProposalsCoreAsync(sinceUtc, cancellationToken);

        private async Task<List<ProposalDto>> DownloadProposalsCoreAsync(DateTime? sinceUtc, CancellationToken cancellationToken)
        {
            await EnsureProposalsFolderAsync(cancellationToken);

            string query = $"'{_proposalsFolderId}' in parents and mimeType = 'application/json' and trashed = false";
            if (sinceUtc.HasValue)
                query += $" and modifiedTime > '{sinceUtc.Value.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'}'";

            var list = new List<ProposalDto>();
            string? pageToken = null;
            do
            {
                var listReq = _driveService.Files.List();
                listReq.Q = query;
                listReq.Spaces = "drive";
                listReq.PageSize = 200;
                listReq.PageToken = pageToken;
                listReq.Fields = "nextPageToken, files(id, name, owners(emailAddress), lastModifyingUser(emailAddress))";

                var result = await listReq.ExecuteAsync(cancellationToken);
                foreach (var file in result.Files ?? new List<Google.Apis.Drive.v3.Data.File>())
                {
                    var dto = await ReadProposalFileAsync(file, cancellationToken);
                    if (dto != null) list.Add(dto);
                }
                pageToken = result.NextPageToken;
            } while (!string.IsNullOrEmpty(pageToken));

            return list;
        }

        private async Task<ProposalDto?> ReadProposalFileAsync(Google.Apis.Drive.v3.Data.File file, CancellationToken cancellationToken)
        {
            try
            {
                string json = await DownloadFileContentAsync(file.Id, cancellationToken);
                var dto = JsonSerializer.Deserialize<ProposalDto>(json, _jsonOptions);
                if (dto == null) return null;

                dto.DriveFileId = file.Id;
                // Identités attestées par Drive : servent à vérifier l'auteur et l'origine des décisions
                dto.DriveOwnerEmail = file.Owners?.FirstOrDefault()?.EmailAddress;
                dto.DriveLastModifiedBy = file.LastModifyingUser?.EmailAddress;
                return dto;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(GoogleDriveCollabStore), $"Proposition illisible : {file.Name}", ex);
                return null;
            }
        }

        public async Task<ProposalDto?> DownloadProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            await EnsureProposalsFolderAsync(cancellationToken);
            var file = await FindFileInFolderAsync($"{proposalId}.json", _proposalsFolderId!, cancellationToken,
                "files(id, name, owners(emailAddress), lastModifyingUser(emailAddress))");
            return file == null ? null : await ReadProposalFileAsync(file, cancellationToken);
        }

        #endregion

        #region Votes (Append-Only)

        public async Task<string> UploadVoteAsync(VoteDto vote, CancellationToken cancellationToken = default)
        {
            if (vote == null) throw new ArgumentNullException(nameof(vote));
            await EnsureVotesFolderAsync(cancellationToken);

            string userHash = ComputeEmailHash(vote.VoterEmail);
            string fileName = $"{vote.ProposalId}__{userHash}.json";
            string json = JsonSerializer.Serialize(vote, _jsonOptions);

            string fileId = await UploadOrUpdateFileAsync(fileName, _votesFolderId!, json, cancellationToken);
            vote.DriveFileId = fileId;
            return fileId;
        }

        public async Task<List<VoteDto>> DownloadVotesForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default)
        {
            await EnsureVotesFolderAsync(cancellationToken);
            return await DownloadVotesAsync($"'{_votesFolderId}' in parents and name contains '{proposalId}__' and mimeType = 'application/json' and trashed = false", cancellationToken);
        }

        public async Task<List<VoteDto>> DownloadAllVotesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureVotesFolderAsync(cancellationToken);
            return await DownloadVotesAsync($"'{_votesFolderId}' in parents and mimeType = 'application/json' and trashed = false", cancellationToken);
        }

        private async Task<List<VoteDto>> DownloadVotesAsync(string query, CancellationToken cancellationToken)
        {
            var listReq = _driveService.Files.List();
            listReq.Q = query;
            listReq.Spaces = "drive";
            listReq.PageSize = 500;
            listReq.Fields = "files(id, name)";

            var result = await listReq.ExecuteAsync(cancellationToken);
            var list = new List<VoteDto>();
            if (result.Files == null) return list;

            foreach (var file in result.Files)
            {
                try
                {
                    string json = await DownloadFileContentAsync(file.Id, cancellationToken);
                    var dto = JsonSerializer.Deserialize<VoteDto>(json, _jsonOptions);
                    if (dto != null)
                    {
                        dto.DriveFileId = file.Id;
                        list.Add(dto);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(GoogleDriveCollabStore), $"Vote illisible : {file.Name}", ex);
                }
            }

            return list;
        }

        #endregion

        #region Concurrency Locking (merge.lock)

        public async Task<LockResult> AcquireLockAsync(string lockName, string holderEmail, string holderName, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureCollabFolderAsync(cancellationToken);
                string fileName = string.IsNullOrWhiteSpace(lockName) ? "merge.lock" : $"{lockName}.lock";

                var existingFile = await FindFileInFolderAsync(fileName, _collabFolderId!, cancellationToken);

                if (existingFile != null)
                {
                    string currentContent = await DownloadFileContentAsync(existingFile.Id, cancellationToken);
                    LockFileDto? currentLock = null;
                    try { currentLock = JsonSerializer.Deserialize<LockFileDto>(currentContent, _jsonOptions); }
                    catch (JsonException ex) { AppLogger.Warn(nameof(GoogleDriveCollabStore), $"Fichier de verrou illisible ({fileName}), il sera remplacé.", ex); }

                    if (currentLock != null && !currentLock.IsExpired)
                    {
                        // القفل محجوز حالياً ولم ينتهِ وقته
                        if (string.Equals(currentLock.HolderEmail, holderEmail, StringComparison.OrdinalIgnoreCase))
                        {
                            // نفس المستخدم يعيد تجديد القفل
                            currentLock.ExpiresAt = DateTime.UtcNow.Add(timeout);
                            string renewJson = JsonSerializer.Serialize(currentLock, _jsonOptions);
                            await UploadOrUpdateFileAsync(fileName, _collabFolderId!, renewJson, cancellationToken);
                            return LockResult.Success(currentLock.LockId);
                        }

                        return LockResult.Failed(currentLock.HolderEmail, $"Lock currently held by {currentLock.HolderName} ({currentLock.HolderEmail}) until {currentLock.ExpiresAt:HH:mm:ss} UTC");
                    }
                }

                // كتابة ملف القفل الجديد
                var newLock = new LockFileDto
                {
                    HolderEmail = holderEmail,
                    HolderName = holderName,
                    AcquiredAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(timeout)
                };

                string lockJson = JsonSerializer.Serialize(newLock, _jsonOptions);
                await UploadOrUpdateFileAsync(fileName, _collabFolderId!, lockJson, cancellationToken);

                return LockResult.Success(newLock.LockId);
            }
            catch (Exception ex)
            {
                return LockResult.Failed("Error", $"Failed to acquire lock: {ex.Message}");
            }
        }

        public async Task ReleaseLockAsync(string lockName, string lockToken, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureCollabFolderAsync(cancellationToken);
                string fileName = string.IsNullOrWhiteSpace(lockName) ? "merge.lock" : $"{lockName}.lock";

                var existingFile = await FindFileInFolderAsync(fileName, _collabFolderId!, cancellationToken);
                if (existingFile != null)
                {
                    await _driveService.Files.Delete(existingFile.Id).ExecuteAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(GoogleDriveCollabStore), "Libération du verrou impossible.", ex);
            }
        }

        #endregion

        #region Snapshots & Mobile sync/projects.json

        public async Task UploadSnapshotAsync(int version, string jsonContent, CancellationToken cancellationToken = default)
        {
            await EnsureSnapshotsFolderAsync(cancellationToken);
            string fileName = $"v{version}.json";
            await UploadOrUpdateFileAsync(fileName, _snapshotsFolderId!, jsonContent, cancellationToken);
        }

        public async Task<string?> DownloadLatestSnapshotAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSnapshotsFolderAsync(cancellationToken);

            var listReq = _driveService.Files.List();
            listReq.Q = $"'{_snapshotsFolderId}' in parents and mimeType = 'application/json' and trashed = false";
            listReq.OrderBy = "name desc";
            listReq.Spaces = "drive";
            listReq.Fields = "files(id, name)";

            var result = await listReq.ExecuteAsync(cancellationToken);
            var latest = result.Files?.FirstOrDefault();
            if (latest == null) return null;

            return await DownloadFileContentAsync(latest.Id, cancellationToken);
        }

        public async Task UploadSyncProjectsJsonAsync(string projectsJson, CancellationToken cancellationToken = default)
        {
            await EnsureSyncFolderAsync(cancellationToken);
            await UploadOrUpdateFileAsync("projects.json", _syncFolderId!, projectsJson, cancellationToken);
        }

        #endregion

        #region Official snapshot (version officielle des projets)

        public async Task<OfficialSnapshotInfo> UploadOfficialSnapshotAsync(string dbFilePath, int projectCount, CancellationToken cancellationToken = default)
        {
            if (!SecurityConstants.IsSuperAdmin(_currentUserEmail))
                throw new UnauthorizedAccessException("Seul un administrateur peut publier la version officielle.");

            await EnsureWorkspaceStructureAsync(cancellationToken);
            _officialFolderId ??= await GetOrCreateFolderAsync("official", _systemFolderId!, cancellationToken);

            var appProperties = new Dictionary<string, string>
            {
                ["publishedBy"] = _currentUserEmail,
                ["projectCount"] = projectCount.ToString(CultureInfo.InvariantCulture),
                ["appVersion"] = typeof(GoogleDriveCollabStore).Assembly.GetName().Version?.ToString(3) ?? string.Empty
            };

            var existing = await FindFileInFolderAsync(OfficialSnapshotFileName, _officialFolderId, cancellationToken);
            await using var stream = System.IO.File.OpenRead(dbFilePath);
            const string mime = "application/octet-stream";

            Google.Apis.Upload.IUploadProgress progress;
            string fileId;
            if (existing != null)
            {
                var update = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { AppProperties = appProperties }, existing.Id, stream, mime);
                update.Fields = "id";
                progress = await update.UploadAsync(cancellationToken);
                fileId = existing.Id;
            }
            else
            {
                var create = _driveService.Files.Create(new Google.Apis.Drive.v3.Data.File
                {
                    Name = OfficialSnapshotFileName,
                    Parents = new List<string> { _officialFolderId },
                    AppProperties = appProperties
                }, stream, mime);
                create.Fields = "id";
                progress = await create.UploadAsync(cancellationToken);
                fileId = create.ResponseBody?.Id ?? string.Empty;
            }

            if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
                throw new IOException("Envoi de la version officielle interrompu : " + progress.Exception?.Message, progress.Exception);

            return await GetOfficialSnapshotInfoAsync(cancellationToken)
                   ?? new OfficialSnapshotInfo { FileId = fileId, PublishedAtUtc = DateTime.UtcNow, PublishedBy = _currentUserEmail, ProjectCount = projectCount };
        }

        public async Task<OfficialSnapshotInfo?> GetOfficialSnapshotInfoAsync(CancellationToken cancellationToken = default)
        {
            await EnsureWorkspaceStructureAsync(cancellationToken);
            _officialFolderId ??= await FindFolderAsync("official", _systemFolderId!, cancellationToken);
            if (_officialFolderId == null) return null;

            var file = await FindFileInFolderAsync(OfficialSnapshotFileName, _officialFolderId, cancellationToken,
                "files(id, modifiedTime, lastModifyingUser(emailAddress), appProperties)");
            if (file == null) return null;

            // Tous les membres peuvent écrire dans le dossier : n'accepter qu'une version déposée par un administrateur principal
            string? modifiedBy = file.LastModifyingUser?.EmailAddress;
            if (!SecurityConstants.IsSuperAdmin(modifiedBy))
            {
                AppLogger.Warn(nameof(GoogleDriveCollabStore), $"Version officielle ignorée : déposée par {modifiedBy ?? "inconnu"}, qui n'est pas administrateur.");
                return null;
            }

            string Prop(string key) => file.AppProperties != null && file.AppProperties.TryGetValue(key, out var v) ? v : string.Empty;
            return new OfficialSnapshotInfo
            {
                FileId = file.Id,
                PublishedAtUtc = file.ModifiedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow,
                PublishedBy = string.IsNullOrEmpty(Prop("publishedBy")) ? modifiedBy! : Prop("publishedBy"),
                ProjectCount = int.TryParse(Prop("projectCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0,
                AppVersion = Prop("appVersion")
            };
        }

        public async Task<OfficialSnapshotInfo?> DownloadOfficialSnapshotAsync(string destPath, CancellationToken cancellationToken = default)
        {
            var info = await GetOfficialSnapshotInfoAsync(cancellationToken);
            if (info == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destPath))!);
            string tempPath = destPath + ".download";
            await using (var fs = System.IO.File.Create(tempPath))
            {
                var progress = await _driveService.Files.Get(info.FileId).DownloadAsync(fs, cancellationToken);
                if (progress.Status != Google.Apis.Download.DownloadStatus.Completed)
                    throw new IOException("Téléchargement de la version officielle interrompu : " + progress.Exception?.Message, progress.Exception);
            }
            System.IO.File.Move(tempPath, destPath, overwrite: true);
            return info;
        }

        #endregion

        #region Changes & Polling

        public async Task<string?> GetStartPageTokenAsync(CancellationToken cancellationToken = default)
        {
            var req = _driveService.Changes.GetStartPageToken();
            var response = await req.ExecuteAsync(cancellationToken);
            return response.StartPageTokenValue;
        }

        public async Task<CollabChangesResult> GetChangesAsync(string pageToken, CancellationToken cancellationToken = default)
        {
            var result = new CollabChangesResult();
            if (string.IsNullOrEmpty(pageToken)) return result;

            var req = _driveService.Changes.List(pageToken);
            req.Fields = "nextPageToken, newStartPageToken, changes(fileId, removed, file(id, name, parents, mimeType, owners(emailAddress), lastModifyingUser(emailAddress)))";
            req.Spaces = "drive";

            var response = await req.ExecuteAsync(cancellationToken);
            result.NewPageToken = response.NewStartPageToken ?? response.NextPageToken;

            if (response.Changes == null || !response.Changes.Any())
                return result;

            if (_proposalsFolderId == null) await EnsureWorkspaceStructureAsync(cancellationToken);

            foreach (var change in response.Changes)
            {
                if (change.Removed == true || change.File == null) continue;

                var parents = change.File.Parents ?? new List<string>();

                // 1. فحص هل التغيير في مجلد المقترحات
                if (_proposalsFolderId != null && parents.Contains(_proposalsFolderId))
                {
                    var prop = await ReadProposalFileAsync(change.File, cancellationToken);
                    if (prop != null) result.ModifiedProposals.Add(prop);
                }
                // 2. فحص هل التغيير في مجلد الأصوات
                else if (_votesFolderId != null && parents.Contains(_votesFolderId))
                {
                    try
                    {
                        string json = await DownloadFileContentAsync(change.File.Id, cancellationToken);
                        var vote = JsonSerializer.Deserialize<VoteDto>(json, _jsonOptions);
                        if (vote != null)
                        {
                            vote.DriveFileId = change.File.Id;
                            result.ModifiedVotes.Add(vote);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Warn(nameof(GoogleDriveCollabStore), $"Vote illisible : {change.File.Name}", ex);
                    }
                }
                // 3. فحص هل الملف هو team.json
                else if (change.File.Name == "team.json")
                {
                    result.HasTeamConfigUpdate = true;
                }
                // 4. فحص هل الملف هو projects.json
                else if (change.File.Name == "projects.json")
                {
                    result.HasMobileJsonUpdate = true;
                }
                // 5. Nouvelle version officielle publiée
                else if (change.File.Name == OfficialSnapshotFileName)
                {
                    result.HasOfficialSnapshotUpdate = true;
                }
            }

            return result;
        }

        #endregion

        #region Helpers

        private async Task EnsureCollabFolderAsync(CancellationToken cancellationToken)
        {
            if (_collabFolderId == null) await EnsureWorkspaceStructureAsync(cancellationToken);
        }

        private async Task EnsureProposalsFolderAsync(CancellationToken cancellationToken)
        {
            if (_proposalsFolderId == null) await EnsureWorkspaceStructureAsync(cancellationToken);
        }

        private async Task EnsureVotesFolderAsync(CancellationToken cancellationToken)
        {
            if (_votesFolderId == null) await EnsureWorkspaceStructureAsync(cancellationToken);
        }

        private async Task EnsureSnapshotsFolderAsync(CancellationToken cancellationToken)
        {
            if (_snapshotsFolderId == null) await EnsureWorkspaceStructureAsync(cancellationToken);
        }

        private async Task EnsureSyncFolderAsync(CancellationToken cancellationToken)
        {
            if (_syncFolderId == null) await EnsureWorkspaceStructureAsync(cancellationToken);
        }

        /// <summary>Échappe une valeur littérale dans une requête Drive (apostrophes et barres obliques inverses).</summary>
        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

        private async Task<Google.Apis.Drive.v3.Data.File?> FindFileInFolderAsync(string fileName, string parentId, CancellationToken cancellationToken, string fields = "files(id, name)")
        {
            var req = _driveService.Files.List();
            req.Q = $"name = '{Escape(fileName)}' and '{Escape(parentId)}' in parents and trashed = false";
            req.Spaces = "drive";
            req.Fields = fields;
            var res = await req.ExecuteAsync(cancellationToken);
            return res.Files?.FirstOrDefault();
        }

        private async Task<string> DownloadFileContentAsync(string fileId, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            await _driveService.Files.Get(fileId).DownloadAsync(stream, cancellationToken);
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        private async Task<string> UploadOrUpdateFileAsync(string fileName, string parentId, string content, CancellationToken cancellationToken)
        {
            var existing = await FindFileInFolderAsync(fileName, parentId, cancellationToken);
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);

            if (existing != null)
            {
                var updateReq = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), existing.Id, stream, "application/json");
                updateReq.Fields = "id";
                await updateReq.UploadAsync(cancellationToken);
                return existing.Id;
            }
            else
            {
                var newFile = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName,
                    Parents = new List<string> { parentId },
                    MimeType = "application/json"
                };
                var createReq = _driveService.Files.Create(newFile, stream, "application/json");
                createReq.Fields = "id";
                await createReq.UploadAsync(cancellationToken);
                return createReq.ResponseBody?.Id ?? string.Empty;
            }
        }

        public static string ComputeEmailHash(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "unknown";
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
            return Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 16);
        }

        #endregion
    }
}

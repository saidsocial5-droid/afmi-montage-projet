using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models.Collab;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>Métadonnées de la version officielle publiée sur l'espace d'équipe.</summary>
    public class OfficialSnapshotInfo
    {
        public string FileId { get; set; } = string.Empty;
        public DateTime PublishedAtUtc { get; set; }
        public string PublishedBy { get; set; } = string.Empty;
        public int ProjectCount { get; set; }
        public string AppVersion { get; set; } = string.Empty;
    }

    /// <summary>L'espace d'équipe est introuvable ou inaccessible pour le compte connecté.</summary>
    public class WorkspaceAccessException : Exception
    {
        public WorkspaceAccessException(string message) : base(message) { }
    }

    /// <summary>
    /// واجهة تجريد طبقة التخزين التعاوني (Collab Store Abstraction).
    /// تمكن من استبدال Google Drive بـ Firestore أو أي مزود آخر بسلاسة دون تعديل منطق التطبيق.
    /// </summary>
    public interface ICollabStore
    {
        // 1. إدارة مساحة العمل والفريق
        Task<bool> EnsureWorkspaceStructureAsync(CancellationToken cancellationToken = default);
        Task<TeamConfig?> GetTeamConfigAsync(CancellationToken cancellationToken = default);
        Task SaveTeamConfigAsync(TeamConfig config, CancellationToken cancellationToken = default);
        Task<bool> InviteMemberAsync(string email, string role, CancellationToken cancellationToken = default);

        /// <summary>Retire un membre de team.json et révoque son accès au dossier Drive.</summary>
        Task<bool> RemoveMemberAsync(string email, CancellationToken cancellationToken = default);

        // 2. المقترحات (Append-Only Proposals: proposals/{guid}.json)
        Task<string> UploadProposalAsync(ProposalDto proposal, CancellationToken cancellationToken = default);
        Task<List<ProposalDto>> DownloadProposalsAsync(CancellationToken cancellationToken = default);

        /// <summary>Propositions modifiées depuis une date (filet de sécurité si le flux de changements Drive en omet).</summary>
        Task<List<ProposalDto>> DownloadProposalsModifiedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
            => DownloadProposalsAsync(cancellationToken);
        Task<ProposalDto?> DownloadProposalAsync(Guid proposalId, CancellationToken cancellationToken = default);

        // 3. الأصوات (Append-Only Votes: votes/{proposalId}__{userEmailHash}.json)
        Task<string> UploadVoteAsync(VoteDto vote, CancellationToken cancellationToken = default);
        Task<List<VoteDto>> DownloadVotesForProposalAsync(Guid proposalId, CancellationToken cancellationToken = default);
        Task<List<VoteDto>> DownloadAllVotesAsync(CancellationToken cancellationToken = default);

        // 4. قفل الدمج المتفائل (Merge Locking: merge.lock)
        Task<LockResult> AcquireLockAsync(string lockName, string holderEmail, string holderName, TimeSpan timeout, CancellationToken cancellationToken = default);
        Task ReleaseLockAsync(string lockName, string lockToken, CancellationToken cancellationToken = default);

        // 5. اللقطات وتوافق تطبيق الهاتف (Snapshots & Mobile: sync/projects.json)
        Task UploadSnapshotAsync(int version, string jsonContent, CancellationToken cancellationToken = default);
        Task<string?> DownloadLatestSnapshotAsync(CancellationToken cancellationToken = default);
        Task UploadSyncProjectsJsonAsync(string projectsJson, CancellationToken cancellationToken = default);

        // 6. المزامنة والاستعلام التزايدي
        Task<string?> GetStartPageTokenAsync(CancellationToken cancellationToken = default);
        Task<CollabChangesResult> GetChangesAsync(string pageToken, CancellationToken cancellationToken = default);

        // 7. Version officielle des projets (_system/official/afmi_official.db), ADR 23.
        //    Seule une version publiée par un administrateur principal est renvoyée (vérification Drive « lastModifyingUser »).
        Task<OfficialSnapshotInfo> UploadOfficialSnapshotAsync(string dbFilePath, int projectCount, CancellationToken cancellationToken = default);
        Task<OfficialSnapshotInfo?> GetOfficialSnapshotInfoAsync(CancellationToken cancellationToken = default);
        Task<OfficialSnapshotInfo?> DownloadOfficialSnapshotAsync(string destPath, CancellationToken cancellationToken = default);
    }
}

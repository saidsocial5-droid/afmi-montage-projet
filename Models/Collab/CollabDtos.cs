using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AFMI_Montage_projet_2.Models.Collab
{
    /// <summary>
    /// هوية المستخدم الموثوقة المستخلصة من Google OAuth ID Token
    /// </summary>
    public class UserProfile
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? PictureUrl { get; set; }
        public string Role { get; set; } = TeamRoles.Member;
        public bool IsAuthenticated { get; set; }
    }

    /// <summary>
    /// بنية ملف المقترح الفردي: collab/proposals/{guid}.json
    /// </summary>
    public class ProposalDto
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string? BaseValue { get; set; }
        public string? ProposedValue { get; set; }
        public string AuthorEmail { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int BaseVersion { get; set; } = 1;
        public string Status { get; set; } = ContributionStatuses.Proposed;
        public string? DriveFileId { get; set; }

        // Espace d'équipe (ADR 23) : décision de l'administrateur et affichage
        public string? ReviewedByEmail { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
        public string? EntityLabel { get; set; }
        public string? ProjectGlobalId { get; set; }

        /// <summary>Dernier compte ayant modifié le fichier, attesté par Google Drive (non sérialisé).</summary>
        [JsonIgnore]
        public string? DriveLastModifiedBy { get; set; }

        /// <summary>Propriétaire (créateur) du fichier, attesté par Google Drive (non sérialisé).</summary>
        [JsonIgnore]
        public string? DriveOwnerEmail { get; set; }

        // V4: Signed Collaboration & Device Pairing Verification
        public string? DeviceId { get; set; }
        public string? DeviceSignature { get; set; }
    }

    /// <summary>
    /// بنية ملف الصوت الفردي: collab/votes/{proposalId}__{userEmailHash}.json
    /// </summary>
    public class VoteDto
    {
        public Guid Id { get; set; }
        public Guid ProposalId { get; set; }
        public string VoterEmail { get; set; } = string.Empty;
        public string VoterName { get; set; } = string.Empty;
        public bool Approve { get; set; }
        public string? Comment { get; set; }
        public DateTime At { get; set; }
        public string? DriveFileId { get; set; }

        // V4: Signed Collaboration & Device Pairing Verification
        public string? DeviceId { get; set; }
        public string? DeviceSignature { get; set; }
    }

    /// <summary>
    /// بنية ملف القفل المتفائل: collab/merge.lock
    /// </summary>
    public class LockFileDto
    {
        public string LockId { get; set; } = Guid.NewGuid().ToString("N");
        public string HolderEmail { get; set; } = string.Empty;
        public string HolderName { get; set; } = string.Empty;
        public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(1);

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    /// <summary>
    /// نتيجة محاولة الاستحواذ على القفل
    /// </summary>
    public class LockResult
    {
        public bool Acquired { get; set; }
        public string? LockToken { get; set; }
        public string? CurrentHolderEmail { get; set; }
        public string? Message { get; set; }

        public static LockResult Success(string token) => new() { Acquired = true, LockToken = token, Message = "Lock acquired successfully." };
        public static LockResult Failed(string holder, string message) => new() { Acquired = false, CurrentHolderEmail = holder, Message = message };
    }

    /// <summary>
    /// نتيجة التغييرات الدورية المسترجعة من Changes.List
    /// </summary>
    public class CollabChangesResult
    {
        public string? NewPageToken { get; set; }
        public List<ProposalDto> ModifiedProposals { get; set; } = new();
        public List<VoteDto> ModifiedVotes { get; set; } = new();
        public bool HasTeamConfigUpdate { get; set; }
        public bool HasMobileJsonUpdate { get; set; }
        public bool HasOfficialSnapshotUpdate { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public abstract class BaseEntity
    {
        [MaxLength(36)]
        public string GlobalId { get; set; } = Guid.NewGuid().ToString();

        [MaxLength(100)]
        public string? DriveFileId { get; set; }

        public int SyncVersion { get; set; } = 1;

        /// <summary>
        /// رقم إصدار الكيان للتحكم المتفائل في التزامن (Optimistic Concurrency)
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// البريد الإلكتروني لآخر من قام بتعديل معتمد
        /// </summary>
        [MaxLength(100)]
        public string? UpdatedByEmail { get; set; }

        [MaxLength(20)]
        public string ContributionStatus { get; set; } = "Official";

        [MaxLength(100)]
        public string? AuthorEmail { get; set; }

        [MaxLength(100)]
        public string? AuthorName { get; set; }

        [MaxLength(7)]
        public string? AuthorColor { get; set; }

        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    }
}

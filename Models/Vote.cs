using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models
{
    /// <summary>
    /// يمثل تصويت عضو في الفريق على مساهمة مقترحة
    /// يُحفظ كملف فردي مستقل على Google Drive: collab/votes/{proposalId}__{userEmailHash}.json
    /// بنمط Append-only يضمن عدم حدوث أي تضارب كتابة.
    /// </summary>
    public class Vote
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// معرف المساهمة المقترحة
        /// </summary>
        [Required]
        public Guid ContributionId { get; set; }

        /// <summary>
        /// البريد الإلكتروني للمصوت (مصدر هوية موثوق عبر OAuth ID Token)
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string VoterEmail { get; set; } = string.Empty;

        /// <summary>
        /// الاسم الصريح للمصوت
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string VoterName { get; set; } = string.Empty;

        /// <summary>
        /// قرار التصويت: true = موافقة، false = رفض
        /// </summary>
        public bool Approve { get; set; }

        /// <summary>
        /// تعليق اختياري يوضح سبب القرار أو ملاحظة توجيهية
        /// </summary>
        [MaxLength(1000)]
        public string? Comment { get; set; }

        /// <summary>
        /// توقيت التصويت بالـ UTC
        /// </summary>
        public DateTime At { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// معرف الملف على Google Drive: votes/{proposalId}__{userEmailHash}.json
        /// </summary>
        [MaxLength(150)]
        public string? DriveFileId { get; set; }

        /// <summary>
        /// العلاقة مع الكيان المقترح
        /// </summary>
        [ForeignKey(nameof(ContributionId))]
        public Contribution? Contribution { get; set; }
    }
}

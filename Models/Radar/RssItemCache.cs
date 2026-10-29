using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models.Radar
{
    /// <summary>
    /// عناصر الخلاصات المخزنة مؤقتاً محلياً لضمان العمل دون اتصال بالإنترنت (Offline Mode).
    /// تُخزن في جدول rss_items_cache.
    /// </summary>
    [Table("rss_items_cache")]
    public class RssItemCache
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FeedId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string FeedName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Donor { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ExternalId { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        public string CleanDescription { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Link { get; set; } = string.Empty;

        public DateTime? PublishDate { get; set; }

        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public bool IsHidden { get; set; } = false;

        public bool IsInPipeline { get; set; } = false;

        public double? AiScore { get; set; }

        public string? AiScoreRationale { get; set; }

        public string? AiMatchedAxes { get; set; }

        public string? AiSummaryAr { get; set; }

        public string? AiSummaryFr { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AiEstimatedBudget { get; set; }

        [MaxLength(10)]
        public string? AiCurrency { get; set; }

        public DateTime? AiDeadline { get; set; }

        public bool IsScored { get; set; } = false;
    }
}

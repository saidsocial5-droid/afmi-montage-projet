using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models.Radar
{
    /// <summary>
    /// فرص التمويل والمنح المحفوظة في محفظة المشاريع (grants_pipeline).
    /// متوافقة مع قواعد بيانات SQLite و PostgreSQL.
    /// </summary>
    [Table("grants_pipeline")]
    public class GrantOpportunity : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string ExternalId { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Donor { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string OriginalUrl { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedBudget { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; }

        public DateTime? Deadline { get; set; }

        public string SummaryAr { get; set; } = string.Empty;

        public string SummaryFr { get; set; } = string.Empty;

        public double RelevanceScore { get; set; }

        [MaxLength(500)]
        public string MatchedAxes { get; set; } = string.Empty;

        public string Rationale { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string PipelineStatus { get; set; } = "Mise sous veille";

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public string Notes { get; set; } = string.Empty;

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }
    }
}

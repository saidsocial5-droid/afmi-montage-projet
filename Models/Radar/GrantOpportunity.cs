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
        public string PipelineStatus { get; set; } = VeilleStatus.Nouveau;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public string Notes { get; set; } = string.Empty;

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // ── Base de veille (onglet APPELS) ──

        /// <summary>Identifiant V-AAAA-NNN, attribué à l'entrée dans la base de veille.</summary>
        [MaxLength(20)]
        public string VeilleId { get; set; } = string.Empty;

        /// <summary>A (20-25), B (14-19) ou C (&lt; 14).</summary>
        [MaxLength(5)]
        public string Priority { get; set; } = string.Empty;

        // Notation /25 : cinq critères notés de 0 à 5
        public int ScorePertinence { get; set; }
        public int ScoreEligibilite { get; set; }
        public int ScoreFinancier { get; set; }
        public int ScoreFaisabilite { get; set; }
        public int ScoreStrategique { get; set; }

        /// <summary>Total après plafonnement (14 au plus tant que l'appel n'est pas confirmé).</summary>
        public int ScoreTotal { get; set; }

        /// <summary>Justification de chaque note, une ligne par critère.</summary>
        public string ScoreJustification { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Confirmation { get; set; } = VeilleConfirmation.NonConfirme;

        public DateTime? VerifiedAt { get; set; }

        /// <summary>Axes de veille A1 à A10 touchés (« A1, A2 »).</summary>
        [MaxLength(100)]
        public string VeilleAxes { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CallType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Duration { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Zone { get; set; } = string.Empty;

        public string Eligibility { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Cofinancing { get; set; } = string.Empty;

        [MaxLength(300)]
        public string OverheadCosts { get; set; } = string.Empty;

        [MaxLength(300)]
        public string DetectionSource { get; set; } = string.Empty;

        [MaxLength(500)]
        public string ConsortiumAdvice { get; set; } = string.Empty;
    }
}

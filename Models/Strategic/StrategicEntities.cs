using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models.Strategic
{
    /// <summary>
    /// كيان أساسي يدعم الحذف المنطقي (Soft Delete) وحقول التدقيق والتزامن
    /// </summary>
    public abstract class StrategicBaseEntity : BaseEntity
    {
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        [MaxLength(150)]
        public string? DeletedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [MaxLength(150)]
        public string? CreatedBy { get; set; }
    }

    /// <summary>
    /// الأساس الاستراتيجي للمشروع (Strategic Foundation) - نقطة البداية الهندسية
    /// </summary>
    public class StrategicFoundation : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        // 1. بطاقة المشروع والسياق
        [MaxLength(250)]
        public string IdeaTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TargetZone { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TargetBeneficiaries { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Sector { get; set; } = string.Empty;

        public int EstimatedDurationMonths { get; set; } = 12;

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedBudget { get; set; } = 0m;

        [MaxLength(10)]
        public string Currency { get; set; } = "EUR";

        [MaxLength(200)]
        public string PotentialDonor { get; set; } = string.Empty;

        public string DonorPriorities { get; set; } = string.Empty;

        public int CompletenessScore { get; set; } = 0;

        // 4 & 5. التحليل الاستراتيجي والمشكلة المركزية
        public string CoreProblemStatement { get; set; } = string.Empty;
        public string ProblemEvidenceData { get; set; } = string.Empty;
        public string SelectedStrategicOptionRationale { get; set; } = string.Empty;
        public string GeneralStrategicObjective { get; set; } = string.Empty;

        // القوائم المرتبطة
        public List<DataSource> DataSources { get; set; } = new();
        public List<ContextIndicator> ContextIndicators { get; set; } = new();
        public List<StakeholderEntry> Stakeholders { get; set; } = new();
        public List<SwotItem> SwotItems { get; set; } = new();
        public List<PestelItem> PestelItems { get; set; } = new();
        public List<NeedAssessment> NeedsAssessments { get; set; } = new();
        public List<StrategicOption> StrategicOptions { get; set; } = new();
        public List<TheoryOfChangeNode> TheoryOfChangeNodes { get; set; } = new();
        public List<StrategicObjective> StrategicObjectives { get; set; } = new();
        public List<RiskEntry> Risks { get; set; } = new();
        public List<AssumptionEntry> Assumptions { get; set; } = new();
    }

    /// <summary>
    /// سجل مصادر البيانات والمستندات
    /// </summary>
    public class DataSource : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(50)]
        public string SourceType { get; set; } = "Document"; // Document, Statistic, Report, Interview, FieldSurvey

        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(20)]
        public string ReliabilityLevel { get; set; } = "High"; // High, Medium, Low

        public DateTime? PublicationDate { get; set; }

        public string ExtractedKeyInsights { get; set; } = string.Empty;

        public string RawExtractedText { get; set; } = string.Empty;
    }

    /// <summary>
    /// مؤشر خط الأساس السياقي (Context Baseline Indicator)
    /// </summary>
    public class ContextIndicator : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BaselineValue { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Unit { get; set; } = string.Empty;

        public int Year { get; set; } = DateTime.UtcNow.Year;

        [MaxLength(200)]
        public string SourceName { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// تحليل الطرف المعني وموقعه في مصفوفة القوة والاهتمام
    /// </summary>
    public class StakeholderEntry : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Category { get; set; } = "Beneficiary"; // Beneficiary, Authority, Partner, CivilSociety, Donor

        [MaxLength(150)]
        public string Role { get; set; } = string.Empty;

        [Range(1, 5)]
        public int PowerLevel { get; set; } = 3; // 1 (Weak) to 5 (Dominant)

        [Range(1, 5)]
        public int InterestLevel { get; set; } = 3; // 1 (Low) to 5 (High)

        public string Expectations { get; set; } = string.Empty;

        [MaxLength(100)]
        public string EngagementStrategy { get; set; } = "ManageClosely"; // ManageClosely, KeepSatisfied, KeepInformed, Monitor
    }

    /// <summary>
    /// عنصر من مصفوفة SWOT مع التوثيق
    /// </summary>
    public class SwotItem : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(30)]
        public string Category { get; set; } = "Strength"; // Strength, Weakness, Opportunity, Threat

        [Required]
        public string Statement { get; set; } = string.Empty;

        public string SourceEvidence { get; set; } = string.Empty;

        [Range(1, 5)]
        public int ImportanceWeight { get; set; } = 3;
    }

    /// <summary>
    /// عنصر من تحليل PESTEL البيئي الكلي
    /// </summary>
    public class PestelItem : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(30)]
        public string Dimension { get; set; } = "Political"; // Political, Economic, Social, Technological, Environmental, Legal

        [Required]
        public string Description { get; set; } = string.Empty;

        [MaxLength(20)]
        public string ImpactOnProject { get; set; } = "Neutral"; // Positive, Negative, Neutral
    }

    /// <summary>
    /// تقييم احتياج مرجح بالأولويات
    /// </summary>
    public class NeedAssessment : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        public string NeedDescription { get; set; } = string.Empty;

        [MaxLength(200)]
        public string TargetGroupAffected { get; set; } = string.Empty;

        [Range(1, 5)]
        public int UrgencyScore { get; set; } = 3;

        [Range(1, 5)]
        public int FeasibilityScore { get; set; } = 3;

        [Range(1, 5)]
        public int ImpactScore { get; set; } = 3;

        [Column(TypeName = "decimal(18,2)")]
        public decimal WeightedScore => (UrgencyScore * 0.35m) + (FeasibilityScore * 0.25m) + (ImpactScore * 0.40m);

        public int PriorityRank { get; set; } = 1;
    }

    /// <summary>
    /// خيار استراتيجي مع مصفوفة القرار
    /// </summary>
    public class StrategicOption : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(200)]
        public string OptionTitle { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FeasibilityRating { get; set; } = 3.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ImpactRating { get; set; } = 3.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostEffectivenessRating { get; set; } = 3.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalScore => (FeasibilityRating + ImpactRating + CostEffectivenessRating) / 3.0m;

        public bool IsSelected { get; set; } = false;

        public string DecisionRationale { get; set; } = string.Empty;
    }

    /// <summary>
    /// عقدة في نظرية التغيير (Theory of Change)
    /// </summary>
    public class TheoryOfChangeNode : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(30)]
        public string NodeType { get; set; } = "Activity"; // Input, Activity, Output, Outcome, Impact

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string LinkedAssumptions { get; set; } = string.Empty;

        public int OrderIndex { get; set; } = 0;

        public int? ParentNodeId { get; set; }
    }

    /// <summary>
    /// هدف استراتيجي عام أو نوعي متوافق مع معايير SMART
    /// </summary>
    public class StrategicObjective : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = "SO1";

        [Required]
        [MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsSpecificObjective { get; set; } = true;

        // معايير SMART
        public bool IsSpecific { get; set; } = true;
        public bool IsMeasurable { get; set; } = true;
        public bool IsAchievable { get; set; } = true;
        public bool IsRelevant { get; set; } = true;
        public bool IsTimeBound { get; set; } = true;

        [MaxLength(100)]
        public string TargetTimeline { get; set; } = string.Empty;

        public string LinkedNeedIds { get; set; } = string.Empty;
    }

    /// <summary>
    /// سجل المخاطر المعمق مع الدرجة وإجراءات التخفيف
    /// </summary>
    public class RiskEntry : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [MaxLength(20)]
        public string Code { get; set; } = "R01";

        [MaxLength(50)]
        public string Category { get; set; } = "Operational"; // Operational, Financial, Environmental, Social, Political, Technical

        [Required]
        public string Description { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Probability { get; set; } = 3;

        [Range(1, 5)]
        public int Impact { get; set; } = 3;

        public int RiskScore => Probability * Impact;

        public string MitigationStrategy { get; set; } = string.Empty;

        [MaxLength(150)]
        public string ResponsibleParty { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Status { get; set; } = "Active"; // Active, Mitigated, Closed
    }

    /// <summary>
    /// فرضية أساسية في السلسلة التنموية
    /// </summary>
    public class AssumptionEntry : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int StrategicFoundationId { get; set; }
        public StrategicFoundation? StrategicFoundation { get; set; }

        [Required]
        public string Statement { get; set; } = string.Empty;

        [MaxLength(50)]
        public string AssociatedLevel { get; set; } = "ActivityToOutput"; // ActivityToOutput, OutputToOutcome, OutcomeToImpact

        public string ValidationMechanism { get; set; } = string.Empty;
    }

    /// <summary>
    /// كيان تتبع الروابط التلقائية بين الوحدات (Traceability & Propagation Link)
    /// </summary>
    public class StrategicLink : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [Required]
        [MaxLength(60)]
        public string SourceEntityType { get; set; } = string.Empty; // e.g. StrategicObjective, TheoryOfChangeNode

        public int SourceEntityId { get; set; }

        [Required]
        [MaxLength(60)]
        public string TargetEntityType { get; set; } = string.Empty; // e.g. Objective, Result, Activity, Indicator

        public int TargetEntityId { get; set; }

        [Required]
        [MaxLength(50)]
        public string LinkType { get; set; } = string.Empty; // FeedsObjective, FeedsLogframe, FeedsGantt, FeedsBudget, FeedsCanvas

        [MaxLength(30)]
        public string Status { get; set; } = "Synced"; // Synced, Stale, Overridden

        public string? LastSyncValue { get; set; }

        public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;

        public string? Notes { get; set; }
    }

    /// <summary>
    /// كيان سجل التدقيق والتجاوزات الإدارية المبررة (Audit Log Entry)
    /// </summary>
    public class AuditLogEntry : StrategicBaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int? ProjectId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ActionType { get; set; } = string.Empty;

        [MaxLength(150)]
        public string UserEmail { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public string OverrideReason { get; set; } = string.Empty;

        public string ImpactSummary { get; set; } = string.Empty;
    }
}

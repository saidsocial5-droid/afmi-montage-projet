using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models
{
    public enum TextAssistActionType
    {
        Enrichir,
        Detailler,
        Reformuler
    }

    public enum TargetLengthOption
    {
        PlusCourt,
        Equivalent,
        PlusLong
    }

    public enum ToneRegisterOption
    {
        InstitutionnelBailleur,
        Technique,
        SimpleAccessible
    }

    public enum OutputLanguageOption
    {
        Auto,
        Francais,
        Arabe,
        Anglais
    }

    public enum FieldSemanticType
    {
        Generique,
        Titre,
        ProblemeCentral,
        Cause,
        Effet,
        ObjectifGeneral,
        ObjectifSpecifique,
        Resultat,
        Activite,
        Indicateur,
        SourceVerification,
        Hypothese,
        Risque,
        JustificationBudgetaire,
        ContexteGeneral,
        Probleme = ProblemeCentral,
        LigneBudgetaire = JustificationBudgetaire
    }

    [Table("TextVersionHistories")]
    public class TextVersionHistory
    {
        [Key]
        public int Id { get; set; }

        public string GlobalId { get; set; } = Guid.NewGuid().ToString();

        public int? ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FieldIdentifier { get; set; } = string.Empty;

        [MaxLength(50)]
        public string FieldSemanticType { get; set; } = nameof(Models.FieldSemanticType.Generique);

        [Required]
        [MaxLength(50)]
        public string ActionType { get; set; } = nameof(TextAssistActionType.Enrichir);

        public string OriginalText { get; set; } = string.Empty;

        public string ProposedText { get; set; } = string.Empty;

        public string FinalText { get; set; } = string.Empty;

        public string? ContextUsedJson { get; set; }

        public string? WarningsJson { get; set; }

        [MaxLength(100)]
        public string ProviderUsed { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ModelUsed { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(150)]
        public string? CreatedBy { get; set; }
    }

    [Table("TextAssistAuditLogs")]
    public class TextAssistAuditLog
    {
        [Key]
        public int Id { get; set; }

        public int? ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FieldIdentifier { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ProviderUsed { get; set; } = string.Empty;

        [MaxLength(100)]
        public string ModelUsed { get; set; } = string.Empty;

        public bool WasAccepted { get; set; }

        public bool WasModified { get; set; }

        public int OriginalLength { get; set; }

        public int FinalLength { get; set; }

        public long ElapsedMilliseconds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(150)]
        public string? CreatedBy { get; set; }
    }

    [Table("TextAssistSettings")]
    public class TextAssistSettings
    {
        [Key]
        public int Id { get; set; }

        public int? ProjectId { get; set; }

        [MaxLength(150)]
        public string? UserId { get; set; }

        [MaxLength(50)]
        public string TargetLength { get; set; } = nameof(TargetLengthOption.Equivalent);

        [MaxLength(50)]
        public string ToneRegister { get; set; } = nameof(ToneRegisterOption.InstitutionnelBailleur);

        [MaxLength(50)]
        public string OutputLanguage { get; set; } = nameof(OutputLanguageOption.Auto);

        public string? CustomInstruction { get; set; }

        public bool AllowCloudAi { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

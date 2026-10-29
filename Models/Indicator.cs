using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Indicator : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        public int? ObjectiveId { get; set; }
        public Objective? Objective { get; set; }

        public int? ResultId { get; set; }
        public Result? Result { get; set; }

        public int? ActivityId { get; set; }
        public Activity? Activity { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string Baseline { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string ActualValue { get; set; } = string.Empty; // Valeur atteinte réelllement

        // Ventilations démographiques
        public string GenderMaleValue { get; set; } = string.Empty;
        public string GenderFemaleValue { get; set; } = string.Empty;
        public string AgeYouthValue { get; set; } = string.Empty;
        public string AgeAdultValue { get; set; } = string.Empty;

        public string VerificationSource { get; set; } = string.Empty;
        public string CollectionMethod { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string ResponsibleRole { get; set; } = string.Empty;

        // Liaison indicateur standard
        public int? StandardIndicatorId { get; set; }
        public StandardIndicator? StandardIndicator { get; set; }
    }
}





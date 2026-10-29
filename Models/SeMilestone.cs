using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class SeMilestone : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime TargetDate { get; set; }

        public string Frequency { get; set; } = "Ponctuelle"; // Ponctuelle, Mensuelle, Trimestrielle

        public string ResponsibleRole { get; set; } = string.Empty;

        public string ExpectedDeliverable { get; set; } = string.Empty; // ex: Rapport d'avancement, Synthèse d'évaluation

        [MaxLength(50)]
        public string Status { get; set; } = "A faire"; // A faire, En cours, Réalisé, En retard
    }
}





using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class SafeguardEvaluation : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty; // ex: Biodiversité, Peuples autochtones, Genre, etc.

        [Required]
        [MaxLength(50)]
        public string RiskLevel { get; set; } = "Non prévu"; // Non prévu / Non applicable / Faible / Moyen / Fort

        public string Description { get; set; } = string.Empty; // Description du risque potentiel

        public string MitigationAction { get; set; } = string.Empty; // Mesure de gestion proposée

        public string Status { get; set; } = "Examen initial"; // Examen initial, Validé
    }
}





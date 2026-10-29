using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Risk : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = "Interne"; // Interne, Externe, Financier, Social, Environnemental

        [Required]
        public string Description { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Probability { get; set; } = 3; // Échelle de 1 à 5

        [Range(1, 5)]
        public int Impact { get; set; } = 3; // Échelle de 1 à 5

        public string MitigationMeasure { get; set; } = string.Empty; // Mesure d'atténuation

        public string ResponsibleRole { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Status { get; set; } = "Actif"; // Actif, Atténué, Clos

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}





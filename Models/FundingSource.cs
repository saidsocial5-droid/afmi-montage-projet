using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class FundingSource : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // ex: FEM, Communauté locale, Partenaire X

        [MaxLength(50)]
        public string Type { get; set; } = "Bailleur principal"; // Bailleur principal, Apport propre, Cofinancement espèces, Cofinancement nature

        public string NatureOfFunding { get; set; } = "Espèces"; // Espèces, Nature (ex: locaux, temps de travail)

        [MaxLength(50)]
        public string Status { get; set; } = "Disponible"; // Disponible, Projeté

        public decimal TotalAmountAllocated { get; set; }

        [MaxLength(10)]
        public string Currency { get; set; } = "EUR";
    }
}





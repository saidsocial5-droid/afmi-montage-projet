using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class BudgetItem : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        public int CategoryId { get; set; }
        public BudgetCategory? Category { get; set; }

        public int? ActivityId { get; set; }
        public Activity? Activity { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Unit { get; set; } = "Mois";

        public decimal Quantity { get; set; } = 1;
        public decimal UnitCost { get; set; } = 0;
        public decimal Frequency { get; set; } = 1;

        // Le coût total est calculé : Quantity * UnitCost * Frequency
        public decimal TotalCost => Quantity * UnitCost * Frequency;

        // Répartition des financements
        public decimal FundingAssociation { get; set; } = 0;
        public decimal FundingCofinancing { get; set; } = 0;
        public decimal FundingRequested { get; set; } = 0;

        public string Notes { get; set; } = string.Empty;
    }
}





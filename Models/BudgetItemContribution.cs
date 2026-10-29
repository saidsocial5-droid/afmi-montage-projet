using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class BudgetItemContribution : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BudgetItemId { get; set; }
        public BudgetItem? BudgetItem { get; set; }

        [Required]
        public int FundingSourceId { get; set; }
        public FundingSource? FundingSource { get; set; }

        public decimal AllocatedAmount { get; set; } // Part financée par cette source spécifique
    }
}





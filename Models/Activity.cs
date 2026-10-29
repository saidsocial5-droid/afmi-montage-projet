using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Activity : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ResultId { get; set; }
        public Result? Result { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal BudgetAllocated { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public decimal TotalBudget { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string DisplayName => $"{Code} - {Description}";

        public int? OutputId { get; set; }
        public Output? Output { get; set; }

        // Relations
        public List<Indicator> Indicators { get; set; } = new();

        // V3 : Fonctionnalités Collaboratives
        public int? ProposedByUserId { get; set; }
        public User? ProposedByUser { get; set; }
        
        [MaxLength(20)]
        public string Status { get; set; } = "Approved"; // Pending, Approved, Rejected
        
        public bool IsAnonymized { get; set; } = false;
        public string AiSuggestionBadge { get; set; } = string.Empty;
    }
}





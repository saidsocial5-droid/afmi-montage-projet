using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Result : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ObjectiveId { get; set; }
        public Objective? Objective { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        // Relations
        public List<Output> Outputs { get; set; } = new();
        public List<Activity> Activities { get; set; } = new();

        // V3 : Fonctionnalités Collaboratives
        public int? ProposedByUserId { get; set; }
        public User? ProposedByUser { get; set; }
        
        [MaxLength(20)]
        public string Status { get; set; } = "Approved"; // Pending, Approved, Rejected
        
        public bool IsAnonymized { get; set; } = false;
        public string AiSuggestionBadge { get; set; } = string.Empty;
    }
}





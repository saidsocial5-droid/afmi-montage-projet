using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models
{
    public class ActivityReport : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ActivityId { get; set; }
        public Activity? Activity { get; set; }

        public int? ReportedByUserId { get; set; }
        public User? ReportedByUser { get; set; }

        public DateTime ReportDate { get; set; } = DateTime.UtcNow;

        public string Narrative { get; set; } = string.Empty;

        public decimal ExecutionRate { get; set; } // 0 to 100

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        // List of uploaded photo paths (JSON array or semicolon separated)
        public string PhotoPaths { get; set; } = string.Empty;
    }
}




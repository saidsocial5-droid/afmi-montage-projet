using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(150)]
        public string UserEmail { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        // Old properties restored for compatibility
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public int ActionUserId { get; set; }
        public string ActionType { get; set; } = string.Empty;
    }
}

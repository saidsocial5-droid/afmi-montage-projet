using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class AiLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string EngineUsed { get; set; } = string.Empty; // Local ou Cloud

        [Required]
        public string PromptSummary { get; set; } = string.Empty;

        public long ResponseTimeMs { get; set; }

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

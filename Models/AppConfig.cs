using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class AppConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = "Gemini"; // "Gemini", "OpenAI", "Claude"

        public string EncryptedApiKey { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SelectedModel { get; set; } = "gemini-pro";

        // Paramètres de configuration IA Hybride
        public bool UseLocalByDefault { get; set; } = true;
        public bool CloudOptInGranted { get; set; } = false;
        public bool DataMinimizationEnabled { get; set; } = true;
        public int MonthlyRequestLimit { get; set; } = 200;
    }
}


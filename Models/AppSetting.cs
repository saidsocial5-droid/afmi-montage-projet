using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class AppSetting
    {
        [Key]
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Scope { get; set; } = "Global";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = string.Empty;
    }
}

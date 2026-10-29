using System;
using System.Linq;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    public static class SettingsService
    {
        public static event Action<string, string>? OnSettingChanged;

        public static string GetSetting(string key, string defaultValue = "")
        {
            try
            {
                using var db = new AppDbContext();
                var setting = db.AppSettings.FirstOrDefault(s => s.Key == key);
                return setting != null ? setting.Value : defaultValue;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(SettingsService), $"Lecture du paramètre '{key}' impossible, valeur par défaut utilisée.", ex);
                return defaultValue;
            }
        }

        /// <summary>Enregistre un paramètre. Retourne false (et journalise) en cas d'échec.</summary>
        public static bool SaveSetting(string key, string value, string scope = "Global")
        {
            try
            {
                using var db = new AppDbContext();
                var setting = db.AppSettings.FirstOrDefault(s => s.Key == key);
                if (setting == null)
                {
                    db.AppSettings.Add(new AppSetting { Key = key, Value = value, Scope = scope, LastUpdated = DateTime.UtcNow });
                }
                else
                {
                    setting.Value = value;
                    setting.Scope = scope;
                    setting.LastUpdated = DateTime.UtcNow;
                }
                db.SaveChanges();
                OnSettingChanged?.Invoke(key, value);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(SettingsService), $"Échec de l'enregistrement du paramètre '{key}'.", ex);
                return false;
            }
        }
    }
}

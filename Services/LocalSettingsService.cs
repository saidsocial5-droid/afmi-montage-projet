using System;
using System.IO;
using System.Text.Json;

namespace AFMI_Montage_projet_2.Services
{
    public class LocalSettings
    {
        public string? BackgroundImagePath { get; set; }
        public bool RememberMe { get; set; }
        public string? SavedUsername { get; set; }
        public string OllamaUrl { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "";
    }

    public static class LocalSettingsService
    {
        // Suit la redirection AFMI_DATA_DIR (tests) comme la base locale : les tests ne lisent ni n'écrasent jamais les réglages réels
        private static string SettingsFolder => Models.AppDbContext.DataDirectoryOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AFMI_Montage_Projet");
        private static string SettingsFile => Path.Combine(SettingsFolder, "settings.json");

        public static LocalSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<LocalSettings>(json) ?? new LocalSettings();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(LocalSettingsService), "Réglages locaux illisibles : valeurs par défaut utilisées.", ex);
            }
            return new LocalSettings();
        }

        public static void SaveSettings(LocalSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                    Directory.CreateDirectory(SettingsFolder);

                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(LocalSettingsService), "Impossible d'enregistrer les réglages locaux.", ex);
            }
        }
    }
}

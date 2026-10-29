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
        private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AFMI_Montage_Projet");
        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

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
            catch
            {
                // Ignore errors
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
            catch
            {
                // Ignore errors
            }
        }
    }
}

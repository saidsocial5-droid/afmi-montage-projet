using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFMI_Montage_projet_2.Helpers;

namespace AFMI_Montage_projet_2.Services
{
    public class DatabaseSettings
    {
        public bool UseCloud { get; set; } = false;
        public string Host { get; set; } = string.Empty;
        public string Port { get; set; } = "5432";
        public string Database { get; set; } = "postgres";
        public string Username { get; set; } = "postgres";

        /// <summary>Mot de passe en clair, en mémoire uniquement (jamais écrit sur disque).</summary>
        [JsonIgnore]
        public string Password { get; set; } = string.Empty;

        /// <summary>Mot de passe chiffré DPAPI (forme persistée dans dbconfig.json).</summary>
        public string PasswordProtected { get; set; } = string.Empty;

        /// <summary>Ancien champ en clair des versions précédentes, lu une seule fois pour migration.</summary>
        [JsonPropertyName("Password")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? LegacyPassword { get; set; }

        public string GetPostgresConnectionString()
        {
            return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Include Error Detail=true";
        }

        public DatabaseSettings Clone() => (DatabaseSettings)MemberwiseClone();
    }

    public static class DatabaseConfigManager
    {
        private static readonly object _lock = new();
        private static DatabaseSettings? _cached;

        private static string GetConfigPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "AFMI_Montage_Projet");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "dbconfig.json");
        }

        /// <summary>
        /// Charge la configuration (mise en cache : AppDbContext l'appelle à chaque instanciation).
        /// Retourne une copie pour que les modifications non enregistrées n'affectent pas le cache.
        /// </summary>
        public static DatabaseSettings LoadSettings()
        {
            lock (_lock)
            {
                _cached ??= LoadFromDisk();
                return _cached.Clone();
            }
        }

        private static DatabaseSettings LoadFromDisk()
        {
            try
            {
                string path = GetConfigPath();
                if (!File.Exists(path)) return new DatabaseSettings();

                var settings = FromJson(File.ReadAllText(path), out bool needsRewrite);
                if (needsRewrite)
                {
                    // Migration : l'ancien fichier contenait le mot de passe en clair → on le réécrit chiffré.
                    File.WriteAllText(path, ToJson(settings));
                    AppLogger.Info(nameof(DatabaseConfigManager), "Mot de passe PostgreSQL migré vers un stockage chiffré (DPAPI).");
                }
                return settings;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(DatabaseConfigManager), "Lecture de dbconfig.json impossible, configuration locale par défaut utilisée.", ex);
                return new DatabaseSettings();
            }
        }

        /// <summary>Enregistre la configuration. Retourne false (et journalise) en cas d'échec.</summary>
        public static bool SaveSettings(DatabaseSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    WriteToDisk(settings);
                    _cached = settings.Clone();
                    _cached.LegacyPassword = null;
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Error(nameof(DatabaseConfigManager), "Échec de l'enregistrement de dbconfig.json.", ex);
                    return false;
                }
            }
        }

        private static void WriteToDisk(DatabaseSettings settings) => File.WriteAllText(GetConfigPath(), ToJson(settings));

        /// <summary>Sérialise la configuration ; le mot de passe n'est écrit que sous forme chiffrée (DPAPI).</summary>
        public static string ToJson(DatabaseSettings settings)
        {
            var toWrite = settings.Clone();
            toWrite.PasswordProtected = EncryptionHelper.Protect(settings.Password);
            toWrite.LegacyPassword = null;
            return JsonSerializer.Serialize(toWrite, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Désérialise la configuration et déchiffre le mot de passe.
        /// <paramref name="needsRewrite"/> vaut true si le fichier contenait encore un mot de passe en clair (ancien format).
        /// </summary>
        public static DatabaseSettings FromJson(string json, out bool needsRewrite)
        {
            var settings = JsonSerializer.Deserialize<DatabaseSettings>(json) ?? new DatabaseSettings();
            needsRewrite = false;

            if (!string.IsNullOrEmpty(settings.PasswordProtected))
            {
                settings.Password = EncryptionHelper.Unprotect(settings.PasswordProtected);
            }
            else if (!string.IsNullOrEmpty(settings.LegacyPassword))
            {
                settings.Password = settings.LegacyPassword;
                needsRewrite = true;
            }
            settings.LegacyPassword = null;
            return settings;
        }
    }
}

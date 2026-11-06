using System;
using System.IO;
using System.Runtime.CompilerServices;
using AFMI_Montage_projet_2.Models;
using Microsoft.Data.Sqlite;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// Isole les tests des données réelles de l'utilisateur : toute base SQLite, sauvegarde ou journal
    /// créé via les chemins par défaut de l'application est redirigé vers un dossier temporaire.
    /// </summary>
    internal static class TestEnvironment
    {
        public static string DataDirectory { get; private set; } = string.Empty;

        /// <summary>Adresse Ollama des tests : port 9 (discard) en boucle locale, toujours fermé.</summary>
        public const string UnreachableOllamaUrl = "http://127.0.0.1:9";

        [ModuleInitializer]
        internal static void Initialize()
        {
            // Même culture de formatage que l'application, quelle que soit la langue du poste ou du serveur d'intégration
            AFMI_Montage_projet_2.Helpers.AppCulture.Apply();

            DataDirectory = Path.Combine(Path.GetTempPath(), "AFMI_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DataDirectory);
            Environment.SetEnvironmentVariable(AppDbContext.DataDirectoryOverrideVariable, DataDirectory);

            using (var db = new AppDbContext())
            {
                db.SeedDefaultData();
            }

            // Jamais d'IA réelle dans les tests : Ollama pointe vers un port fermé (connexion refusée aussitôt),
            // ce qui rend déterministes les tests qui passent par le repli sans IA, avec ou sans Ollama sur le poste.
            AFMI_Montage_projet_2.Services.LocalSettingsService.SaveSettings(
                new AFMI_Montage_projet_2.Services.LocalSettings { OllamaUrl = UnreachableOllamaUrl });

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    Directory.Delete(DataDirectory, recursive: true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            };
        }
    }
}

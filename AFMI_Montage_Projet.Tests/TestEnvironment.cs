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

        [ModuleInitializer]
        internal static void Initialize()
        {
            DataDirectory = Path.Combine(Path.GetTempPath(), "AFMI_Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DataDirectory);
            Environment.SetEnvironmentVariable(AppDbContext.DataDirectoryOverrideVariable, DataDirectory);

            using (var db = new AppDbContext())
            {
                db.SeedDefaultData();
            }

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

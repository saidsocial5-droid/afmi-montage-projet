using System;
using System.IO;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// Durcissement de l'infrastructure : stockage chiffré du mot de passe PostgreSQL,
    /// mise à niveau idempotente du schéma SQLite, version de l'application.
    /// </summary>
    public class InfrastructureHardeningTests
    {
        [Fact]
        public void DatabaseSettings_ToJson_NeverWritesClearTextPassword()
        {
            var settings = new DatabaseSettings { UseCloud = true, Host = "db.example.org", Password = "S3cret!Pg" };

            string json = DatabaseConfigManager.ToJson(settings);

            Assert.DoesNotContain("S3cret!Pg", json);
            Assert.DoesNotContain("\"Password\"", json);
            Assert.Contains("PasswordProtected", json);
        }

        [Fact]
        public void DatabaseSettings_RoundTrip_RestoresPassword()
        {
            var settings = new DatabaseSettings { Host = "h", Password = "pässwörd-ü" };

            var restored = DatabaseConfigManager.FromJson(DatabaseConfigManager.ToJson(settings), out bool needsRewrite);

            Assert.Equal("pässwörd-ü", restored.Password);
            Assert.Equal("h", restored.Host);
            Assert.False(needsRewrite);
        }

        [Fact]
        public void DatabaseSettings_LegacyClearTextFile_IsReadAndFlaggedForRewrite()
        {
            const string legacyJson = "{ \"UseCloud\": true, \"Host\": \"old\", \"Port\": \"5432\", \"Database\": \"postgres\", \"Username\": \"u\", \"Password\": \"legacy-pw\" }";

            var settings = DatabaseConfigManager.FromJson(legacyJson, out bool needsRewrite);

            Assert.Equal("legacy-pw", settings.Password);
            Assert.True(needsRewrite);
            Assert.DoesNotContain("legacy-pw", DatabaseConfigManager.ToJson(settings));
        }

        [Fact]
        public void SeedDefaultData_UpgradesLegacySchema_AndIsIdempotent()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

            using (var ctx = new AppDbContext(options))
            {
                ctx.Database.EnsureCreated();
                // Simuler une ancienne base : supprimer une colonne ajoutée dans une version ultérieure
                ctx.Database.ExecuteSqlRaw("ALTER TABLE \"Indicators\" DROP COLUMN \"AgeAdultValue\";");
            }

            using (var ctx = new AppDbContext(options))
            {
                ctx.SeedDefaultData();
                ctx.SeedDefaultData(); // deuxième passage : ne doit rien casser
            }

            using (var ctx = new AppDbContext(options))
            {
                var columns = ctx.Database.SqlQueryRaw<string>("SELECT name AS \"Value\" FROM pragma_table_info('Indicators')").ToList();
                Assert.Contains("AgeAdultValue", columns);
                Assert.True(ctx.BudgetCategories.Count() == 6, "Les catégories budgétaires par défaut ne doivent être créées qu'une fois.");
            }
        }

        [Fact]
        public void AppVersion_IsDefinedAndMatchesInstaller()
        {
            Assert.NotEqual("0.0.0", AutoUpdaterService.CurrentVersion);

            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "setup.iss"))) dir = Directory.GetParent(dir)?.FullName;
            if (dir == null) return; // hors arborescence du dépôt

            string iss = File.ReadAllText(Path.Combine(dir, "setup.iss"));
            Assert.Contains($"#define MyAppVersion \"{AutoUpdaterService.CurrentVersion}\"", iss);
        }
    }
}

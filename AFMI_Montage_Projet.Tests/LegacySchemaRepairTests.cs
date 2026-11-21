using System;
using System.IO;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// ADR 34 : une ancienne base dont la table Users a été créée sans les colonnes Status et Role
    /// (bloc « CREATE TABLE » historique) doit être réparée au démarrage : l'inscription échouait avec
    /// « An error occurred while saving the entity changes ».
    /// </summary>
    public class LegacySchemaRepairTests : IDisposable
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"afmi_legacy_users_{Guid.NewGuid():N}.db");
        private readonly DbContextOptions<AppDbContext> _options;

        public LegacySchemaRepairTests()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
            catch (IOException) { /* fichier temporaire, supprimé par le système plus tard */ }
        }

        private void CreateLegacyUsersTable()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "CREATE TABLE \"Users\" (\"Id\" INTEGER PRIMARY KEY AUTOINCREMENT, \"Username\" TEXT NOT NULL, \"Email\" TEXT NOT NULL, \"PasswordHash\" TEXT NOT NULL, \"IsSystemAdmin\" INTEGER NOT NULL, \"CreatedAt\" TEXT NOT NULL);" +
                "INSERT INTO \"Users\" (\"Username\", \"Email\", \"PasswordHash\", \"IsSystemAdmin\", \"CreatedAt\") VALUES ('ancien', 'ancien.admin@example.org', 'x', 1, '2025-01-01 00:00:00');" +
                "INSERT INTO \"Users\" (\"Username\", \"Email\", \"PasswordHash\", \"IsSystemAdmin\", \"CreatedAt\") VALUES ('membre', 'membre@example.org', 'x', 0, '2025-01-01 00:00:00');";
            cmd.ExecuteNonQuery();
        }

        [Fact]
        public void LegacyUsersTable_GetsMissingColumns_AndRegistrationWorks()
        {
            CreateLegacyUsersTable();

            using (var db = new AppDbContext(_options))
            {
                db.SeedDefaultData();
            }

            using (var db = new AppDbContext(_options))
            {
                db.Users.Add(new User { Username = "said", Email = "said@example.org", PasswordHash = "x", Status = "Active", Role = "SuperAdmin" });
                db.SaveChanges(); // échouait avant l'ADR 34

                var users = db.Users.AsNoTracking().OrderBy(u => u.Id).ToList();
                Assert.Equal(3, users.Count);
                // Comptes existants : restent actifs, l'administrateur système garde ses droits
                Assert.Equal("Active", users[0].Status);
                Assert.Equal("SuperAdmin", users[0].Role);
                Assert.Equal("Active", users[1].Status);
                Assert.Equal("Reader", users[1].Role);
                Assert.Equal("said", users[2].Username);
            }
        }

        [Fact]
        public void SchemaRepair_IsIdempotent()
        {
            CreateLegacyUsersTable();
            using (var db = new AppDbContext(_options)) db.SeedDefaultData();
            using (var db = new AppDbContext(_options)) db.SeedDefaultData(); // second démarrage : rien à ajouter, aucune erreur

            using var check = new AppDbContext(_options);
            Assert.Equal(2, check.Users.Count());
        }
    }
}

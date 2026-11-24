using System;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>ADR 35 : mots de passe des comptes locaux hachés (PBKDF2), anciens comptes convertis sans être bloqués.</summary>
    public class PasswordHashingTests
    {
        [Fact]
        public void Hash_IsSaltedAndVerifies()
        {
            string first = PasswordHasher.Hash("Ifrane2026!");
            string second = PasswordHasher.Hash("Ifrane2026!");

            Assert.True(PasswordHasher.IsHashed(first));
            Assert.DoesNotContain("Ifrane2026!", first);
            Assert.NotEqual(first, second); // sel aléatoire

            Assert.True(PasswordHasher.Verify(first, "Ifrane2026!", out bool needsRehash));
            Assert.False(needsRehash);
            Assert.False(PasswordHasher.Verify(first, "ifrane2026!", out _));
            Assert.False(PasswordHasher.Verify(first, "", out _));
        }

        [Fact]
        public void Verify_AcceptsLegacyPlainText_AndAsksForRehash()
        {
            Assert.True(PasswordHasher.Verify("ancien-mdp", "ancien-mdp", out bool needsRehash));
            Assert.True(needsRehash);

            Assert.False(PasswordHasher.Verify("ancien-mdp", "autre", out needsRehash));
            Assert.False(needsRehash);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("PBKDF2$SHA512$abc$AAAA$AAAA")]
        [InlineData("PBKDF2$SHA512$1000$pas-du-base64$AAAA")]
        [InlineData("PBKDF2$MD5$1000$AAAA$AAAA")]
        [InlineData("PBKDF2$SHA512$1000$AAAA")]
        public void Verify_RejectsEmptyOrMalformedValues(string? stored)
        {
            Assert.False(PasswordHasher.Verify(stored, "x", out bool needsRehash));
            Assert.False(needsRehash);
        }

        [Fact]
        public void SeedDefaultData_HashesLegacyPasswords_Once()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

            string alreadyHashed = PasswordHasher.Hash("deja-hache");
            using (var db = new AppDbContext(options))
            {
                db.Database.EnsureCreated();
                db.Users.Add(new User { Username = "ancien", Email = "ancien@example.org", PasswordHash = "clair", Status = "Active", Role = "Reader" });
                db.Users.Add(new User { Username = "recent", Email = "recent@example.org", PasswordHash = alreadyHashed, Status = "Active", Role = "Reader" });
                db.SaveChanges();
            }

            using (var db = new AppDbContext(options)) db.HashLegacyPasswords();

            string converted;
            using (var db = new AppDbContext(options))
            {
                var users = db.Users.AsNoTracking().OrderBy(u => u.Username).ToList();
                converted = users[0].PasswordHash;
                Assert.True(PasswordHasher.IsHashed(converted));
                Assert.True(PasswordHasher.Verify(converted, "clair", out bool needsRehash));
                Assert.False(needsRehash);
                Assert.Equal(alreadyHashed, users[1].PasswordHash); // déjà haché : inchangé
            }

            using (var db = new AppDbContext(options)) db.HashLegacyPasswords(); // idempotent

            using var check = new AppDbContext(options);
            Assert.Equal(converted, check.Users.AsNoTracking().Single(u => u.Username == "ancien").PasswordHash);
        }
    }
}

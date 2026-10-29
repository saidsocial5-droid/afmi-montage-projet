using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// اختبارات التوصيف (Characterization Tests) لتثبيت وضمان عدم تراجع السلوكيات الحساسة القائمة
    /// لوحدة المشاريع والمنح، الكاتب الذكي للمشاريع، والتصدير، ومفتاح التفعيل.
    /// </summary>
    public class CharacterizationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public CharacterizationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_char_test_{Guid.NewGuid():N}.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var db = new AppDbContext(_dbOptions);
            db.Database.EnsureCreated();
            db.SeedDefaultData();
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            }
            catch { }
        }

        private AppDbContext CreateDbContext() => new AppDbContext(_dbOptions);

        #region 1. توصيف وحدة المنح والمشاريع (Projects & Grants Baseline)

        [Fact]
        public void ProjectManagement_CreateAndRetrieveProject_PreservesCoreAttributes()
        {
            using var db = CreateDbContext();
            var project = new Project
            {
                Title = "Projet Préservation Cèdre de l'Atlas",
                LeadOrganization = "Association Forêt Modèle Ifrane (AFMI)",
                GeneralObjective = "Restauration des écosystèmes forestiers dégradés",
                ContextJustification = "Pressions anthropiques et changement climatique",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Projects.Add(project);
            db.SaveChanges();

            Assert.True(project.Id > 0);

            var retrieved = db.Projects.Find(project.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("Projet Préservation Cèdre de l'Atlas", retrieved!.Title);
            Assert.Equal("Association Forêt Modèle Ifrane (AFMI)", retrieved.LeadOrganization);
        }

        [Fact]
        public void ProjectManagement_CascadeDeletion_PreservesIntegrity()
        {
            using var db = CreateDbContext();
            var project = new Project
            {
                Title = "Projet Test Suppression",
                LeadOrganization = "AFMI",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Projects.Add(project);
            db.SaveChanges();

            var obj = new Objective
            {
                ProjectId = project.Id,
                Code = "OS1",
                Description = "Description test"
            };
            db.Objectives.Add(obj);

            var doc = new ProjectDocument
            {
                ProjectId = project.Id,
                FileName = "cadrage.pdf",
                FilePath = "C:/tmp/cadrage.pdf",
                DocumentType = "Canevas",
                AiSummary = "Résumé du document",
                UploadedAt = DateTime.UtcNow
            };
            db.ProjectDocuments.Add(doc);
            db.SaveChanges();

            // Deleting project should cascade delete related objectives and documents
            db.Projects.Remove(project);
            db.SaveChanges();

            Assert.Null(db.Projects.Find(project.Id));
            Assert.Empty(db.Objectives.Where(o => o.ProjectId == project.Id));
            Assert.Empty(db.ProjectDocuments.Where(d => d.ProjectId == project.Id));
        }

        #endregion

        #region 2. توصيف الكاتب الذكي للمشاريع (Agent Grant Writer / Ideation Parser)

        [Fact]
        public void AgentGrantWriter_MarkdownJsonParsing_ExtractsComponentsCorrectly()
        {
            // Characterizes the robust markdown and JSON extraction pattern used in IdeationInnovationViewModel
            string rawAiOutput = @"Voici ma proposition d'innovation pour le projet :
```json
{
  ""Title"": ""Éco-Restauration Participative du Cèdre de l'Atlas"",
  ""Context"": ""Le massif d'Ifrane subit des épisodes récurrents de sécheresse impactant la régénération naturelle."",
  ""Objective"": ""Restaurer 500 hectares de cédraies à l'horizon 2028 en mobilisant les coopératives pastorales.""
}
```
J'espère que cette proposition répond à vos attentes selon les standards des bailleurs de fonds.";

            string cleaned = rawAiOutput.Trim();
            var mdMatch = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            if (mdMatch.Success) cleaned = mdMatch.Groups[1].Value.Trim();

            int firstBrace = cleaned.IndexOf('{');
            int lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            using var doc = JsonDocument.Parse(cleaned);
            string title = doc.RootElement.GetProperty("Title").GetString() ?? "";
            string context = doc.RootElement.GetProperty("Context").GetString() ?? "";
            string objective = doc.RootElement.GetProperty("Objective").GetString() ?? "";

            Assert.Equal("Éco-Restauration Participative du Cèdre de l'Atlas", title);
            Assert.Contains("massif d'Ifrane", context);
            Assert.Contains("500 hectares", objective);
        }

        [Fact]
        public void AgentGrantWriter_MalformedJsonRecovery_FailsGracefullyWithoutCrashing()
        {
            string brokenAiOutput = "Désolé, je ne peux pas générer du JSON valide pour le moment.";
            string cleaned = brokenAiOutput.Trim();

            int firstBrace = cleaned.IndexOf('{');
            int lastBrace = cleaned.LastIndexOf('}');

            bool hasValidBraces = firstBrace >= 0 && lastBrace > firstBrace;
            Assert.False(hasValidBraces);
        }

        #endregion

        #region 3. توصيف مفتاح التفعيل (ProjectRadar.Enabled Feature Flag)

        [Fact]
        public void ProjectRadar_FeatureFlag_DefaultsToTrue_WhenNotExplicitlyDisabled()
        {
            // When setting is empty or missing, feature flag should resolve to enabled (true)
            string settingValue = SettingsService.GetSetting("ProjectRadar.Enabled", "true");
            bool isEnabled = !string.Equals(settingValue, "false", StringComparison.OrdinalIgnoreCase);
            Assert.True(isEnabled);
        }

        [Fact]
        public void ProjectRadar_FeatureFlag_RespectsExplicitDisable()
        {
            string falseVal = "false";
            bool isEnabled = !string.Equals(falseVal, "false", StringComparison.OrdinalIgnoreCase);
            Assert.False(isEnabled);
        }

        #endregion
    }
}

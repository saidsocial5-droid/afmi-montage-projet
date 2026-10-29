using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services.Strategic;

namespace AFMI_Montage_Projet.Tests
{
    public class QualityCheckerAndAiParserTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public QualityCheckerAndAiParserTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_qc_test_{Guid.NewGuid():N}.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var db = CreateDbContext();
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

        [Fact]
        public async Task EvaluateProjectQualityAsync_CatchesMissingFoundation()
        {
            using var db = CreateDbContext();
            var project = new Project
            {
                Title = "Projet Sans Fondation",
                TotalBudget = 10000m,
                Currency = "DZD"
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var checker = new DeterministicQualityChecker(() => CreateDbContext());
            var report = await checker.EvaluateProjectQualityAsync(project.Id);

            Assert.False(report.IsPassed);
            Assert.Contains(report.Findings, f => f.RuleId == "STRAT-01" && f.Severity == FindingSeverity.Critical);
        }

        [Fact]
        public async Task EvaluateProjectQualityAsync_PassesForCompleteProject()
        {
            using var db = CreateDbContext();
            var project = new Project
            {
                Title = "Projet RBM Complet",
                TotalBudget = 80000m,
                Currency = "DZD"
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var foundation = new StrategicFoundation
            {
                ProjectId = project.Id,
                IdeaTitle = "Idée Valide",
                CoreProblemStatement = "Problème Central Documenté",
                ProblemEvidenceData = "Données 25% prouvées",
                EstimatedBudget = 100000m,
                Currency = "DZD" // même devise que le cadrage (règle COH-07)
            };
            // Un projet complet décrit sa zone d'intervention et ses bénéficiaires (ADR 25, règles TER/BEN)
            db.TerritoryProfiles.Add(new TerritoryProfile
            {
                ProjectId = project.Id, Province = "Ifrane", Communes = "Tigrigra", Climate = "Montagnard", Ecosystems = "Cédraie",
                Population = "12 000 habitants", MainLivelihoods = "Élevage"
            });
            db.BeneficiaryGroups.Add(new BeneficiaryGroup { ProjectId = project.Id, Name = "Femmes formées", EstimatedCount = 100, WomenPercent = 100 });
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Impact", Title = "Impact Durable" });
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Outcome", Title = "Effet Spécifique" });
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Activity", Title = "Activité Prévue" });
            foundation.StrategicObjectives.Add(new StrategicObjective
            {
                Code = "OS1",
                Title = "Former 100 femmes en 6 mois",
                Description = "Indicateur 100 femmes",
                IsSpecificObjective = true
            });
            db.StrategicFoundations.Add(foundation);

            var obj = new Objective { ProjectId = project.Id, Code = "OS1", Description = "Former 100 femmes" };
            db.Objectives.Add(obj);
            await db.SaveChangesAsync();

            var res = new Result { ObjectiveId = obj.Id, Code = "R1", Description = "100 femmes qualifiées" };
            db.Results.Add(res);
            await db.SaveChangesAsync();

            var act = new Activity { ResultId = res.Id, Code = "A1.1", Description = "Sessions de formation", BudgetAllocated = 40000m };
            act.Indicators.Add(new Indicator { Code = "IND1", Description = "Nombre de formées", Target = "100" });
            db.Activities.Add(act);

            var cat = db.BudgetCategories.First();
            db.BudgetItems.Add(new BudgetItem { ProjectId = project.Id, CategoryId = cat.Id, Description = "Salles et matériel", Quantity = 1, UnitCost = 40000m, Frequency = 1 });
            await db.SaveChangesAsync();

            var checker = new DeterministicQualityChecker(() => CreateDbContext());
            var report = await checker.EvaluateProjectQualityAsync(project.Id);

            Assert.True(report.IsPassed);
            Assert.Equal(0, report.CriticalCount);
            Assert.True(report.QualityScore >= 80);
        }

        [Fact]
        public async Task EvaluateProjectQualityAsync_FlagsMissingIndicatorsOnActivities()
        {
            using var db = CreateDbContext();
            var project = new Project { Title = "Projet Sans Indicateurs" };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var foundation = new StrategicFoundation
            {
                ProjectId = project.Id,
                CoreProblemStatement = "Problème Détecté",
                ProblemEvidenceData = "10%",
                EstimatedBudget = 50000m
            };
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Impact", Title = "Impact" });
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Outcome", Title = "Outcome" });
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Activity", Title = "Activity" });
            db.StrategicFoundations.Add(foundation);

            var obj = new Objective { ProjectId = project.Id, Code = "OS1", Description = "Objectif test" };
            db.Objectives.Add(obj);
            await db.SaveChangesAsync();

            var res = new Result { ObjectiveId = obj.Id, Code = "R1", Description = "Résultat test" };
            db.Results.Add(res);
            await db.SaveChangesAsync();

            // Activity with NO indicator
            var act = new Activity { ResultId = res.Id, Code = "A1", Description = "Activité orpheline d'indicateur" };
            db.Activities.Add(act);
            await db.SaveChangesAsync();

            var checker = new DeterministicQualityChecker(() => CreateDbContext());
            var report = await checker.EvaluateProjectQualityAsync(project.Id);

            Assert.Contains(report.Findings, f => f.RuleId == "LOG-03" && f.Severity == FindingSeverity.Warning);
        }

        [Fact]
        public void SanitizeJsonOutput_CleansMarkdownCodeFences()
        {
            string raw = "```json\n{\n  \"title\": \"Projet Santé\",\n  \"budget\": 5000\n}\n```";
            string cleaned = StructuredAiPrompter.SanitizeJsonOutput(raw);

            Assert.Equal("{\n  \"title\": \"Projet Santé\",\n  \"budget\": 5000\n}", cleaned);
            
            using var doc = JsonDocument.Parse(cleaned);
            Assert.Equal("Projet Santé", doc.RootElement.GetProperty("title").GetString());
            Assert.Equal(5000, doc.RootElement.GetProperty("budget").GetInt32());
        }

        [Fact]
        public void SanitizeJsonOutput_HandlesSurroundingChatText()
        {
            string raw = "Voici votre réponse demandée:\n```json\n{\"score\": 95, \"status\": \"Approved\"}\n```\nJ'espère que cela vous aide!";
            string cleaned = StructuredAiPrompter.SanitizeJsonOutput(raw);

            Assert.Equal("{\"score\": 95, \"status\": \"Approved\"}", cleaned);

            using var doc = JsonDocument.Parse(cleaned);
            Assert.Equal(95, doc.RootElement.GetProperty("score").GetInt32());
            Assert.Equal("Approved", doc.RootElement.GetProperty("status").GetString());
        }
    }
}

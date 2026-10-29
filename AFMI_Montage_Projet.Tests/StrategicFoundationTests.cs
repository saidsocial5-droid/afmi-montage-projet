using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services.Strategic;

namespace AFMI_Montage_Projet.Tests
{
    public class StrategicFoundationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public StrategicFoundationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_test_{Guid.NewGuid():N}.db");
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
        public async Task GetOrCreateFoundationAsync_CreatesNewFoundationWithProjectDefaults()
        {
            using var db = CreateDbContext();
            var project = new Project
            {
                Title = "Projet Écologique Oasien",
                InterventionZone = "Ghardaïa",
                TotalBudget = 50000m,
                Currency = "DZD",
                Justification = "Dégradation des palmeraies",
                GeneralObjective = "Préservation de la biodiversité oasienne"
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var service = new StrategicFoundationService(() => CreateDbContext());
            var foundation = await service.GetOrCreateFoundationAsync(project.Id, "test@afmi.dz");

            Assert.NotNull(foundation);
            Assert.Equal(project.Id, foundation.ProjectId);
            Assert.Equal("Projet Écologique Oasien", foundation.IdeaTitle);
            Assert.Equal("Ghardaïa", foundation.TargetZone);
            Assert.Equal(50000m, foundation.EstimatedBudget);
            Assert.Equal("DZD", foundation.Currency);
            Assert.Equal("Dégradation des palmeraies", foundation.CoreProblemStatement);
            Assert.Equal("Préservation de la biodiversité oasienne", foundation.GeneralStrategicObjective);
            Assert.False(foundation.IsDeleted);
        }

        [Fact]
        public void CalculateCompleteness_ScoresCorrectlyBasedOnFilledComponents()
        {
            var sf = new StrategicFoundation
            {
                IdeaTitle = "Mon Idée",
                TargetZone = "Sud",
                EstimatedBudget = 10000m,
                CoreProblemStatement = "Problème d'eau"
            };

            var service = new StrategicFoundationService(() => CreateDbContext());
            int initialScore = service.CalculateCompleteness(sf);
            Assert.True(initialScore >= 25); // 15% card + 10% problem

            sf.StrategicObjectives.Add(new StrategicObjective { Code = "SO1", Title = "Accès à l'eau" });
            sf.Stakeholders.Add(new StakeholderEntry { Name = "Association locale", Role = "Partenaire" });
            sf.Risks.Add(new RiskEntry { Description = "Sécheresse prolongée", MitigationStrategy = "Forage solaire" });

            int newScore = service.CalculateCompleteness(sf);
            Assert.True(newScore > initialScore);
        }

        [Fact]
        public async Task SoftDeleteAndRestore_HidesFromNormalQueriesAndRestoresCleanly()
        {
            using var db = CreateDbContext();
            var project = new Project { Title = "Projet Parent Test" };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var sf = new StrategicFoundation { ProjectId = project.Id, IdeaTitle = "Projet Test" };
            db.StrategicFoundations.Add(sf);
            await db.SaveChangesAsync();

            var stakeholder = new StakeholderEntry
            {
                StrategicFoundationId = sf.Id,
                Name = "Mairie",
                Role = "Autorité locale"
            };
            db.Stakeholders.Add(stakeholder);
            await db.SaveChangesAsync();

            var service = new StrategicFoundationService(() => CreateDbContext());

            // 1. Soft Delete
            bool deleted = await service.SoftDeleteAsync<StakeholderEntry>(stakeholder.Id, "admin@afmi.dz");
            Assert.True(deleted);

            // 2. Normal query must NOT find it (Global Query Filter)
            using (var dbVerify = CreateDbContext())
            {
                var notFound = await dbVerify.Stakeholders.FirstOrDefaultAsync(s => s.Id == stakeholder.Id);
                Assert.Null(notFound);

                // IgnoreQueryFilters MUST find it with IsDeleted = true
                var foundWithFilter = await dbVerify.Stakeholders.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == stakeholder.Id);
                Assert.NotNull(foundWithFilter);
                Assert.True(foundWithFilter.IsDeleted);
                Assert.NotNull(foundWithFilter.DeletedAt);
                Assert.Equal("admin@afmi.dz", foundWithFilter.DeletedBy);
            }

            // 3. Restore
            bool restored = await service.RestoreAsync<StakeholderEntry>(stakeholder.Id, "admin@afmi.dz");
            Assert.True(restored);

            using (var dbRestored = CreateDbContext())
            {
                var foundAgain = await dbRestored.Stakeholders.FirstOrDefaultAsync(s => s.Id == stakeholder.Id);
                Assert.NotNull(foundAgain);
                Assert.False(foundAgain.IsDeleted);
            }
        }

        [Fact]
        public async Task ReadinessGateService_EvaluatesGapsAndAllowsOverrideWithAudit()
        {
            using var db = CreateDbContext();
            var project = new Project { Title = "Projet Test Readiness" };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var gateService = new ReadinessGateService(() => CreateDbContext());

            // 1. Gate for Cadrage before objectives are added should fail
            var eval = await gateService.EvaluateStageAsync(project.Id, LifecycleStage.Cadrage);
            Assert.False(eval.IsPassed);
            Assert.Contains(eval.BlockingGaps, g => g.Contains("الهدف العام") || g.Contains("OS"));

            // 2. Record administrative override with reason
            bool recorded = await gateService.RecordGateOverrideAsync(project.Id, LifecycleStage.Cadrage, "Autorisation dérogatoire pour atelier préparatoire", "directeur@afmi.dz");
            Assert.True(recorded);

            // 3. Re-evaluate: must be passed due to recorded override
            var evalAfterOverride = await gateService.EvaluateStageAsync(project.Id, LifecycleStage.Cadrage);
            Assert.True(evalAfterOverride.IsPassed);
            Assert.True(evalAfterOverride.IsOverridden);
            Assert.Equal("Autorisation dérogatoire pour atelier préparatoire", evalAfterOverride.OverrideReason);
        }

        [Fact]
        public async Task ImpactAnalysisService_DetectsDownstreamItemsAndProtectsOverridden()
        {
            using var db = CreateDbContext();
            var project = new Project { Title = "Impact Test Project" };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var obj = new Objective { ProjectId = project.Id, Code = "OS1", Description = "Amélioration des revenus" };
            db.Objectives.Add(obj);
            await db.SaveChangesAsync();

            var res = new Result { ObjectiveId = obj.Id, Description = "100 agriculteurs formés" };
            db.Results.Add(res);
            await db.SaveChangesAsync();

            // Link 1: Synced (will be affected)
            db.StrategicLinks.Add(new StrategicLink
            {
                ProjectId = project.Id,
                SourceEntityType = "StrategicObjective",
                SourceEntityId = 10,
                TargetEntityType = "Objective",
                TargetEntityId = obj.Id,
                LinkType = "FeedsObjective",
                Status = "Synced"
            });

            // Link 2: Overridden (user manually modified it and protected it)
            db.StrategicLinks.Add(new StrategicLink
            {
                ProjectId = project.Id,
                SourceEntityType = "StrategicObjective",
                SourceEntityId = 10,
                TargetEntityType = "BudgetItem",
                TargetEntityId = 55,
                LinkType = "FeedsBudget",
                Status = "Overridden"
            });
            await db.SaveChangesAsync();

            var impactService = new ImpactAnalysisService(() => CreateDbContext());
            var report = await impactService.AnalyzeImpactAsync(project.Id, "StrategicObjective", 10, "SO 1");

            Assert.Equal(3, report.TotalImpactedCount); // Direct Objective + Cascade Result + Overridden Budget
            Assert.Equal(1, report.ProtectedOverriddenCount);
            Assert.Equal(2, report.StaleToBeMarkedCount);

            // Apply Stale status
            await impactService.ApplyStaleStatusAsync(project.Id, "StrategicObjective", 10, "analyst@afmi.dz");

            using var dbCheck = CreateDbContext();
            var link1 = await dbCheck.StrategicLinks.FirstAsync(l => l.TargetEntityType == "Objective");
            var link2 = await dbCheck.StrategicLinks.FirstAsync(l => l.TargetEntityType == "BudgetItem");

            Assert.Equal("Stale", link1.Status);
            Assert.Equal("Overridden", link2.Status); // Preserved!
        }

        [Fact]
        public void EnsureStrategicFoundationForLegacyProjects_MigratesExistingProjectsWithoutDataLoss()
        {
            using var db = CreateDbContext();
            var oldProject = new Project
            {
                Title = "Vieux Projet 2024",
                InterventionZone = "Biskra",
                TotalBudget = 75000m,
                Currency = "DZD",
                Justification = "Sécheresse historique",
                GeneralObjective = "Renforcement agricole"
            };
            db.Projects.Add(oldProject);
            db.SaveChanges();

            // Run migration method
            db.EnsureStrategicFoundationForLegacyProjects();

            using var dbVerify = CreateDbContext();
            var foundation = dbVerify.StrategicFoundations.FirstOrDefault(sf => sf.ProjectId == oldProject.Id);

            Assert.NotNull(foundation);
            Assert.Equal("Vieux Projet 2024", foundation.IdeaTitle);
            Assert.Equal("Biskra", foundation.TargetZone);
            Assert.Equal(75000m, foundation.EstimatedBudget);
            Assert.Equal("Sécheresse historique", foundation.CoreProblemStatement);
            Assert.Equal("Renforcement agricole", foundation.GeneralStrategicObjective);
            Assert.Equal("Migration", foundation.CreatedBy);
        }
    }
}

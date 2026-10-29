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
    public class PropagationEngineTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public PropagationEngineTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_prop_test_{Guid.NewGuid():N}.db");
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

        private async Task<(Project project, StrategicFoundation foundation)> CreateTestProjectWithFoundationAsync()
        {
            using var db = CreateDbContext();
            var project = new Project
            {
                Title = "Projet Rénovation Agricole",
                InterventionZone = "Biskra",
                TotalBudget = 100000m,
                Currency = "DZD"
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var foundation = new StrategicFoundation
            {
                ProjectId = project.Id,
                IdeaTitle = "Rénovation Agricole",
                GeneralStrategicObjective = "Améliorer les rendements maraîchers durables",
                CoreProblemStatement = "Baisse critique de 40% des rendements due à la salinité",
                EstimatedBudget = 120000m,
                CreatedBy = "expert@afmi.dz"
            };
            foundation.StrategicObjectives.Add(new StrategicObjective
            {
                Code = "OS1",
                Title = "Systèmes d'irrigation",
                Description = "Installer 50 systèmes d'irrigation goutte-à-goutte solaires",
                IsSpecificObjective = true
            });
            foundation.StrategicObjectives.Add(new StrategicObjective
            {
                Code = "OS2",
                Title = "Formation agricole",
                Description = "Former 200 agriculteurs aux techniques d'agriculture biologique",
                IsSpecificObjective = true
            });
            foundation.TheoryOfChangeNodes.Add(new TheoryOfChangeNode
            {
                NodeType = "Outcome",
                Title = "Rendements accrus et économie d'eau de 35%",
                OrderIndex = 1
            });

            db.StrategicFoundations.Add(foundation);
            await db.SaveChangesAsync();

            return (project, foundation);
        }

        [Fact]
        public async Task GenerateProposalsAsync_ProposesObjectivesForCadrage()
        {
            var (project, _) = await CreateTestProjectWithFoundationAsync();
            var service = new PropagationEngineService(() => CreateDbContext());

            var proposals = await service.GenerateProposalsAsync(project.Id, LifecycleStage.Cadrage);

            Assert.NotEmpty(proposals);
            Assert.Contains(proposals, p => p.TargetEntityName == "Project.GeneralObjective");
            Assert.Contains(proposals, p => p.ProposedContent.Contains("irrigation"));
            Assert.Contains(proposals, p => p.ProposedContent.Contains("Former 200 agriculteurs"));
        }

        [Fact]
        public async Task ApplyProposalsAsync_CreatesObjectivesAndStrategicLinks()
        {
            var (project, _) = await CreateTestProjectWithFoundationAsync();
            var service = new PropagationEngineService(() => CreateDbContext());

            var proposals = await service.GenerateProposalsAsync(project.Id, LifecycleStage.Cadrage);
            var summary = await service.ApplyProposalsAsync(project.Id, proposals, "expert@afmi.dz");

            Assert.True(summary.CreatedCount >= 2);

            using var db = CreateDbContext();
            var objectives = await db.Objectives.Where(o => o.ProjectId == project.Id).ToListAsync();
            Assert.True(objectives.Count >= 2);

            var links = await db.StrategicLinks.Where(l => l.ProjectId == project.Id).ToListAsync();
            Assert.True(links.Count >= 2);
            Assert.All(links, l => Assert.Equal("Synced", l.Status));

            // Verify Audit Log
            var audits = await db.AuditLogEntries.Where(a => a.ProjectId == project.Id).ToListAsync();
            Assert.Contains(audits, a => a.ActionType == "PROPAGATION_APPLIED");
        }

        [Fact]
        public async Task ProblemTreePropagation_CreatesCoreNodeWithLink()
        {
            var (project, _) = await CreateTestProjectWithFoundationAsync();
            var service = new PropagationEngineService(() => CreateDbContext());

            var proposals = await service.GenerateProposalsAsync(project.Id, LifecycleStage.ProblemTree);
            Assert.Single(proposals);
            Assert.Equal("CoreProblem", proposals[0].SourceEntityName);

            var summary = await service.ApplyProposalsAsync(project.Id, proposals, "expert@afmi.dz");
            Assert.Equal(1, summary.CreatedCount);

            using var db = CreateDbContext();
            var coreNode = await db.ProblemNodes.FirstOrDefaultAsync(p => p.ProjectId == project.Id && p.NodeType == "Core");
            Assert.NotNull(coreNode);
            Assert.Contains("salinité", coreNode.Description);

            var link = await db.StrategicLinks.FirstOrDefaultAsync(l => l.ProjectId == project.Id && l.TargetEntityType == nameof(ProblemNode));
            Assert.NotNull(link);
            Assert.Equal("Synced", link.Status);
        }

        [Fact]
        public async Task CheckBudgetCeilingAsync_DetectsExceededCeiling()
        {
            var (project, _) = await CreateTestProjectWithFoundationAsync();
            var service = new PropagationEngineService(() => CreateDbContext());

            using (var db = CreateDbContext())
            {
                var cat = db.BudgetCategories.First();
                // Add budget items that exceed 120,000 ceiling (e.g. 150,000)
                db.BudgetItems.Add(new BudgetItem
                {
                    ProjectId = project.Id,
                    CategoryId = cat.Id,
                    Description = "Équipements solaires lourds",
                    Quantity = 3,
                    UnitCost = 50000m,
                    Frequency = 1
                });
                await db.SaveChangesAsync();
            }

            var (hasWarning, ceiling, total, message) = await service.CheckBudgetCeilingAsync(project.Id);

            Assert.True(hasWarning);
            Assert.Equal(120000m, ceiling);
            Assert.Equal(150000m, total);
            Assert.Contains("dépasse le plafond", message);
            Assert.Contains("DZD", message); // devise du projet de test
        }

        [Fact]
        public async Task CheckBudgetCeilingAsync_PassesWhenWithinCeiling()
        {
            var (project, _) = await CreateTestProjectWithFoundationAsync();
            var service = new PropagationEngineService(() => CreateDbContext());

            using (var db = CreateDbContext())
            {
                var cat = db.BudgetCategories.First();
                db.BudgetItems.Add(new BudgetItem
                {
                    ProjectId = project.Id,
                    CategoryId = cat.Id,
                    Description = "Outils légers",
                    Quantity = 2,
                    UnitCost = 20000m,
                    Frequency = 1
                });
                await db.SaveChangesAsync();
            }

            var (hasWarning, ceiling, total, message) = await service.CheckBudgetCeilingAsync(project.Id);

            Assert.False(hasWarning);
            Assert.Equal(40000m, total);
            Assert.Contains("respecte le plafond", message);
        }

        [Fact]
        public async Task CadreLogiquePropagation_CreatesResultsAndActivities_AndDoesNotDuplicate()
        {
            var (project, foundation) = await CreateTestProjectWithFoundationAsync();
            using (var db = CreateDbContext())
            {
                var f = db.StrategicFoundations.Include(x => x.TheoryOfChangeNodes).First(x => x.Id == foundation.Id);
                var outcome = f.TheoryOfChangeNodes.First(n => n.NodeType == "Outcome");
                f.TheoryOfChangeNodes.Add(new TheoryOfChangeNode { NodeType = "Activity", Title = "Installer les kits d'irrigation", OrderIndex = 2, ParentNodeId = outcome.Id });
                await db.SaveChangesAsync();
            }
            var service = new PropagationEngineService(() => CreateDbContext());

            // Les objectifs doivent exister avant les résultats
            await service.ApplyProposalsAsync(project.Id, await service.GenerateProposalsAsync(project.Id, LifecycleStage.Cadrage), "expert@afmi.dz");

            // Une seule passe : le résultat (effet ToC) puis l'activité ToC enfant, rattachée à ce résultat
            var summary = await service.ApplyProposalsAsync(project.Id, await service.GenerateProposalsAsync(project.Id, LifecycleStage.CadreLogique), "expert@afmi.dz");
            Assert.Equal(2, summary.CreatedCount);

            using (var db = CreateDbContext())
            {
                var result = db.Results.Single(r => r.Description.Contains("Rendements accrus"));
                Assert.Equal("R1.1", result.Code);
                var activity = db.Activities.Single(a => a.Description == "Installer les kits d'irrigation");
                Assert.Equal(result.Id, activity.ResultId);
                Assert.Equal("A1.1.1", activity.Code);
            }

            // Une fois aligné, plus aucune proposition (pas de doublon)
            Assert.Empty(await service.GenerateProposalsAsync(project.Id, LifecycleStage.CadreLogique));
        }

        [Fact]
        public async Task RefreshLinkStatuses_MarksStaleOnSourceChange_AndOverriddenOnManualEdit()
        {
            var (project, foundation) = await CreateTestProjectWithFoundationAsync();
            var service = new PropagationEngineService(() => CreateDbContext());
            await service.ApplyProposalsAsync(project.Id, await service.GenerateProposalsAsync(project.Id, LifecycleStage.Cadrage), "expert@afmi.dz");

            var initial = await service.RefreshLinkStatusesAsync(project.Id);
            Assert.Equal(0, initial.ChangedCount);
            Assert.Equal(2, initial.SyncedCount);

            using (var db = CreateDbContext())
            {
                // OS1 : la source stratégique change → Stale
                var so1 = db.StrategicObjectives.First(o => o.StrategicFoundationId == foundation.Id && o.Code == "OS1");
                so1.Description = "Installer 80 systèmes d'irrigation solaires";
                // OS2 : l'utilisateur réécrit l'objectif du cadre logique → Overridden
                int so2Id = db.StrategicObjectives.First(o => o.Code == "OS2" && o.StrategicFoundationId == foundation.Id).Id;
                var os2Link = db.StrategicLinks.First(l => l.ProjectId == project.Id && l.SourceEntityId == so2Id);
                db.Objectives.First(o => o.Id == os2Link.TargetEntityId).Description = "Formulation retravaillée par l'équipe";
                await db.SaveChangesAsync();
            }

            var refreshed = await service.RefreshLinkStatusesAsync(project.Id);
            Assert.Equal(1, refreshed.StaleCount);
            Assert.Equal(1, refreshed.OverriddenCount);

            // L'objectif protégé est proposé mais désélectionné par défaut ; le Stale est proposé et sélectionné
            var proposals = await service.GenerateProposalsAsync(project.Id, LifecycleStage.Cadrage);
            Assert.Contains(proposals, p => p.ProposedContent.Contains("80 systèmes") && p.IsSelected && p.ActionType == "Update");
            Assert.Contains(proposals, p => p.CurrentContent == "Formulation retravaillée par l'équipe" && !p.IsSelected);

            // La porte de maturité signale les éléments à revoir
            var gate = await new ReadinessGateService(() => CreateDbContext()).EvaluateStageAsync(project.Id, LifecycleStage.Cadrage);
            Assert.Equal(StageStatus.NeedsReview, gate.Status);
        }
    }
}

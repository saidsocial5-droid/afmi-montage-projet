using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;
using AFMI_Montage_projet_2.ViewModels.Radar;

namespace AFMI_Montage_Projet.Tests
{
    public class DateAndFormulationTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public DateAndFormulationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_date_test_{Guid.NewGuid():N}.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var db = new AppDbContext(_dbOptions);
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
            }
            catch { }
        }

        private AppDbContext CreateDbContext() => new AppDbContext(_dbOptions);

        [Fact]
        public void RssParser_DateParsing_HandlesDiverseRfcIsoAndCultureFormats()
        {
            // 1. Single digit day RFC 822 with timezone offset
            var d1 = RssParser.ParseDate("1 May 2025 10:00:00 +0000");
            Assert.NotNull(d1);
            Assert.Equal(2025, d1.Value.Year);
            Assert.Equal(5, d1.Value.Month);
            Assert.Equal(1, d1.Value.Day);

            // 2. Standard RFC 1123 / 822
            var d2 = RssParser.ParseDate("Mon, 15 Jun 2026 12:00:00 GMT");
            Assert.NotNull(d2);
            Assert.Equal(2026, d2.Value.Year);
            Assert.Equal(6, d2.Value.Month);
            Assert.Equal(15, d2.Value.Day);

            // 3. ISO 8601 UTC
            var d3 = RssParser.ParseDate("2026-08-20T14:30:00Z");
            Assert.NotNull(d3);
            Assert.Equal(2026, d3.Value.Year);
            Assert.Equal(8, d3.Value.Month);
            Assert.Equal(20, d3.Value.Day);

            // 4. Numeric formats
            var d4 = RssParser.ParseDate("25/11/2026");
            Assert.NotNull(d4);
            Assert.Equal(2026, d4.Value.Year);
            Assert.Equal(11, d4.Value.Month);
            Assert.Equal(25, d4.Value.Day);

            // 5. French textual date
            var d5 = RssParser.ParseDate("19 octobre 2026");
            Assert.NotNull(d5);
            Assert.Equal(2026, d5.Value.Year);
            Assert.Equal(10, d5.Value.Month);
            Assert.Equal(19, d5.Value.Day);

            // 6. Null or whitespace safely returns null
            Assert.Null(RssParser.ParseDate(null));
            Assert.Null(RssParser.ParseDate("   "));
        }

        [Fact]
        public void OpportunityItemViewModel_ExpirationAndRemainingDays_CalculatedAccurately()
        {
            var now = DateTime.UtcNow.Date;

            // Opportunité expirée (il y a 5 jours)
            var expiredItem = new RssItemCache
            {
                ExternalId = "exp-1",
                Title = "Appel de subvention clos",
                AiDeadline = now.AddDays(-5)
            };
            var expiredVm = new OpportunityItemViewModel(expiredItem);
            Assert.True(expiredVm.IsExpired);
            Assert.False(expiredVm.IsUrgentDeadline);
            Assert.Equal("#DC2626", expiredVm.DeadlineStatusColor); // Rouge
            Assert.Equal("#FEE2E2", expiredVm.DeadlineStatusBadgeBackground);
            Assert.Contains("Expiré le", expiredVm.DeadlineStatusText);
            Assert.Contains("منتهية الصلاحية", expiredVm.DeadlineStatusTextAr);

            // Opportunité imminente (dans 6 jours <= 15)
            var urgentItem = new RssItemCache
            {
                ExternalId = "urg-1",
                Title = "Appel urgent à échéance courte",
                AiDeadline = now.AddDays(6)
            };
            var urgentVm = new OpportunityItemViewModel(urgentItem);
            Assert.False(urgentVm.IsExpired);
            Assert.True(urgentVm.IsUrgentDeadline);
            Assert.Equal("#D97706", urgentVm.DeadlineStatusColor); // Ambre
            Assert.Equal("#FEF3C7", urgentVm.DeadlineStatusBadgeBackground);
            Assert.Contains("Échéance dans 6 j", urgentVm.DeadlineStatusText);
            Assert.Contains("وشيك الإغلاق", urgentVm.DeadlineStatusTextAr);

            // Opportunité active (dans 50 jours)
            var activeItem = new RssItemCache
            {
                ExternalId = "act-1",
                Title = "Appel vert durable",
                AiDeadline = now.AddDays(50)
            };
            var activeVm = new OpportunityItemViewModel(activeItem);
            Assert.False(activeVm.IsExpired);
            Assert.False(activeVm.IsUrgentDeadline);
            Assert.Equal("#059669", activeVm.DeadlineStatusColor); // Vert
            Assert.Equal("#ECFDF5", activeVm.DeadlineStatusBadgeBackground);
            Assert.Contains("Ouvert jusqu'au", activeVm.DeadlineStatusText);
            Assert.Contains("جارية حتى", activeVm.DeadlineStatusTextAr);
        }

        [Fact]
        public async Task ProjectRadarViewModel_DeadlineAndPublishDateFilters_FilterAccurately()
        {
            var now = DateTime.UtcNow.Date;
            var rssService = new RssFeedService(dbContextFactory: () => CreateDbContext());
            var pipelineService = new PipelineService(dbContextFactory: () => CreateDbContext());
            var vm = new ProjectRadarViewModel(rssService: rssService, pipelineService: pipelineService);
            await vm.LoadFeedsAndCacheAsync();
            vm.AllOpportunities.Clear();

            // 1. Opportunité récente & active
            vm.AllOpportunities.Add(new OpportunityItemViewModel(new RssItemCache
            {
                ExternalId = "recent-active",
                Title = "Fonds Forêt Cèdre",
                PublishDate = now.AddDays(-2),
                AiDeadline = now.AddDays(40)
            }));

            // 2. Opportunité imminente (< 30 jours)
            vm.AllOpportunities.Add(new OpportunityItemViewModel(new RssItemCache
            {
                ExternalId = "recent-imminent",
                Title = "Fonds Eau Climat",
                PublishDate = now.AddDays(-10),
                AiDeadline = now.AddDays(12)
            }));

            // 3. Opportunité expirée
            vm.AllOpportunities.Add(new OpportunityItemViewModel(new RssItemCache
            {
                ExternalId = "old-expired",
                Title = "Fonds Clôturé 2025",
                PublishDate = now.AddMonths(-6),
                AiDeadline = now.AddDays(-20)
            }));

            vm.ApplyFilters();

            // Test A : Par défaut "En cours (non expirées)" -> 2 items (recent-active, recent-imminent)
            Assert.Equal("En cours (non expirées)", vm.SelectedDeadlineFilter);
            Assert.Equal(2, vm.FilteredOpportunities.Count);
            Assert.DoesNotContain(vm.FilteredOpportunities, o => o.ExternalId == "old-expired");

            // Test B : "Expirées uniquement" -> 1 item (old-expired)
            vm.SelectedDeadlineFilter = "Expirées uniquement";
            Assert.Single(vm.FilteredOpportunities);
            Assert.Equal("old-expired", vm.FilteredOpportunities[0].ExternalId);

            // Test C : "Échéance proche (< 30 jours)" -> 1 item (recent-imminent)
            vm.SelectedDeadlineFilter = "Échéance proche (< 30 jours)";
            Assert.Single(vm.FilteredOpportunities);
            Assert.Equal("recent-imminent", vm.FilteredOpportunities[0].ExternalId);

            // Test D : Filtre date de publication "Derniers 7 jours"
            vm.SelectedDeadlineFilter = "Toutes (y compris expirées)";
            vm.SelectedPublishDateFilter = "Derniers 7 jours";
            Assert.Single(vm.FilteredOpportunities);
            Assert.Equal("recent-active", vm.FilteredOpportunities[0].ExternalId);

            // Test E : ClearFilters remet à "En cours (non expirées)"
            vm.ClearFilters();
            Assert.Equal("En cours (non expirées)", vm.SelectedDeadlineFilter);
            Assert.Equal(2, vm.FilteredOpportunities.Count);
        }

        [Fact]
        public async Task AiProjectFormulationService_CreatesCompleteProjectWithAllLifecycleEntities()
        {
            var service = new AiProjectFormulationService(() => CreateDbContext());

            var item = new RssItemCache
            {
                ExternalId = "grant-biodiv-2026",
                Title = "Appel à projets : Conservation de la biodiversité du Moyen Atlas",
                CleanDescription = "Financement d'initiatives communautaires pour la sauvegarde des écosystèmes forestiers et de la cédraie.",
                Donor = "GEF",
                Link = "https://www.thegef.org/call-biodiv-moyen-atlas",
                AiEstimatedBudget = 100000,
                AiCurrency = "USD",
                AiDeadline = DateTime.UtcNow.AddMonths(4),
                AiMatchedAxes = "Forêt et Cèdre, Eau"
            };

            // Formuler le projet
            var project = await service.FormulateProjectFromOpportunityAsync(item);

            Assert.NotNull(project);
            Assert.True(project.Id > 0);
            Assert.Contains("Moyen Atlas", project.Title);
            Assert.Equal("Association Forêt Modèle Ifrane (AFMI)", project.LeadOrganization);
            Assert.Equal(100000m, project.TotalBudget);
            Assert.Equal("Province d'Ifrane / Région Fès-Meknès (Moyen Atlas)", project.InterventionZone);

            using var db = CreateDbContext();

            // 1. Vérifier l'Arbre de problèmes
            var problemNodes = await db.ProblemNodes.Where(p => p.ProjectId == project.Id).ToListAsync();
            Assert.True(problemNodes.Count >= 5); // 1 Core + au moins 2 Causes + au moins 2 Conséquences
            Assert.Contains(problemNodes, p => p.NodeType == "Core");
            Assert.Contains(problemNodes, p => p.NodeType == "Cause");
            Assert.Contains(problemNodes, p => p.NodeType == "Consequence");

            // 2. Vérifier les Objectifs spécifiques, Résultats, Activités
            var objectives = await db.Objectives.Where(o => o.ProjectId == project.Id).ToListAsync();
            Assert.True(objectives.Count >= 2);

            var results = await db.Results.Where(r => objectives.Select(o => o.Id).Contains(r.ObjectiveId)).ToListAsync();
            Assert.True(results.Count >= 2);

            var activities = await db.Activities.Where(a => results.Select(r => r.Id).Contains(a.ResultId)).ToListAsync();
            Assert.True(activities.Count >= 3);

            // 3. Vérifier les Indicateurs SMART
            var indicators = await db.Indicators.Where(i => i.ProjectId == project.Id).ToListAsync();
            Assert.NotEmpty(indicators);
            Assert.All(indicators, ind =>
            {
                Assert.False(string.IsNullOrWhiteSpace(ind.Description));
                Assert.False(string.IsNullOrWhiteSpace(ind.Target));
                Assert.False(string.IsNullOrWhiteSpace(ind.VerificationSource));
            });

            // 4. Vérifier les Risques
            var risks = await db.Risks.Where(r => r.ProjectId == project.Id).ToListAsync();
            Assert.True(risks.Count >= 2);
            Assert.All(risks, r =>
            {
                Assert.InRange(r.Probability, 1, 5);
                Assert.InRange(r.Impact, 1, 5);
                Assert.False(string.IsNullOrWhiteSpace(r.MitigationMeasure));
            });

            // 5. Vérifier le Budget Prévisionnel
            var budgetItems = await db.BudgetItems.Where(b => b.ProjectId == project.Id).ToListAsync();
            Assert.True(budgetItems.Count >= 4);
            var categories = await db.BudgetCategories.ToListAsync();
            Assert.True(categories.Count >= 4);

            decimal totalRequested = budgetItems.Sum(b => b.FundingRequested);
            Assert.Equal(100000m, totalRequested);

            // 6. Vérifier l'enregistrement dans la table grants_pipeline
            var pipelineEntry = await db.GrantsPipeline.FirstOrDefaultAsync(g => g.ExternalId == item.ExternalId);
            Assert.NotNull(pipelineEntry);
            Assert.Equal("En rédaction", pipelineEntry.PipelineStatus);
            Assert.Contains(project.Id.ToString(), pipelineEntry.Notes);
        }
    }
}

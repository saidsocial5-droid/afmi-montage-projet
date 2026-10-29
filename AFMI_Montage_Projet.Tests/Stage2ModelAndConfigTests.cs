using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_Projet.Tests
{
    public class Stage2ModelAndConfigTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public Stage2ModelAndConfigTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_s2_test_{Guid.NewGuid():N}.db");
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

        [Fact]
        public void GrantsPipeline_AddAndRetrieveOpportunity_PersistsAllFields()
        {
            using var db = CreateDbContext();
            var opp = new GrantOpportunity
            {
                ExternalId = "test-guid-12345",
                Title = "Appel à Projets Biodiversité Moyen Atlas 2026",
                Donor = "GEF SGP",
                OriginalUrl = "https://www.thegef.org/grants/2026-moyen-atlas",
                EstimatedBudget = 75000.50m,
                Currency = "USD",
                Deadline = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                SummaryAr = "دعم صغار الفلاحين والتعاونيات الغابوية لغرس أشجار الأرز",
                SummaryFr = "Appui aux coopératives sylvopastorales pour la régénération du cèdre",
                RelevanceScore = 9.2,
                MatchedAxes = "Axe 1, Axe 4",
                Rationale = "Alignement parfait avec la mission AFMI et zone géographique couverte.",
                PipelineStatus = "En cours d'étude",
                AddedAt = DateTime.UtcNow,
                Notes = "Dossier à monter avec la coopérative locale"
            };

            db.GrantsPipeline.Add(opp);
            db.SaveChanges();

            Assert.True(opp.Id > 0);

            var retrieved = db.GrantsPipeline.FirstOrDefault(g => g.ExternalId == "test-guid-12345");
            Assert.NotNull(retrieved);
            Assert.Equal("GEF SGP", retrieved!.Donor);
            Assert.Equal(75000.50m, retrieved.EstimatedBudget);
            Assert.Equal("USD", retrieved.Currency);
            Assert.Equal(9.2, retrieved.RelevanceScore);
            Assert.Contains("الأرز", retrieved.SummaryAr);
            Assert.Contains("cèdre", retrieved.SummaryFr);
            Assert.Equal("En cours d'étude", retrieved.PipelineStatus);
        }

        [Fact]
        public void RssItemsCache_AddAndRetrieveCache_Succeeds()
        {
            using var db = CreateDbContext();
            var item = new RssItemCache
            {
                FeedId = "feed-eu-greendeal",
                FeedName = "Union Européenne",
                Donor = "UE",
                ExternalId = "item-eu-999",
                Title = "Appel Green Deal Climat & Forêts",
                CleanDescription = "Financement d'initiatives de restauration forestière",
                Link = "https://ec.europa.eu/calls/climat-foret",
                PublishDate = DateTime.UtcNow.AddDays(-2),
                FetchedAt = DateTime.UtcNow,
                IsRead = false,
                IsHidden = false,
                IsInPipeline = false,
                AiScore = 8.5,
                AiScoreRationale = "Forte pertinence thématique",
                AiMatchedAxes = "Axe 1",
                IsScored = true
            };

            db.RssItemsCache.Add(item);
            db.SaveChanges();

            Assert.True(item.Id > 0);

            var retrieved = db.RssItemsCache.FirstOrDefault(r => r.ExternalId == "item-eu-999");
            Assert.NotNull(retrieved);
            Assert.Equal("Union Européenne", retrieved!.FeedName);
            Assert.Equal(8.5, retrieved.AiScore);
            Assert.True(retrieved.IsScored);
        }

        [Fact]
        public void ProjectRadarConfigService_LoadsDefaultFeeds_HasEightInstitutionalDonors()
        {
            var feeds = ProjectRadarConfigService.LoadFeeds();
            Assert.NotNull(feeds);
            Assert.True(feeds.Count >= 8);

            Assert.Contains(feeds, f => f.Id == "feed-gef-sgp");
            Assert.Contains(feeds, f => f.Id == "feed-undp-morocco");
            Assert.Contains(feeds, f => f.Id == "feed-eu-greendeal");
            Assert.Contains(feeds, f => f.Id == "feed-worldbank-climate");
            Assert.Contains(feeds, f => f.Id == "feed-afd-ffem");
            Assert.Contains(feeds, f => f.Id == "feed-iucn-med");
            Assert.Contains(feeds, f => f.Id == "feed-fao-forestry");
            Assert.Contains(feeds, f => f.Id == "feed-cepf-med");

            var euFeed = feeds.First(f => f.Id == "feed-eu-greendeal");
            Assert.True(euFeed.Verified);
            Assert.True(euFeed.IsDefault);
        }

        [Fact]
        public void ProjectRadarConfigService_LoadsFiveOfficialAfmiAxes()
        {
            var axes = ProjectRadarConfigService.LoadAxes();
            Assert.NotNull(axes);
            Assert.Equal(5, axes.Count);

            foreach (var axis in axes)
            {
                Assert.False(string.IsNullOrWhiteSpace(axis.Title));
                Assert.False(string.IsNullOrWhiteSpace(axis.Description));
                Assert.True(axis.IsCompleted);
            }

            Assert.Contains(axes, a => a.Id == 1 && a.Title.Contains("gouvernance", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 2 && a.Title.Contains("ressources naturelles", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 3 && a.Title.Contains("énergie", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 4 && a.Title.Contains("revenus", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 5 && a.Title.Contains("communication", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ProjectRadarConfigService_LoadsAfmiProfile_HasRequiredThemesAndMission()
        {
            var profile = ProjectRadarConfigService.LoadProfile();
            Assert.NotNull(profile);
            Assert.Equal("Association Forêt Modèle Ifrane (AFMI)", profile.OrganizationName);
            Assert.Contains("Moyen Atlas", profile.Mission);
            Assert.Contains(profile.GeographicZones, z => z.Contains("Ifrane"));
            Assert.Contains(profile.PreferredThemes, t => t.Contains("Cédraies"));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>Base de veille (lot 1, ADR 28) : axes A1-A10, notation /25, pré-filtrage, doublons, échéances.</summary>
    public class VeilleBaseTests : IDisposable
    {
        private static readonly DateTime Today = new(2026, 11, 10);

        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public VeilleBaseTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_veille_test_{Guid.NewGuid():N}.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var db = new AppDbContext(_dbOptions);
            db.Database.EnsureCreated();
            db.SeedDefaultData();
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            }
            catch (IOException) { }
        }

        private AppDbContext CreateDbContext() => new AppDbContext(_dbOptions);
        private VeilleBaseService CreateService() => new VeilleBaseService(CreateDbContext);

        private static VeilleCandidate Candidate(string title, string summary, DateTime? deadline = null, string donor = "CEPF") => new()
        {
            Title = title,
            Donor = donor,
            Summary = summary,
            Deadline = deadline,
            Link = $"https://example.org/{Guid.NewGuid():N}",
            DetectionSource = "RSS — test"
        };

        #region Axes et normalisation

        [Fact]
        public void DetectAxes_UsesWordBoundaries_AndArabicKeywords()
        {
            Assert.DoesNotContain("A3", VeilleAxes.DetectAxes("Renforcement du réseau des associations"));
            Assert.DoesNotContain("A6", VeilleAxes.DetectAxes("Appui à l'agriculture de montagne"));
            Assert.Contains("A2", VeilleAxes.DetectAxes("Appui à l'agriculture de montagne"));
            Assert.Contains("A3", VeilleAxes.DetectAxes("Gestion des eaux et des sols"));
            Assert.Contains("A1", VeilleAxes.DetectAxes("مشروع حماية الغابات"));
            Assert.Empty(VeilleAxes.DetectAxes("Marché de fournitures de bureau"));
        }

        [Fact]
        public void NormalizeTitle_IgnoresCaseAccentsYearsAndPunctuation()
        {
            Assert.Equal(VeilleText.NormalizeTitle("Appel à projets « Biodiversité » 2026"),
                         VeilleText.NormalizeTitle("APPEL A PROJETS - biodiversite (2027)"));
        }

        #endregion

        #region Notation /25

        [Fact]
        public void Score_Pertinence_FollowsAxisCountAndCoreMission()
        {
            Assert.Equal(5, VeilleScoringService.Score(new VeilleCallInfo { Title = "Restauration de la cédraie et des Agdals" }, Today).Pertinence);
            Assert.Equal(3, VeilleScoringService.Score(new VeilleCallInfo { Title = "Reboisement et gestion de l'eau" }, Today).Pertinence);
            Assert.Equal(1, VeilleScoringService.Score(new VeilleCallInfo { Title = "Programme de reboisement" }, Today).Pertinence);
            Assert.Equal(0, VeilleScoringService.Score(new VeilleCallInfo { Title = "Marché de fournitures de bureau" }, Today).Pertinence);
        }

        [Theory]
        [InlineData(20, "A")]
        [InlineData(19, "B")]
        [InlineData(14, "B")]
        [InlineData(13, "C")]
        public void PriorityOf_UsesMethodThresholds(int total, string expected)
        {
            Assert.Equal(expected, VeilleScoringService.PriorityOf(total));
        }

        [Fact]
        public void Recompute_CapsUnconfirmedCallsAt14_AndClampsNotes()
        {
            var opp = new GrantOpportunity
            {
                ScorePertinence = 7, ScoreEligibilite = 5, ScoreFinancier = 5, ScoreFaisabilite = 5, ScoreStrategique = 5,
                Confirmation = VeilleConfirmation.NonConfirme
            };
            VeilleScoringService.Recompute(opp);
            Assert.Equal(5, opp.ScorePertinence);
            Assert.Equal(14, opp.ScoreTotal);
            Assert.Equal("B", opp.Priority);

            opp.Confirmation = VeilleConfirmation.Confirme;
            VeilleScoringService.Recompute(opp);
            Assert.Equal(25, opp.ScoreTotal);
            Assert.Equal("A", opp.Priority);
        }

        [Fact]
        public void Score_LargeAmount_IsNeverPenalized_AndSuggestsConsortium()
        {
            var small = VeilleScoringService.Score(new VeilleCallInfo { Title = "Reboisement", Amount = 50_000m, Currency = "EUR" }, Today);
            var large = VeilleScoringService.Score(new VeilleCallInfo { Title = "Reboisement", Amount = 5_000_000m, Currency = "EUR" }, Today);

            Assert.True(large.Financier >= small.Financier);
            Assert.Empty(small.ConsortiumAdvice);
            Assert.StartsWith("Consortium conseillé avec", large.ConsortiumAdvice);
        }

        [Fact]
        public void Score_IndividualOnlyCall_IsKeptWithMention()
        {
            var card = VeilleScoringService.Score(new VeilleCallInfo
            {
                Title = "Subventions de récit sur le patrimoine",
                Eligibility = "Ouvert aux personnes physiques de plus de 18 ans"
            }, Today);
            Assert.True(card.IsIndividualOnly);
            Assert.Equal(1, card.Eligibilite);

            var opp = new GrantOpportunity();
            VeilleScoringService.Apply(card, opp);
            Assert.Contains(VeilleScoringService.IndividualMention, opp.Notes);
            Assert.Equal("C", opp.Priority);
        }

        [Fact]
        public void Score_Feasibility_DependsOnRemainingWeeksAndDocuments()
        {
            int Feas(int days, bool heavy, bool hasDeadline = true) => VeilleScoringService.Score(new VeilleCallInfo
            {
                Title = "Reboisement",
                Deadline = hasDeadline ? Today.AddDays(days) : null,
                HeavyDocuments = heavy
            }, Today).Faisabilite;

            Assert.Equal(5, Feas(60, heavy: false));
            Assert.Equal(3, Feas(60, heavy: true));
            Assert.Equal(3, Feas(28, heavy: false));
            Assert.Equal(1, Feas(14, heavy: true));
            Assert.Equal(3, Feas(0, heavy: false, hasDeadline: false));
        }

        #endregion

        #region Pré-filtrage

        [Fact]
        public void PreFilter_RejectsWithReason()
        {
            var zone = VeilleBaseService.PreFilter(Candidate("Biodiversity grants", "Open to EU member states only."), Today);
            Assert.False(zone.Accepted);
            Assert.Equal("zone excluant le Maroc", zone.Reason);

            var zoneWithMorocco = VeilleBaseService.PreFilter(Candidate("Biodiversity grants", "EU member states only, and Morocco as partner country."), Today);
            Assert.True(zoneWithMorocco.Accepted);

            Assert.Equal("hors axes", VeilleBaseService.PreFilter(Candidate("Marché de fournitures de bureau", ""), Today).Reason);
            Assert.Equal("associations non éligibles",
                VeilleBaseService.PreFilter(Candidate("Climate innovation challenge", "For start-ups only."), Today).Reason);

            var closed = VeilleBaseService.PreFilter(Candidate("Annual forest grants", "Annual call.", Today.AddDays(-3)), Today);
            Assert.False(closed.Accepted);
            Assert.Equal("clos le 07/11/2026", closed.Reason);
            Assert.True(closed.FollowNextCycle);
        }

        #endregion

        #region Base permanente

        [Fact]
        public async Task ProcessCandidate_AssignsIds_AndNeverCreatesDuplicates()
        {
            var service = CreateService();

            var first = await service.ProcessCandidateAsync(Candidate("Appel à projets Biodiversité du Moyen Atlas 2026", "Associations éligibles."), Today);
            Assert.Equal(VeilleOutcomeKind.Added, first.Kind);
            Assert.Equal("V-2026-001", first.Opportunity!.VeilleId);
            Assert.Equal(VeilleStatus.Nouveau, first.Opportunity.PipelineStatus);
            Assert.True(first.Opportunity.ScoreTotal <= VeilleScoringService.UnconfirmedCap);

            var second = await service.ProcessCandidateAsync(Candidate("Appel a projets biodiversite du Moyen Atlas", "Autre agrégateur.", Today.AddDays(60)), Today);
            Assert.Equal(VeilleOutcomeKind.Duplicate, second.Kind);
            Assert.Equal(VeilleTable.Appels, second.DuplicateOf);

            var third = await service.ProcessCandidateAsync(Candidate("Programme de reboisement communautaire", "ONG locales."), Today);
            Assert.Equal("V-2026-002", third.Opportunity!.VeilleId);

            using var db = CreateDbContext();
            Assert.Equal(2, await db.GrantsPipeline.CountAsync());
            var completed = await db.GrantsPipeline.SingleAsync(g => g.VeilleId == "V-2026-001");
            Assert.Equal(Today.AddDays(60), completed.Deadline);
            Assert.Contains("[10/11/2026] Complété via RSS — test : date limite", completed.Notes);
        }

        [Fact]
        public async Task ProcessCandidate_RejectedCalls_AreRememberedAndRecurrentOnesFollowed()
        {
            var service = CreateService();

            var rejected = await service.ProcessCandidateAsync(Candidate("Marché de fournitures de bureau", ""), Today);
            Assert.Equal(VeilleOutcomeKind.Rejected, rejected.Kind);

            var again = await service.ProcessCandidateAsync(Candidate("Marché de fournitures de bureau", ""), Today.AddDays(1));
            Assert.Equal(VeilleOutcomeKind.Duplicate, again.Kind);
            Assert.Equal(VeilleTable.NonRetenus, again.DuplicateOf);

            var recurrent = await service.ProcessCandidateAsync(Candidate("Prix annuel de la forêt", "Édition annuelle.", Today.AddDays(-1)), Today);
            Assert.Equal(VeilleOutcomeKind.Rejected, recurrent.Kind);

            using var db = CreateDbContext();
            Assert.Equal(2, await db.RejectedCalls.CountAsync());
            Assert.All(await db.RejectedCalls.ToListAsync(), r => Assert.False(string.IsNullOrWhiteSpace(r.Reason)));
            var upcoming = await db.UpcomingCalls.SingleAsync();
            Assert.Equal("AV-001", upcoming.VeilleId);
            Assert.Equal(0, await db.GrantsPipeline.CountAsync());
        }

        [Fact]
        public async Task RunDeadlineCheck_ClosesAndFlagsTooLate_WithDatedNotes()
        {
            using (var db = CreateDbContext())
            {
                GrantOpportunity Opp(string id, string status, int deadlineInDays, string confirmation = VeilleConfirmation.Confirme, int addedDaysAgo = 1) => new()
                {
                    VeilleId = id, ExternalId = id, Title = id, Donor = "Test", OriginalUrl = "https://example.org/" + id,
                    PipelineStatus = status, Deadline = Today.AddDays(deadlineInDays), Confirmation = confirmation,
                    AddedAt = Today.AddDays(-addedDaysAgo)
                };
                db.GrantsPipeline.AddRange(
                    Opp("past", VeilleStatus.EnCours, -1),
                    Opp("soon", VeilleStatus.Nouveau, 5),
                    Opp("soon-engaged", VeilleStatus.EnCours, 5),
                    Opp("submitted", VeilleStatus.Soumis, -5),
                    Opp("unconfirmed", VeilleStatus.Nouveau, 12, VeilleConfirmation.NonConfirme, addedDaysAgo: 8),
                    Opp("fine", VeilleStatus.Nouveau, 40, VeilleConfirmation.NonConfirme, addedDaysAgo: 8));
                await db.SaveChangesAsync();
            }

            var changes = await CreateService().RunDeadlineCheckAsync(Today);

            Assert.Equal(new[] { "past", "soon", "unconfirmed" }, changes.Select(c => c.VeilleId).OrderBy(x => x).ToArray());

            using var check = CreateDbContext();
            string StatusOf(string id) => check.GrantsPipeline.Single(g => g.VeilleId == id).PipelineStatus;
            Assert.Equal(VeilleStatus.Clos, StatusOf("past"));
            Assert.Equal(VeilleStatus.TropTard, StatusOf("soon"));
            Assert.Equal(VeilleStatus.EnCours, StatusOf("soon-engaged"));
            Assert.Equal(VeilleStatus.Soumis, StatusOf("submitted"));
            Assert.Equal(VeilleStatus.TropTard, StatusOf("unconfirmed"));
            Assert.Equal(VeilleStatus.Nouveau, StatusOf("fine"));
            Assert.Contains("[10/11/2026] Contrôle des échéances", check.GrantsPipeline.Single(g => g.VeilleId == "past").Notes);
            Assert.Equal(6, check.GrantsPipeline.Count());
        }

        [Fact]
        public async Task AxisCoverage_CountsActiveCallsByPriority_AndFlagsLeastCoveredAxis()
        {
            using (var db = CreateDbContext())
            {
                db.GrantsPipeline.AddRange(
                    new GrantOpportunity { VeilleId = "V-2026-001", ExternalId = "1", Title = "a", Donor = "d", OriginalUrl = "u1", VeilleAxes = "A1, A2", Priority = "A" },
                    new GrantOpportunity { VeilleId = "V-2026-002", ExternalId = "2", Title = "b", Donor = "d", OriginalUrl = "u2", VeilleAxes = "A1", Priority = "C" },
                    new GrantOpportunity { VeilleId = "V-2026-003", ExternalId = "3", Title = "c", Donor = "d", OriginalUrl = "u3", VeilleAxes = "A6", Priority = "B", PipelineStatus = VeilleStatus.Clos });
                await db.SaveChangesAsync();
            }

            var coverage = await CreateService().ComputeAxisCoverageAsync();

            var a1 = coverage.Single(c => c.Code == "A1");
            Assert.Equal((1, 0, 1), (a1.CountA, a1.CountB, a1.CountC));
            Assert.Equal(0, coverage.Single(c => c.Code == "A6").Total);
            Assert.Equal("A3", coverage.Single(c => c.IsLeastCovered).Code);
        }

        [Fact]
        public async Task PromoteUpcoming_MovesOpenedCallToAppels()
        {
            var service = CreateService();
            var upcoming = await service.AddUpcomingAsync("Interreg NEXT MED", "Troisième appel à projets", "mars 2027", Today.AddDays(150), "", "https://example.org/next", "");

            var opp = await service.PromoteUpcomingAsync(upcoming.Id, Today);

            Assert.NotNull(opp);
            Assert.Equal("V-2026-001", opp!.VeilleId);
            Assert.Equal(VeilleStatus.Nouveau, opp.PipelineStatus);
            Assert.Null(await service.PromoteUpcomingAsync(upcoming.Id, Today));

            using var db = CreateDbContext();
            Assert.Equal(opp.Id, (await db.UpcomingCalls.SingleAsync()).PromotedGrantId);
        }

        [Fact]
        public async Task SaveScoreAndStatus_RecomputeAndKeepHistory()
        {
            var service = CreateService();
            var added = (await service.ProcessCandidateAsync(Candidate("Programme de reboisement communautaire", "ONG locales."), Today)).Opportunity!;

            var saved = await service.SaveScoreAsync(added.Id, 5, 5, 4, 5, 3, VeilleConfirmation.Confirme, "Page officielle consultée.", Today);
            Assert.Equal(22, saved!.ScoreTotal);
            Assert.Equal("A", saved.Priority);
            Assert.Equal(Today, saved.VerifiedAt);

            Assert.True(await service.SetStatusAsync(added.Id, VeilleStatus.Ecarte, "cofinancement de 50 % impossible", Today));
            using var db = CreateDbContext();
            var opp = await db.GrantsPipeline.SingleAsync();
            Assert.Equal(VeilleStatus.Ecarte, opp.PipelineStatus);
            Assert.Contains("Statut : Nouveau → Écarté (cofinancement de 50 % impossible)", opp.Notes);
        }

        [Fact]
        public async Task PipelineService_AddFromRadar_EntersVeilleBase()
        {
            var pipeline = new PipelineService(CreateDbContext);
            var (success, _, opp) = await pipeline.AddToPipelineAsync(new RssItemCache
            {
                ExternalId = "rss-1",
                Title = "Small grants for forest restoration in the Middle Atlas",
                CleanDescription = "Civil society organizations working on landscapes.",
                Donor = "CEPF",
                Link = "https://example.org/rss-1",
                FeedName = "CEPF"
            });

            Assert.True(success);
            Assert.StartsWith($"V-{DateTime.Today.Year}-", opp!.VeilleId);
            Assert.Equal(VeilleStatus.Nouveau, opp.PipelineStatus);
            Assert.Equal(5, opp.ScorePertinence);
            Assert.Equal("RSS — CEPF", opp.DetectionSource);
            Assert.Equal(VeilleConfirmation.NonConfirme, opp.Confirmation);
        }

        [Fact]
        public async Task SeedDefaultData_MigratesLegacyStatus_AndBackfillScoresOldCalls()
        {
            using (var db = CreateDbContext())
            {
                db.GrantsPipeline.Add(new GrantOpportunity
                {
                    ExternalId = "legacy", Title = "Reboisement de la cédraie", Donor = "UICN", OriginalUrl = "https://example.org/legacy",
                    PipelineStatus = VeilleStatus.LegacyMiseSousVeille, AddedAt = Today.AddDays(-30)
                });
                await db.SaveChangesAsync();
            }

            using (var db = CreateDbContext()) db.SeedDefaultData();
            Assert.Equal(1, await CreateService().BackfillAsync(Today));

            using var check = CreateDbContext();
            var opp = await check.GrantsPipeline.SingleAsync();
            Assert.Equal(VeilleStatus.Nouveau, opp.PipelineStatus);
            Assert.Equal("V-2026-001", opp.VeilleId);
            Assert.NotEmpty(opp.Priority);
            Assert.Contains("A1", opp.VeilleAxes);
        }

        #endregion
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Strategic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// Analyse globale : règles de cohérence entre modules, agrégation des portes de maturité
    /// et contexte complet fourni aux assistants IA.
    /// </summary>
    public class ProjectAnalysisTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public ProjectAnalysisTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var db = CreateDb();
            db.SeedDefaultData();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateDb() => new AppDbContext(_options);

        /// <summary>Projet avec un objectif, un résultat et deux activités ; A1 financée, A2 non.</summary>
        private async Task<(Project Project, Activity Funded, Activity Unfunded)> CreateProjectAsync(Action<Project>? configure = null)
        {
            using var db = CreateDb();
            var project = new Project
            {
                Title = "Restauration forestière Ifrane",
                Currency = "MAD",
                TotalBudget = 100000m,
                StartDate = new DateTime(2027, 1, 1),
                EndDate = new DateTime(2027, 12, 31)
            };
            configure?.Invoke(project);
            db.Projects.Add(project);
            await db.SaveChangesAsync();

            var obj = new Objective { ProjectId = project.Id, Code = "OS1", Description = "Restaurer 50 ha de cédraie" };
            db.Objectives.Add(obj);
            await db.SaveChangesAsync();
            var res = new Result { ObjectiveId = obj.Id, Code = "R1", Description = "50 ha reboisés" };
            db.Results.Add(res);
            await db.SaveChangesAsync();

            var a1 = new Activity { ResultId = res.Id, Code = "A1.1", Description = "Production de plants", StartDate = new DateTime(2027, 2, 1), EndDate = new DateTime(2027, 4, 30) };
            var a2 = new Activity { ResultId = res.Id, Code = "A1.2", Description = "Plantation", StartDate = new DateTime(2027, 11, 1), EndDate = new DateTime(2028, 2, 28) };
            db.Activities.AddRange(a1, a2);
            await db.SaveChangesAsync();

            var cat = db.BudgetCategories.First();
            db.BudgetItems.Add(new BudgetItem { ProjectId = project.Id, CategoryId = cat.Id, ActivityId = a1.Id, Description = "Pépinière", Quantity = 1, UnitCost = 60000m });
            db.BudgetItems.Add(new BudgetItem { ProjectId = project.Id, CategoryId = cat.Id, Description = "Frais divers", Quantity = 1, UnitCost = 15000m });
            await db.SaveChangesAsync();

            return (project, a1, a2);
        }

        [Fact]
        public async Task CrossModuleRules_DetectBudgetScheduleAndRiskGaps()
        {
            var (project, _, _) = await CreateProjectAsync();

            var report = await new DeterministicQualityChecker(CreateDb).EvaluateProjectQualityAsync(project.Id);
            var rules = report.Findings.Select(f => f.RuleId).ToHashSet();

            Assert.Contains("COH-02", rules); // ligne « Frais divers » sans activité
            Assert.Contains("COH-03", rules); // A1.2 sans coût
            Assert.Contains("COH-04", rules); // 75 000 détaillés vs 100 000 déclarés
            Assert.Contains("CHR-03", rules); // A1.2 finit en 2028, après la fin du projet
            Assert.Contains("RSK-01", rules); // aucun risque
            Assert.DoesNotContain("CHR-02", rules);

            var budgetFinding = report.Findings.First(f => f.RuleId == "COH-04");
            Assert.Contains("MAD", budgetFinding.Details);
            Assert.DoesNotContain("DZD", string.Join(" ", report.Findings.Select(f => f.Details)));
        }

        [Fact]
        public async Task CrossModuleRules_FlagInvertedDatesAsCritical_AndUncoveredFunding()
        {
            var (project, funded, _) = await CreateProjectAsync();
            using (var db = CreateDb())
            {
                var act = db.Activities.First(a => a.Id == funded.Id);
                act.EndDate = act.StartDate!.Value.AddDays(-10);
                db.FundingSources.Add(new FundingSource { ProjectId = project.Id, Name = "FEM/PMF", TotalAmountAllocated = 30000m });
                db.Risks.Add(new Risk { ProjectId = project.Id, Description = "Sécheresse prolongée", Probability = 4, Impact = 5, MitigationMeasure = "" });
                await db.SaveChangesAsync();
            }

            var report = await new DeterministicQualityChecker(CreateDb).EvaluateProjectQualityAsync(project.Id);

            Assert.Contains(report.Findings, f => f.RuleId == "CHR-02" && f.Severity == FindingSeverity.Critical);
            Assert.Contains(report.Findings, f => f.RuleId == "COH-05");
            Assert.Contains(report.Findings, f => f.RuleId == "RSK-02");
            Assert.DoesNotContain(report.Findings, f => f.RuleId == "RSK-01");
            Assert.False(report.IsPassed);
        }

        [Fact]
        public async Task CrossModuleRules_ProblemStatementWithoutTree_IsFlagged_AndClearedOnceTreeExists()
        {
            var (project, _, _) = await CreateProjectAsync();
            using (var db = CreateDb())
            {
                db.StrategicFoundations.Add(new StrategicFoundation { ProjectId = project.Id, CoreProblemStatement = "Dégradation de la cédraie", ProblemEvidenceData = "-30 % en 20 ans" });
                await db.SaveChangesAsync();
            }

            var checker = new DeterministicQualityChecker(CreateDb);
            Assert.Contains((await checker.EvaluateProjectQualityAsync(project.Id)).Findings, f => f.RuleId == "COH-01");

            using (var db = CreateDb())
            {
                db.ProblemNodes.Add(new ProblemNode { ProjectId = project.Id, Title = "Dégradation de la cédraie", NodeType = "Core" });
                await db.SaveChangesAsync();
            }
            Assert.DoesNotContain((await checker.EvaluateProjectQualityAsync(project.Id)).Findings, f => f.RuleId == "COH-01");
        }

        [Fact]
        public async Task ProjectAnalysisService_AggregatesQualityAndAllNineStages()
        {
            var (project, _, _) = await CreateProjectAsync();

            var analysis = await new ProjectAnalysisService(CreateDb).AnalyzeAsync(project.Id);

            Assert.Equal(9, analysis.Stages.Count);
            Assert.Equal(Enum.GetValues<LifecycleStage>().Length, analysis.Stages.Select(s => s.Stage).Distinct().Count());
            Assert.InRange(analysis.ReadinessPercent, 0, 100);
            Assert.InRange(analysis.Quality.QualityScore, 0, 100);

            string summary = analysis.ToPromptSummary();
            Assert.Contains("COH-04", summary);
            Assert.Contains("étapes terminées", summary);
        }

        [Fact]
        public async Task FullAiContext_IncludesLogframeBudgetAndRisks_WithProjectCurrency()
        {
            var (project, _, _) = await CreateProjectAsync();
            using (var db = CreateDb())
            {
                db.Risks.Add(new Risk { ProjectId = project.Id, Category = "Environnemental", Description = "Incendie de forêt", Probability = 3, Impact = 5, MitigationMeasure = "Pare-feux" });
                await db.SaveChangesAsync();
            }

            string context = ProjectContextBuilder.BuildFullContext(CreateDb, project.Id);

            Assert.Contains("CADRE LOGIQUE", context);
            Assert.Contains("[OS1]", context);
            Assert.Contains("[A1.2] Plantation", context);
            Assert.Contains("BUDGET DÉTAILLÉ : 75", context);
            Assert.Contains("MAD", context);
            Assert.Contains("Incendie de forêt", context);
            Assert.DoesNotContain("DZD", context);
        }
    }
}

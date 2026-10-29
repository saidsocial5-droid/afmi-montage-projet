using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Research;
using AFMI_Montage_projet_2.Services.Strategic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// ADR 25 : territoire et bénéficiaires (source unique), analyse des documents et liens, intégration au projet,
    /// projets similaires, cohérence entre étapes (doublons, ordre, conformité à l'appel).
    /// </summary>
    public class ResearchAndTerritoryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public ResearchAndTerritoryTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var db = new AppDbContext(_options);
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext Db() => new(_options);

        private int CreateProject(string title = "Gestion durable des parcours forestiers du cèdre", string currency = "MAD")
        {
            using var db = Db();
            var p = new Project { Title = title, Currency = currency, InterventionZone = "Ifrane" };
            db.Projects.Add(p);
            db.SaveChanges();
            return p.Id;
        }

        private static TerritoryProfile Territory(int projectId) => new()
        {
            ProjectId = projectId,
            Region = "Fès-Meknès",
            Province = "Ifrane",
            Communes = "Tigrigra, Timahdite",
            Ecosystems = "Cédraie, parcours",
            EnvironmentalIssues = "Surpâturage des parcours\nDépérissement du cèdre",
            SocialIssues = "Exode des jeunes"
        };

        private static List<BeneficiaryGroup> Groups(int projectId) => new()
        {
            new() { ProjectId = projectId, Name = "Éleveurs membres des AGS", BeneficiaryType = BeneficiaryTypes.Direct, EstimatedCount = 200, WomenPercent = 10, Needs = "Accès au fourrage en hiver", ExpectedBenefits = "Revenus stabilisés" },
            new() { ProjectId = projectId, Name = "Femmes de la coopérative de plantes aromatiques", BeneficiaryType = BeneficiaryTypes.Direct, EstimatedCount = 50, WomenPercent = 100 },
            new() { ProjectId = projectId, Name = "Ménages des douars riverains", BeneficiaryType = BeneficiaryTypes.Indirect, EstimatedCount = 1200, CountUnit = "ménages" }
        };

        // ---------- Schéma ----------

        [Fact]
        public void Schema_MissingModelTables_AreCreatedOnExistingDatabase()
        {
            using (var db = Db())
            {
                db.Database.ExecuteSqlRaw("DROP TABLE \"BeneficiaryGroups\";");
                db.Database.ExecuteSqlRaw("DROP TABLE \"TerritoryProfiles\";");
            }

            using (var db = Db()) db.SeedDefaultData();

            using var check = Db();
            int id = CreateProject();
            check.TerritoryProfiles.Add(new TerritoryProfile { ProjectId = id, Province = "Ifrane" });
            check.BeneficiaryGroups.Add(new BeneficiaryGroup { ProjectId = id, Name = "Éleveurs" });
            check.SaveChanges();
            Assert.Equal(1, check.TerritoryProfiles.Count());
            Assert.Equal(1, check.BeneficiaryGroups.Count());
        }

        // ---------- Territoire : source unique ----------

        [Fact]
        public async Task Territory_IsSingleSource_ForZone_TargetBeneficiaries_AndStakeholders_WithoutDuplicates()
        {
            int id = CreateProject();
            var service = new TerritoryService(Db);

            await service.SaveAsync(id, Territory(id), Groups(id));
            await service.SaveAsync(id, await service.GetOrCreateAsync(id), await service.GetBeneficiariesAsync(id)); // second enregistrement

            using var db = Db();
            var project = db.Projects.Single(p => p.Id == id);
            var foundation = db.StrategicFoundations.Include(f => f.Stakeholders).Single(f => f.ProjectId == id);
            Assert.Equal("Tigrigra, Timahdite — Ifrane — Fès-Meknès (Maroc)", project.InterventionZone);
            Assert.Equal(project.InterventionZone, foundation.TargetZone);
            Assert.Contains("Directs : Éleveurs membres des AGS (200 personnes)", foundation.TargetBeneficiaries);
            Assert.Contains("Indirects : Ménages des douars riverains (1 200 ménages)", foundation.TargetBeneficiaries.Replace(' ', ' ').Replace(' ', ' '));

            var beneficiaryStakeholders = foundation.Stakeholders.Where(s => s.Category == "Beneficiary").ToList();
            Assert.Equal(2, beneficiaryStakeholders.Count); // les 2 groupes directs, une seule fois
            Assert.All(db.BeneficiaryGroups.Where(b => b.BeneficiaryType == BeneficiaryTypes.Direct), b => Assert.NotNull(b.LinkedStakeholderId));
        }

        [Fact]
        public void Totals_UseWeightedWomenShare()
        {
            var totals = TerritoryService.ComputeTotals(Groups(1));
            Assert.Equal(250, totals.DirectCount);
            Assert.Equal(1200, totals.IndirectCount);
            Assert.Equal(28, Math.Round(totals.DirectWomenPercent!.Value)); // (200×10 + 50×100) / 250
        }

        [Fact]
        public async Task StrategicGate_RequiresZoneAndDirectBeneficiaries()
        {
            int id = CreateProject();
            var gate = new ReadinessGateService(Db);

            var before = await gate.EvaluateStageAsync(id, LifecycleStage.StrategicPlanning);
            Assert.Contains(before.BlockingGaps, g => g.Contains("zone d'intervention"));
            Assert.Contains(before.BlockingGaps, g => g.Contains("bénéficiaires directs"));

            await new TerritoryService(Db).SaveAsync(id, Territory(id), Groups(id));
            var after = await gate.EvaluateStageAsync(id, LifecycleStage.StrategicPlanning);
            Assert.DoesNotContain(after.BlockingGaps, g => g.Contains("zone d'intervention"));
            Assert.DoesNotContain(after.BlockingGaps, g => g.Contains("bénéficiaires directs"));
        }

        [Fact]
        public async Task Territory_FeedsNeedsAssessment_OnceOnly()
        {
            int id = CreateProject();
            var service = new TerritoryService(Db);
            await service.SaveAsync(id, Territory(id), Groups(id));

            int first = await service.FeedNeedsAssessmentAsync(id);
            int second = await service.FeedNeedsAssessmentAsync(id);

            Assert.Equal(4, first); // 1 besoin de groupe + 3 problèmes du territoire
            Assert.Equal(0, second);
            using var db = Db();
            Assert.Contains(db.NeedsAssessments, n => n.NeedDescription == "Accès au fourrage en hiver" && n.TargetGroupAffected == "Éleveurs membres des AGS");
        }

        [Fact]
        public void TerritoryAi_FillsOnlyEmptyFields_AndAddsOnlyNewGroups()
        {
            var current = new TerritoryProfile { ProjectId = 1, Province = "Ifrane", Climate = "Saisi par l'équipe" };
            var suggestion = new TerritorySuggestion
            {
                Province = "Autre province",
                Climate = "Proposition IA",
                Ecosystems = "Cédraie",
                Beneficiaries = new() { new() { Name = "Éleveurs membres des AGS" }, new() { Name = "Écoles rurales", Type = "Indirect" } }
            };

            var result = TerritoryAiService.Apply(suggestion, current, Groups(1));

            Assert.Equal("Ifrane", current.Province);
            Assert.Equal("Saisi par l'équipe", current.Climate);
            Assert.Equal("Cédraie", current.Ecosystems);
            var added = Assert.Single(result.NewGroups);
            Assert.Equal("Écoles rurales", added.Name);
            Assert.Equal(BeneficiaryTypes.Indirect, added.BeneficiaryType);
        }

        // ---------- Analyse des documents ----------

        private const string CallText = @"Appel à propositions 2027 — Gestion durable des forêts méditerranéennes

Le présent appel à projets s'adresse aux associations et coopératives. Critères d'éligibilité : être enregistré depuis au moins deux ans.

Montant de la subvention : entre 50 000 € et 300 000 €. Durée maximale des projets : 36 mois.
Un cofinancement d'au moins 10 % du coût total est exigé de la part du demandeur.

Date limite de soumission : 15/03/2027 à 17h00.

Dans la province d'Ifrane, le taux de pauvreté rurale atteint 19,4 % (HCP, 2024).";

        [Fact]
        public void Heuristic_ExtractsCallRequirements_WithoutAi()
        {
            var insights = FactExtraction.Heuristic(CallText, "Appel 2027");

            Assert.Equal(DocumentKinds.CallForProposals, insights.DocumentKind);
            Assert.Equal(300000m, insights.BudgetMax);
            Assert.Equal(50000m, insights.BudgetMin);
            Assert.Equal("EUR", insights.Currency);
            Assert.Equal(36, insights.MaxDurationMonths);
            Assert.Equal(10m, insights.CofinancingPercent);
            Assert.Equal(new DateTime(2027, 3, 15), insights.DeadlineDate);
        }

        [Fact]
        public void Verification_FlagsFiguresAbsentFromSource()
        {
            var insights = new DocumentInsights
            {
                BudgetMax = 300000,
                Statistics = new()
                {
                    new() { Label = "Pauvreté rurale", Value = "19,4", Unit = "%" },
                    new() { Label = "Taux de chômage inventé", Value = "37,8", Unit = "%" }
                }
            };

            FactExtraction.VerifyAgainstSource(insights, CallText);

            Assert.True(insights.Statistics[0].Verified);
            Assert.False(insights.Statistics[1].Verified);
            Assert.Single(insights.UnverifiedFacts);
        }

        [Fact]
        public void LongDocuments_KeepTheMostInformativePassages()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 400; i++) sb.AppendLine($"Paragraphe narratif numéro sans intérêt particulier pour le montage, rédigé pour remplir la page.\n");
            sb.AppendLine("Date limite de soumission : 15/03/2027. Montant maximal : 300 000 €. Critères d'éligibilité : associations.\n");
            for (int i = 0; i < 400; i++) sb.AppendLine($"Autre paragraphe descriptif de remplissage sans données utiles pour l'analyse.\n");

            string excerpt = DocumentIntelligenceService.SelectRelevantExcerpt(sb.ToString(), 6000);

            Assert.True(excerpt.Length < 6200);
            Assert.Contains("Date limite de soumission : 15/03/2027", excerpt);
        }

        [Fact]
        public void HtmlExtraction_DropsScriptsAndMenus_KeepsMainContent()
        {
            const string html = @"<html><head><title>Appel à projets Forêts</title><meta name=""description"" content=""Subventions 2027""></head>
<body><nav><a>Accueil</a><a>Contact</a></nav><script>var tracking = 'secret';</script>
<main><h1>Appel à projets</h1><p>Les associations peuvent demander jusqu'à 300 000 €.</p><ul><li>Critère 1</li><li>Critère 2</li></ul>
<p>Le présent appel soutient la restauration des forêts méditerranéennes et des parcours, avec une attention particulière aux femmes rurales et aux jeunes.</p>
<p>Les propositions complètes doivent être déposées avant la date limite indiquée dans les lignes directrices de l'appel, accompagnées du cadre logique et du budget détaillé.</p></main>
<footer>© 2027 Mentions légales</footer></body></html>";

            var page = HtmlTextExtractor.Extract(html);

            Assert.Equal("Appel à projets Forêts", page.Title);
            Assert.Equal("Subventions 2027", page.Description);
            Assert.Contains("300 000 €", page.Text);
            Assert.Contains("• Critère 1", page.Text);
            Assert.DoesNotContain("tracking", page.Text);
            Assert.DoesNotContain("Mentions légales", page.Text);
            Assert.DoesNotContain("Contact", page.Text);
        }

        private int AddTextDocument(int projectId, string content)
        {
            string path = Path.Combine(TestEnvironment.DataDirectory, $"appel_{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, content);
            using var db = Db();
            var doc = new ProjectDocument { ProjectId = projectId, FileName = "appel.txt", FilePath = path, DocumentType = "Document" };
            db.ProjectDocuments.Add(doc);
            db.SaveChanges();
            return doc.Id;
        }

        [Fact]
        public async Task Analysis_FallsBackToAutomaticExtraction_WhenAiFails_AndBuildsRequirementsSummary()
        {
            int id = CreateProject();
            int docId = AddTextDocument(id, CallText);
            var service = new DocumentIntelligenceService(dbFactory: Db, aiAnalyzer: _ => throw new StructuredAiException("IA indisponible", "", "", 3));

            var outcome = await service.AnalyzeAsync(docId, "contexte");

            Assert.False(outcome.UsedAi);
            Assert.Equal(DocumentKinds.CallForProposals, outcome.Insights.DocumentKind);
            using var db = Db();
            var doc = db.ProjectDocuments.Single(d => d.Id == docId);
            Assert.False(string.IsNullOrWhiteSpace(doc.InsightsJson));
            Assert.Equal(DocumentKinds.CallForProposals, doc.DetectedKind);
            Assert.Contains("300 000", db.Projects.Single(p => p.Id == id).GlobalRequirementsSummary.Replace(' ', ' ').Replace(' ', ' '));
        }

        [Fact]
        public async Task Analysis_WithAi_IsVerified_ThenIntegratedWithoutDuplicates()
        {
            int id = CreateProject();
            int docId = AddTextDocument(id, CallText);
            var aiAnswer = new DocumentInsights
            {
                DocumentKind = DocumentKinds.CallForProposals,
                Title = "Appel forêts méditerranéennes 2027",
                Summary = "Appel à propositions pour la gestion durable des forêts.",
                Donor = "Fonds méditerranéen",
                PriorityThemes = new() { "Restauration des forêts" },
                Statistics = new() { new() { Label = "Pauvreté rurale à Ifrane", Value = "19,4", Unit = "%", Year = "2024" }, new() { Label = "Chiffre inventé", Value = "77", Unit = "%" } },
                Needs = new() { "Diversifier les revenus des éleveurs" },
                Threats = new() { "Sécheresses récurrentes" },
                Beneficiaries = new() { new() { Group = "Coopératives féminines", Count = "3" } }
            };
            var service = new DocumentIntelligenceService(dbFactory: Db, aiAnalyzer: _ => Task.FromResult(aiAnswer));

            var outcome = await service.AnalyzeAsync(docId, "contexte");

            Assert.True(outcome.UsedAi);
            Assert.Equal(300000m, outcome.Insights.BudgetMax); // complété par l'extraction automatique
            var invented = outcome.Proposals.Single(p => p.Title == "Chiffre inventé");
            Assert.False(invented.IsSelected);
            Assert.False(invented.IsVerified);

            var integration = new ProjectIntegrationService(Db);
            var first = await integration.ApplyAsync(id, outcome.Proposals);
            var second = await integration.ApplyAsync(id, outcome.Proposals);

            Assert.True(first.Added >= 7);
            Assert.Equal(0, second.Added);
            using var db = Db();
            var f = db.StrategicFoundations.Include(x => x.DataSources).Include(x => x.ContextIndicators).Include(x => x.NeedsAssessments).Include(x => x.SwotItems).Single(x => x.ProjectId == id);
            Assert.Single(f.DataSources);
            Assert.Single(f.ContextIndicators); // le chiffre non vérifié n'est pas intégré
            Assert.Single(f.NeedsAssessments);
            Assert.Single(f.SwotItems, s => s.Category == "Threat");
            Assert.Equal("Fonds méditerranéen", f.PotentialDonor);
            Assert.Equal(300000m, f.EstimatedBudget);
            Assert.Single(db.BeneficiaryGroups.Where(b => b.ProjectId == id));
        }

        // ---------- Projets similaires ----------

        [Fact]
        public void Keywords_AreTranslated_AndMatchWholeWordsOnly()
        {
            var keywords = SimilarProjectsService.ExtractKeywords("Gestion des parcours forestiers et de la cédraie avec les femmes du réseau des coopératives", 6);

            Assert.Contains("rangeland", keywords);
            Assert.Contains("forest", keywords);
            Assert.Contains("cedar", keywords);
            Assert.Contains("women", keywords);
            Assert.DoesNotContain("water", keywords); // « réseau » ne doit pas compter comme « eau »
        }

        [Fact]
        public void PublicDatabases_ResponsesAreParsed()
        {
            const string worldBank = @"{""projects"":{""P005519"":{""id"":""P005519"",""project_name"":""Lakhdar Watershed Management Pilot Project"",""totalamt"":""4,000,000"",""countryshortname"":""Morocco"",""boardapprovaldate"":""1998-12-22T00:00:00Z"",""closingdate"":""6/30/2005 12:00:00 AM"",""project_abstract"":{""cdata"":""Watershed and rangeland management.""},""url"":""https://projects.worldbank.org/P005519"",""status"":""Closed""}}}";
            var wb = Assert.Single(SimilarProjectsService.ParseWorldBank(worldBank));
            Assert.Equal(4_000_000m, wb.Budget);
            Assert.Equal("1998–2005", wb.Period);
            Assert.Equal("Watershed and rangeland management.", wb.Description);

            const string iati = @"{""rows"":[{""aid"":""44000-P172926"",""reporting"":""World Bank"",""title"":""Blue Economy Program"",""status_code"":2,""day_start"":19135,""day_end"":21030,""description"":""Integrated management of natural resources"",""commitment_eur"":331163230}]}";
            var ia = Assert.Single(SimilarProjectsService.ParseIati(iati, "MA"));
            Assert.Equal("En cours", ia.Status);
            Assert.Equal("2022–2027", ia.Period);
            Assert.Equal("https://d-portal.org/ctrack.html#view=act&aid=44000-P172926", ia.Url);
        }

        [Fact]
        public async Task Search_IsBroadened_WhenAllKeywordsTogetherFindTooLittle()
        {
            var queries = new List<string>();
            Task<List<SimilarProject>> FakeSearch(SimilarProjectsQuery q)
            {
                queries.Add(q.Keywords);
                var found = q.Keywords switch
                {
                    "forest" => Enumerable.Range(1, 4).Select(i => new SimilarProject { Title = $"Forest project {i}" }).ToList(),
                    "rangeland" => new List<SimilarProject> { new() { Title = "Forest project 1" }, new() { Title = "Rangeland project" } },
                    _ => new List<SimilarProject>()
                };
                return Task.FromResult(found);
            }

            var results = await SimilarProjectsService.SearchRelaxedAsync(new SimilarProjectsQuery { Keywords = "forest rangeland women" }, FakeSearch);

            Assert.Equal(new[] { "forest rangeland women", "forest rangeland", "forest", "rangeland" }, queries);
            Assert.Equal(5, results.Count); // 4 + 1 nouveau, le doublon « Forest project 1 » est écarté
        }

        [Fact]
        public void Relevance_RanksRelatedProjectsHigher()
        {
            string project = "Gestion des parcours forestiers rangeland forest livestock women cooperatives";
            double related = SimilarProjectsService.ComputeRelevance(project, "Community rangeland and forest management with livestock herders and women cooperatives");
            double unrelated = SimilarProjectsService.ComputeRelevance(project, "Urban metro rail transport financing");
            Assert.True(related > unrelated);
            Assert.Equal(0, unrelated);
        }

        [Fact]
        public async Task SimilarProjectIdeas_FlowIntoStrategicFoundation_Once()
        {
            int id = CreateProject();
            var selected = new List<SimilarProject> { new() { Title = "Lakhdar Watershed", Organisation = "Banque mondiale", Url = "https://projects.worldbank.org/P005519", Description = "Watershed management" } };
            var ideas = new ProjectIdeas
            {
                Lessons = new() { "Associer les AGS dès la conception" },
                Activities = new() { new() { Title = "Créer des unités pastorales gérées par les AGS", InspiredBy = "Lakhdar Watershed" } },
                Risks = new() { new() { Description = "Conflits d'usage entre éleveurs", Mitigation = "Charte pastorale" } },
                Partners = new() { new() { Name = "ANEF Ifrane", Role = "Appui technique" } },
                Approaches = new() { new() { Title = "Paiement pour services écosystémiques" } }
            };

            var proposals = SimilarProjectsService.ToProposals(ideas, selected);
            foreach (var p in proposals) p.IsSelected = true;
            var integration = new ProjectIntegrationService(Db);
            var first = await integration.ApplyAsync(id, proposals);
            var second = await integration.ApplyAsync(id, proposals);

            Assert.Equal(6, first.Added);
            Assert.Equal(0, second.Added);
            using var db = Db();
            var f = db.StrategicFoundations.Include(x => x.TheoryOfChangeNodes).Include(x => x.Risks).Include(x => x.Stakeholders).Include(x => x.StrategicOptions).Include(x => x.DataSources).Single(x => x.ProjectId == id);
            Assert.Single(f.TheoryOfChangeNodes, n => n.NodeType == "Activity" && n.Description.Contains("Inspiré de : Lakhdar Watershed"));
            Assert.Single(f.Risks);
            Assert.Single(f.Stakeholders, s => s.Category == "Partner");
            Assert.Single(f.StrategicOptions);
            Assert.Equal(2, f.DataSources.Count); // projet source + enseignements
        }

        // ---------- Cohérence entre étapes ----------

        [Fact]
        public async Task Quality_ChecksTerritory_Duplicates_CurrencyAndCallCompliance()
        {
            int id = CreateProject(currency: "EUR");
            using (var db = Db())
            {
                var project = db.Projects.Single(p => p.Id == id);
                project.StartDate = new DateTime(2027, 1, 1);
                project.EndDate = new DateTime(2030, 12, 31); // 48 mois
                db.StrategicFoundations.Add(new StrategicFoundation { ProjectId = id, IdeaTitle = "x", Currency = "MAD", CoreProblemStatement = "Dégradation" });
                var obj = new Objective { ProjectId = id, Code = "OS1", Description = "Restaurer les parcours" };
                db.Objectives.Add(obj);
                db.SaveChanges();
                var res = new Result { ObjectiveId = obj.Id, Code = "R1", Description = "Parcours restaurés" };
                db.Results.Add(res);
                db.SaveChanges();
                db.Activities.Add(new Activity { ResultId = res.Id, Code = "A1.1", Description = "Former les éleveurs à la gestion des parcours" });
                db.Activities.Add(new Activity { ResultId = res.Id, Code = "A1.2", Description = "Former les éleveurs à la gestion des parcours" });
                var category = new BudgetCategory { Name = "Formation" };
                db.BudgetCategories.Add(category);
                db.SaveChanges();
                db.BudgetItems.Add(new BudgetItem { ProjectId = id, Description = "Formations", Quantity = 1, UnitCost = 400000, CategoryId = category.Id });
                db.SaveChanges();
            }
            int docId = AddTextDocument(id, CallText);
            await new DocumentIntelligenceService(dbFactory: Db, aiAnalyzer: _ => throw new StructuredAiException("x", "", "", 1)).AnalyzeAsync(docId, "");

            var report = await new DeterministicQualityChecker(Db).EvaluateProjectQualityAsync(id);
            var rules = report.Findings.Select(f => f.RuleId).ToList();

            Assert.Contains("TER-01", rules);
            Assert.Contains("BEN-01", rules);
            Assert.Contains("COH-07", rules); // MAD (socle) vs EUR (cadrage)
            Assert.Contains("DUP-01", rules); // A1.1 et A1.2 identiques
            Assert.Contains(report.Findings, f => f.RuleId == "DOC-01" && f.Severity == FindingSeverity.Critical); // 400 000 > 300 000
            Assert.Contains(report.Findings, f => f.RuleId == "DOC-02" && f.Severity == FindingSeverity.Critical); // 48 > 36 mois
            Assert.Contains("DOC-04", rules); // cofinancement 0 % < 10 %
        }

        [Fact]
        public void Sequence_FlagsSkippedStage()
        {
            var result = new ProjectAnalysisResult { Quality = new QualityReport { QualityScore = 80 } };
            result.Stages.Add(new StageReadiness { Stage = LifecycleStage.StrategicPlanning, Label = "1. Planification stratégique", Status = StageStatus.Completed });
            result.Stages.Add(new StageReadiness { Stage = LifecycleStage.Cadrage, Label = "2. Cadrage", Status = StageStatus.InProgress });
            result.Stages.Add(new StageReadiness { Stage = LifecycleStage.ProblemTree, Label = "3. Arbre à problèmes", Status = StageStatus.NotStarted });
            result.Stages.Add(new StageReadiness { Stage = LifecycleStage.CadreLogique, Label = "4. Cadre logique", Status = StageStatus.NotStarted });
            result.Stages.Add(new StageReadiness { Stage = LifecycleStage.Budget, Label = "6. Budget", Status = StageStatus.Completed });
            result.Stages.Add(new StageReadiness { Stage = LifecycleStage.Export, Label = "9. Exports", Status = StageStatus.Completed });

            ProjectAnalysisService.AddSequenceFindings(result);

            var seq = result.Quality.Findings.Where(f => f.RuleId == "SEQ-01").ToList();
            Assert.Equal(2, seq.Count); // arbre à problèmes et cadre logique sautés avant le budget
            Assert.Contains("6. Budget", seq[0].Details);
            Assert.Equal(74, result.Quality.QualityScore);
        }

        [Fact]
        public async Task AiContext_And_Export_IncludeTerritoryBeneficiariesAndKnowledge()
        {
            int id = CreateProject();
            await new TerritoryService(Db).SaveAsync(id, Territory(id), Groups(id));
            var proposals = SimilarProjectsService.ToProposals(new ProjectIdeas { Lessons = new() { "Impliquer les AGS" } }, new List<SimilarProject>());
            await new ProjectIntegrationService(Db).ApplyAsync(id, proposals);

            string context = ProjectContextBuilder.BuildFullContext(Db, id);
            Assert.Contains("TERRITOIRE D'INTERVENTION", context);
            Assert.Contains("Surpâturage des parcours", context);
            Assert.Contains("BÉNÉFICIAIRES", context);
            Assert.Contains("[Direct] Éleveurs membres des AGS", context);
            Assert.Contains("Leçon : Impliquer les AGS", context);

            using var db = Db();
            var sections = DocumentExportService.BuildTerritorySections(db, id);
            Assert.Contains(sections, s => s.Heading.StartsWith("Présentation de la zone"));
            Assert.Contains(sections, s => s.Heading.StartsWith("Problèmes") && s.Body.Contains("• Dépérissement du cèdre"));
            Assert.Contains(sections, s => s.Heading.StartsWith("Bénéficiaires") && s.Body.Contains("Bénéficiaires indirects"));
        }
    }
}

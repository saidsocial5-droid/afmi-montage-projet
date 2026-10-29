using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.Services.Research
{
    /// <summary>Destination d'une information intégrée au projet.</summary>
    public enum IntegrationTarget
    {
        DataSource,
        DonorInfo,
        BudgetCeiling,
        ContextIndicator,
        ProblemEvidence,
        Need,
        SwotOpportunity,
        SwotThreat,
        Beneficiary,
        Territory,
        TocActivity,
        StrategicRisk,
        StrategicOption,
        Partner
    }

    /// <summary>Information proposée à l'intégration ; l'utilisateur coche ce qu'il retient.</summary>
    public partial class IntegrationProposal : ObservableObject
    {
        public IntegrationTarget Target { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;

        /// <summary>Données de l'élément (champ → valeur) utilisées à l'application.</summary>
        public Dictionary<string, string> Values { get; init; } = new();

        /// <summary>Faux si la donnée chiffrée n'a pas été retrouvée dans la source (décochée par défaut).</summary>
        public bool IsVerified { get; init; } = true;

        [ObservableProperty]
        private bool _isSelected = true;

        public string TargetLabel => Target switch
        {
            IntegrationTarget.DataSource => "📚 Source de données",
            IntegrationTarget.DonorInfo => "🏦 Bailleur & priorités",
            IntegrationTarget.BudgetCeiling => "💰 Plafond budgétaire",
            IntegrationTarget.ContextIndicator => "📊 Indicateur de contexte",
            IntegrationTarget.ProblemEvidence => "❗ Donnée probante du problème",
            IntegrationTarget.Need => "🎯 Besoin",
            IntegrationTarget.SwotOpportunity => "🌱 Opportunité (SWOT)",
            IntegrationTarget.SwotThreat => "⚡ Menace (SWOT)",
            IntegrationTarget.Beneficiary => "👥 Bénéficiaires",
            IntegrationTarget.Territory => "🗺️ Territoire",
            IntegrationTarget.TocActivity => "🔄 Activité (théorie du changement)",
            IntegrationTarget.StrategicRisk => "⚠️ Risque",
            IntegrationTarget.StrategicOption => "🧭 Option stratégique",
            IntegrationTarget.Partner => "🤝 Partenaire potentiel",
            _ => Target.ToString()
        };

        public string Get(string key) => Values.TryGetValue(key, out var v) ? v : string.Empty;
    }

    public class IntegrationResult
    {
        public int Added { get; set; }
        public int Skipped { get; set; }
        public List<string> Messages { get; } = new();
        public string Summary => $"{Added} élément(s) intégré(s) au projet" + (Skipped > 0 ? $", {Skipped} déjà présent(s) (ignoré(s))" : "") + ".";
    }

    /// <summary>
    /// Intègre au projet les informations issues des documents, liens et projets similaires (ADR 25).
    /// Chaque information va à l'endroit où l'étape suivante l'exploite (socle stratégique, territoire,
    /// bénéficiaires, théorie du changement…), sans jamais créer de doublon (comparaison normalisée).
    /// </summary>
    public class ProjectIntegrationService
    {
        private readonly Func<AppDbContext> _dbFactory;

        public ProjectIntegrationService(Func<AppDbContext>? dbFactory = null) => _dbFactory = dbFactory ?? (() => new AppDbContext());

        /// <summary>Clé de comparaison : minuscules, sans accents ni ponctuation, espaces réduits.</summary>
        public static string Key(string? text) =>
            Regex.Replace(Regex.Replace(TextNormalizer.Normalize(text), @"[^\p{L}\p{N}]+", " "), @"\s+", " ").Trim();

        /// <summary>Vrai si deux libellés désignent la même chose (identiques ou l'un contenu dans l'autre, assez long).</summary>
        public static bool SameMeaning(string? a, string? b)
        {
            string ka = Key(a), kb = Key(b);
            if (ka.Length == 0 || kb.Length == 0) return false;
            if (ka == kb) return true;
            return Math.Min(ka.Length, kb.Length) >= 12 && (ka.Contains(kb) || kb.Contains(ka));
        }

        /// <summary>Propositions d'intégration tirées de l'analyse d'un document ou d'une page web.</summary>
        public static List<IntegrationProposal> FromDocument(DocumentInsights insights, string sourceName, string sourcePath, bool isWeb)
        {
            var list = new List<IntegrationProposal>
            {
                new()
                {
                    Target = IntegrationTarget.DataSource,
                    Title = string.IsNullOrWhiteSpace(insights.Title) ? sourceName : insights.Title,
                    Detail = $"{DocumentKinds.Label(insights.DocumentKind)} — enregistré comme source du diagnostic",
                    Values = new()
                    {
                        ["Title"] = string.IsNullOrWhiteSpace(insights.Title) ? sourceName : insights.Title,
                        ["SourceType"] = isWeb ? "WebLink" : insights.DocumentKind == DocumentKinds.Statistics ? "Statistic" : "Document",
                        ["FilePath"] = sourcePath,
                        ["Reliability"] = isWeb ? "Medium" : "High",
                        ["Insights"] = Shorten(insights.Summary, 1500)
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(insights.Donor) || insights.PriorityThemes.Any())
            {
                list.Add(new IntegrationProposal
                {
                    Target = IntegrationTarget.DonorInfo,
                    Title = string.IsNullOrWhiteSpace(insights.Donor) ? "Priorités du bailleur" : insights.Donor,
                    Detail = insights.PriorityThemes.Any() ? "Priorités : " + string.Join(" ; ", insights.PriorityThemes.Take(6)) : "Bailleur pressenti",
                    Values = new() { ["Donor"] = insights.Donor, ["Priorities"] = string.Join(" ; ", insights.PriorityThemes) }
                });
            }

            if (insights.IsCallForProposals && insights.BudgetMax.HasValue)
            {
                list.Add(new IntegrationProposal
                {
                    Target = IntegrationTarget.BudgetCeiling,
                    Title = $"Montant maximal de l'appel : {insights.BudgetMax:N0} {insights.Currency}",
                    Detail = "Utilisé comme budget estimatif plafond du socle stratégique (s'il n'est pas encore fixé).",
                    Values = new() { ["Amount"] = insights.BudgetMax.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Currency"] = insights.Currency },
                    IsVerified = !insights.UnverifiedFacts.Any(f => f.StartsWith("Montant maximum", StringComparison.Ordinal))
                });
            }

            foreach (var stat in insights.Statistics.Where(s => !string.IsNullOrWhiteSpace(s.Label) && !string.IsNullOrWhiteSpace(s.Value)))
            {
                list.Add(new IntegrationProposal
                {
                    Target = IntegrationTarget.ContextIndicator,
                    Title = stat.Label,
                    Detail = $"{stat.Value} {stat.Unit}{(string.IsNullOrWhiteSpace(stat.Year) ? "" : $" ({stat.Year})")}{(string.IsNullOrWhiteSpace(stat.Location) ? "" : $" — {stat.Location}")}{(stat.Verified ? "" : " — ⚠ non retrouvé dans la source")}",
                    Values = new() { ["Name"] = stat.Label, ["Value"] = stat.Value, ["Unit"] = stat.Unit, ["Year"] = stat.Year, ["Source"] = sourceName, ["Location"] = stat.Location },
                    IsVerified = stat.Verified,
                    IsSelected = stat.Verified
                });
            }

            foreach (var problem in insights.Problems.Where(p => !string.IsNullOrWhiteSpace(p)))
                list.Add(new IntegrationProposal { Target = IntegrationTarget.ProblemEvidence, Title = Shorten(problem, 160), Detail = "Ajouté aux données probantes du problème central", Values = new() { ["Text"] = problem, ["Source"] = sourceName } });

            foreach (var need in insights.Needs.Where(n => !string.IsNullOrWhiteSpace(n)))
                list.Add(new IntegrationProposal { Target = IntegrationTarget.Need, Title = Shorten(need, 160), Detail = "Analyse des besoins", Values = new() { ["Text"] = need } });

            foreach (var o in insights.Opportunities.Where(o => !string.IsNullOrWhiteSpace(o)))
                list.Add(new IntegrationProposal { Target = IntegrationTarget.SwotOpportunity, Title = Shorten(o, 160), Values = new() { ["Text"] = o, ["Source"] = sourceName } });

            foreach (var t in insights.Threats.Where(t => !string.IsNullOrWhiteSpace(t)))
                list.Add(new IntegrationProposal { Target = IntegrationTarget.SwotThreat, Title = Shorten(t, 160), Values = new() { ["Text"] = t, ["Source"] = sourceName } });

            foreach (var b in insights.Beneficiaries.Where(b => !string.IsNullOrWhiteSpace(b.Group)))
                list.Add(new IntegrationProposal
                {
                    Target = IntegrationTarget.Beneficiary,
                    Title = b.Group,
                    Detail = $"{(string.IsNullOrWhiteSpace(b.Count) ? "effectif non précisé" : b.Count)}{(string.IsNullOrWhiteSpace(b.Details) ? "" : " — " + Shorten(b.Details, 120))}",
                    Values = new() { ["Name"] = b.Group, ["Count"] = b.Count, ["Details"] = b.Details }
                });

            if (!string.IsNullOrWhiteSpace(insights.TerritoryInfo))
                list.Add(new IntegrationProposal { Target = IntegrationTarget.Territory, Title = "Informations sur le territoire", Detail = Shorten(insights.TerritoryInfo, 200), Values = new() { ["Text"] = insights.TerritoryInfo, ["Source"] = sourceName } });

            return list;
        }

        /// <summary>Applique les propositions cochées. Les éléments déjà présents (même sens) sont ignorés.</summary>
        public async Task<IntegrationResult> ApplyAsync(int projectId, IEnumerable<IntegrationProposal> proposals, string userEmail = "Système")
        {
            var selected = proposals.Where(p => p.IsSelected).ToList();
            var result = new IntegrationResult();
            if (selected.Count == 0) return result;

            using var db = _dbFactory();
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
                          ?? throw new InvalidOperationException("Projet introuvable.");
            var foundation = await GetOrCreateFoundationAsync(db, project, userEmail);

            foreach (var p in selected)
            {
                bool added = p.Target switch
                {
                    IntegrationTarget.DataSource => AddDataSource(db, foundation, p),
                    IntegrationTarget.DonorInfo => ApplyDonor(foundation, p),
                    IntegrationTarget.BudgetCeiling => ApplyBudgetCeiling(foundation, p),
                    IntegrationTarget.ContextIndicator => AddContextIndicator(db, foundation, p),
                    IntegrationTarget.ProblemEvidence => AppendProblemEvidence(foundation, p),
                    IntegrationTarget.Need => AddNeed(db, foundation, p),
                    IntegrationTarget.SwotOpportunity => AddSwot(db, foundation, p, "Opportunity"),
                    IntegrationTarget.SwotThreat => AddSwot(db, foundation, p, "Threat"),
                    IntegrationTarget.Beneficiary => await AddBeneficiaryAsync(db, projectId, p),
                    IntegrationTarget.Territory => await AppendTerritoryAsync(db, projectId, p),
                    IntegrationTarget.TocActivity => AddTocActivity(db, foundation, p),
                    IntegrationTarget.StrategicRisk => AddRisk(db, foundation, p),
                    IntegrationTarget.StrategicOption => AddOption(db, foundation, p),
                    IntegrationTarget.Partner => AddPartner(db, foundation, p),
                    _ => false
                };
                if (added) result.Added++; else result.Skipped++;
            }

            foundation.LastModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            Security.AuditLogger.Log("Intégration de connaissances", $"{result.Added} élément(s) intégré(s) au projet {projectId} depuis l'analyse documentaire.", userEmail);
            return result;
        }

        private static async Task<StrategicFoundation> GetOrCreateFoundationAsync(AppDbContext db, Project project, string userEmail)
        {
            var foundation = await db.StrategicFoundations
                .Include(f => f.DataSources).Include(f => f.ContextIndicators).Include(f => f.NeedsAssessments)
                .Include(f => f.SwotItems).Include(f => f.TheoryOfChangeNodes).Include(f => f.Risks)
                .Include(f => f.StrategicOptions).Include(f => f.Stakeholders)
                .FirstOrDefaultAsync(f => f.ProjectId == project.Id);
            if (foundation != null) return foundation;

            foundation = new StrategicFoundation
            {
                ProjectId = project.Id,
                IdeaTitle = project.Title,
                TargetZone = project.InterventionZone,
                Currency = project.Currency,
                CreatedBy = userEmail,
                CreatedAt = DateTime.UtcNow
            };
            db.StrategicFoundations.Add(foundation);
            return foundation;
        }

        private static bool AddDataSource(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            string path = p.Get("FilePath");
            if (f.DataSources.Any(d => !d.IsDeleted && ((!string.IsNullOrEmpty(path) && string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)) || SameMeaning(d.Title, p.Get("Title")))))
            {
                var existing = f.DataSources.First(d => !d.IsDeleted && ((!string.IsNullOrEmpty(path) && string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)) || SameMeaning(d.Title, p.Get("Title"))));
                if (string.IsNullOrWhiteSpace(existing.ExtractedKeyInsights)) existing.ExtractedKeyInsights = p.Get("Insights");
                return false;
            }
            var source = new DataSource
            {
                StrategicFoundation = f,
                Title = Shorten(p.Get("Title"), 250),
                SourceType = p.Get("SourceType"),
                FilePath = path,
                ReliabilityLevel = string.IsNullOrEmpty(p.Get("Reliability")) ? "Medium" : p.Get("Reliability"),
                ExtractedKeyInsights = p.Get("Insights")
            };
            f.DataSources.Add(source);
            db.DataSources.Add(source);
            return true;
        }

        private static bool ApplyDonor(StrategicFoundation f, IntegrationProposal p)
        {
            bool changed = false;
            if (string.IsNullOrWhiteSpace(f.PotentialDonor) && !string.IsNullOrWhiteSpace(p.Get("Donor")))
            {
                f.PotentialDonor = Shorten(p.Get("Donor"), 200);
                changed = true;
            }
            foreach (var priority in p.Get("Priorities").Split(';').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                if (f.DonorPriorities.Split(';').Any(existing => SameMeaning(existing, priority))) continue;
                f.DonorPriorities = string.IsNullOrWhiteSpace(f.DonorPriorities) ? priority : f.DonorPriorities + " ; " + priority;
                changed = true;
            }
            return changed;
        }

        private static bool ApplyBudgetCeiling(StrategicFoundation f, IntegrationProposal p)
        {
            if (f.EstimatedBudget > 0 || !decimal.TryParse(p.Get("Amount"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amount)) return false;
            f.EstimatedBudget = amount;
            return true;
        }

        private static bool AddContextIndicator(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            if (f.ContextIndicators.Any(c => !c.IsDeleted && SameMeaning(c.Name, p.Get("Name")) && Key(c.BaselineValue) == Key(p.Get("Value")))) return false;
            var indicator = new ContextIndicator
            {
                StrategicFoundation = f,
                Name = Shorten(p.Get("Name"), 250),
                BaselineValue = Shorten(p.Get("Value"), 100),
                Unit = Shorten(p.Get("Unit"), 50),
                Year = int.TryParse(Regex.Match(p.Get("Year"), @"\d{4}").Value, out var y) ? y : DateTime.UtcNow.Year,
                SourceName = Shorten(p.Get("Source"), 200),
                Notes = string.Join(" — ", new[] { p.Get("Location"), p.IsVerified ? "" : "valeur à vérifier dans la source" }.Where(s => !string.IsNullOrWhiteSpace(s)))
            };
            f.ContextIndicators.Add(indicator);
            db.ContextIndicators.Add(indicator);
            return true;
        }

        private static bool AppendProblemEvidence(StrategicFoundation f, IntegrationProposal p)
        {
            string text = p.Get("Text");
            if (f.ProblemEvidenceData.Split('\n').Any(line => SameMeaning(line.TrimStart('•', ' '), text))) return false;
            string line = $"• {text} (source : {p.Get("Source")})";
            f.ProblemEvidenceData = string.IsNullOrWhiteSpace(f.ProblemEvidenceData) ? line : f.ProblemEvidenceData.TrimEnd() + "\n" + line;
            return true;
        }

        private static bool AddNeed(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            if (f.NeedsAssessments.Any(n => !n.IsDeleted && SameMeaning(n.NeedDescription, p.Get("Text")))) return false;
            var need = new NeedAssessment
            {
                StrategicFoundation = f,
                NeedDescription = p.Get("Text"),
                TargetGroupAffected = p.Get("Group"),
                PriorityRank = f.NeedsAssessments.Count(n => !n.IsDeleted) + 1
            };
            f.NeedsAssessments.Add(need);
            db.NeedsAssessments.Add(need);
            return true;
        }

        private static bool AddSwot(AppDbContext db, StrategicFoundation f, IntegrationProposal p, string category)
        {
            if (f.SwotItems.Any(s => !s.IsDeleted && s.Category == category && SameMeaning(s.Statement, p.Get("Text")))) return false;
            var item = new SwotItem { StrategicFoundation = f, Category = category, Statement = p.Get("Text"), SourceEvidence = p.Get("Source") };
            f.SwotItems.Add(item);
            db.SwotItems.Add(item);
            return true;
        }

        private static async Task<bool> AddBeneficiaryAsync(AppDbContext db, int projectId, IntegrationProposal p)
        {
            var existing = await db.BeneficiaryGroups.Where(b => b.ProjectId == projectId).ToListAsync();
            existing.AddRange(db.BeneficiaryGroups.Local.Where(b => b.ProjectId == projectId && b.Id == 0));
            if (existing.Any(b => SameMeaning(b.Name, p.Get("Name")))) return false;

            var group = new BeneficiaryGroup
            {
                ProjectId = projectId,
                Name = Shorten(p.Get("Name"), 200),
                BeneficiaryType = string.IsNullOrEmpty(p.Get("Type")) ? BeneficiaryTypes.Direct : p.Get("Type"),
                EstimatedCount = int.TryParse(Regex.Replace(p.Get("Count"), @"[^\d]", ""), out var n) && n > 0 ? n : null,
                Characteristics = p.Get("Details")
            };
            db.BeneficiaryGroups.Add(group);
            return true;
        }

        private static async Task<bool> AppendTerritoryAsync(AppDbContext db, int projectId, IntegrationProposal p)
        {
            var territory = await db.TerritoryProfiles.FirstOrDefaultAsync(t => t.ProjectId == projectId)
                            ?? db.TerritoryProfiles.Local.FirstOrDefault(t => t.ProjectId == projectId);
            if (territory == null)
            {
                territory = new TerritoryProfile { ProjectId = projectId };
                db.TerritoryProfiles.Add(territory);
            }
            string text = p.Get("Text");
            if (SameMeaning(territory.Synthesis, text) || territory.Synthesis.Contains(text, StringComparison.OrdinalIgnoreCase)) return false;
            territory.Synthesis = string.IsNullOrWhiteSpace(territory.Synthesis) ? text : territory.Synthesis.TrimEnd() + "\n\n" + text;
            string source = p.Get("Source");
            if (!string.IsNullOrWhiteSpace(source) && !territory.Sources.Contains(source, StringComparison.OrdinalIgnoreCase))
                territory.Sources = string.IsNullOrWhiteSpace(territory.Sources) ? source : territory.Sources + " ; " + source;
            territory.UpdatedAt = DateTime.UtcNow;
            return true;
        }

        private static bool AddTocActivity(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            if (f.TheoryOfChangeNodes.Any(n => !n.IsDeleted && n.NodeType == "Activity" && SameMeaning(n.Title, p.Get("Title")))) return false;
            var node = new TheoryOfChangeNode
            {
                StrategicFoundation = f,
                NodeType = "Activity",
                Title = Shorten(p.Get("Title"), 250),
                Description = p.Get("Description"),
                OrderIndex = f.TheoryOfChangeNodes.Count(n => !n.IsDeleted) + 1
            };
            f.TheoryOfChangeNodes.Add(node);
            db.TheoryOfChangeNodes.Add(node);
            return true;
        }

        private static bool AddRisk(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            if (f.Risks.Any(r => !r.IsDeleted && SameMeaning(r.Description, p.Get("Description")))) return false;
            var risk = new RiskEntry
            {
                StrategicFoundation = f,
                Code = $"R{f.Risks.Count(r => !r.IsDeleted) + 1:00}",
                Category = string.IsNullOrEmpty(p.Get("Category")) ? "Operational" : p.Get("Category"),
                Description = p.Get("Description"),
                MitigationStrategy = p.Get("Mitigation")
            };
            f.Risks.Add(risk);
            db.StrategicRisks.Add(risk);
            return true;
        }

        private static bool AddOption(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            if (f.StrategicOptions.Any(o => !o.IsDeleted && SameMeaning(o.OptionTitle, p.Get("Title")))) return false;
            var option = new StrategicOption
            {
                StrategicFoundation = f,
                OptionTitle = Shorten(p.Get("Title"), 250),
                Description = p.Get("Description"),
                DecisionRationale = p.Get("Rationale")
            };
            f.StrategicOptions.Add(option);
            db.StrategicOptions.Add(option);
            return true;
        }

        private static bool AddPartner(AppDbContext db, StrategicFoundation f, IntegrationProposal p)
        {
            if (f.Stakeholders.Any(s => !s.IsDeleted && SameMeaning(s.Name, p.Get("Name")))) return false;
            var stakeholder = new StakeholderEntry
            {
                StrategicFoundation = f,
                Name = Shorten(p.Get("Name"), 200),
                Category = "Partner",
                Role = p.Get("Role"),
                Expectations = p.Get("Details"),
                EngagementStrategy = "KeepSatisfied"
            };
            f.Stakeholders.Add(stakeholder);
            db.Stakeholders.Add(stakeholder);
            return true;
        }

        public static string Shorten(string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = text.Trim();
            return text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
        }
    }
}

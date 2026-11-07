using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;

namespace AFMI_Montage_projet_2.Services.Strategic
{
    public enum FindingSeverity
    {
        Info,
        Warning,
        Critical
    }

    public class QualityFinding
    {
        public string RuleId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public FindingSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string? EntityRef { get; set; }
    }

    public class QualityReport
    {
        public int ProjectId { get; set; }
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
        public int QualityScore { get; set; } // 0 - 100
        public bool IsPassed => !Findings.Any(f => f.Severity == FindingSeverity.Critical);
        public List<QualityFinding> Findings { get; set; } = new();

        public int CriticalCount => Findings.Count(f => f.Severity == FindingSeverity.Critical);
        public int WarningCount => Findings.Count(f => f.Severity == FindingSeverity.Warning);
        public int InfoCount => Findings.Count(f => f.Severity == FindingSeverity.Info);
    }

    /// <summary>
    /// محرك جودة حتمي ومستقل 100% عن الذكاء الاصطناعي لفحص ترابط المشروع وفق معايير RBM
    /// </summary>
    public class DeterministicQualityChecker
    {
        private static DeterministicQualityChecker? _instance;
        public static DeterministicQualityChecker Instance => _instance ??= new DeterministicQualityChecker();

        private readonly Func<AppDbContext> _dbFactory;

        public DeterministicQualityChecker(Func<AppDbContext>? dbFactory = null)
        {
            _dbFactory = dbFactory ?? (() => new AppDbContext());
        }

        public async Task<QualityReport> EvaluateProjectQualityAsync(int projectId)
        {
            var report = new QualityReport { ProjectId = projectId };
            using var db = _dbFactory();

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                report.Findings.Add(new QualityFinding
                {
                    RuleId = "GEN-01",
                    Category = "Général",
                    Severity = FindingSeverity.Critical,
                    Title = "Projet introuvable",
                    Details = "L'identifiant du projet est invalide.",
                    Recommendation = "Veuillez sélectionner un projet valide."
                });
                report.QualityScore = 0;
                return report;
            }

            var foundation = await db.StrategicFoundations
                .Include(f => f.StrategicObjectives)
                .Include(f => f.TheoryOfChangeNodes)
                .Include(f => f.Risks)
                .Include(f => f.ContextIndicators)
                .FirstOrDefaultAsync(f => f.ProjectId == projectId && !f.IsDeleted);

            var objectives = await db.Objectives
                .Include(o => o.Results)
                    .ThenInclude(r => r.Activities)
                        .ThenInclude(a => a.Indicators)
                .Where(o => o.ProjectId == projectId)
                .ToListAsync();

            var budgetItems = await db.BudgetItems.Where(b => b.ProjectId == projectId).ToListAsync();

            int score = 100;

            // 1. فحص الأساس الاستراتيجي
            if (foundation == null)
            {
                report.Findings.Add(new QualityFinding
                {
                    RuleId = "STRAT-01",
                    Category = "Planification Stratégique",
                    Severity = FindingSeverity.Critical,
                    Title = "Absence de socle stratégique",
                    Details = "Le socle stratégique du projet n'a pas encore été initialisé.",
                    Recommendation = "Renseignez la fiche d'idée, le contexte et l'analyse des besoins dans le module de Planification Stratégique."
                });
                score -= 30;
            }
            else
            {
                // فحص المشكلة المركزية والأدلة
                if (string.IsNullOrWhiteSpace(foundation.CoreProblemStatement))
                {
                    report.Findings.Add(new QualityFinding
                    {
                        RuleId = "STRAT-02",
                        Category = "Planification Stratégique",
                        Severity = FindingSeverity.Critical,
                        Title = "Absence de formulation du problème central",
                        Details = "Le problème central n'est pas formulé avec des données probantes.",
                        Recommendation = "Formulez clairement le problème central en l'appuyant sur des indicateurs chiffrés."
                    });
                    score -= 15;
                }
                else if (string.IsNullOrWhiteSpace(foundation.ProblemEvidenceData))
                {
                    report.Findings.Add(new QualityFinding
                    {
                        RuleId = "STRAT-03",
                        Category = "Planification Stratégique",
                        Severity = FindingSeverity.Warning,
                        Title = "Manque de données probantes pour le problème central",
                        Details = "Le problème central est formulé mais ne dispose pas de données statistiques dans le champ des preuves.",
                        Recommendation = "Ajoutez des pourcentages ou statistiques issus de sources fiables pour consolider le dossier auprès du bailleur."
                    });
                    score -= 5;
                }

                // فحص سلسلة نظرية التغيير (ToC)
                var tocNodes = foundation.TheoryOfChangeNodes.Where(n => !n.IsDeleted).ToList();
                if (!tocNodes.Any())
                {
                    report.Findings.Add(new QualityFinding
                    {
                        RuleId = "TOC-01",
                        Category = "Théorie du Changement",
                        Severity = FindingSeverity.Warning,
                        Title = "Théorie du changement non configurée",
                        Details = "La chaîne causale de la théorie du changement n'a pas été construite.",
                        Recommendation = "Définissez le cheminement du changement des intrants jusqu'à l'impact dans l'onglet Théorie du Changement."
                    });
                    score -= 10;
                }
                else
                {
                    bool hasImpact = tocNodes.Any(n => string.Equals(n.NodeType, "Impact", StringComparison.OrdinalIgnoreCase));
                    bool hasOutcome = tocNodes.Any(n => string.Equals(n.NodeType, "Outcome", StringComparison.OrdinalIgnoreCase));
                    bool hasActivity = tocNodes.Any(n => string.Equals(n.NodeType, "Activity", StringComparison.OrdinalIgnoreCase));

                    if (!hasImpact || !hasOutcome || !hasActivity)
                    {
                        report.Findings.Add(new QualityFinding
                        {
                            RuleId = "TOC-02",
                            Category = "Théorie du Changement",
                            Severity = FindingSeverity.Warning,
                            Title = "Chaîne de la théorie du changement incomplète",
                            Details = $"Certains maillons clés sont manquants (Impact: {(hasImpact ? "✓" : "✗")}, Outcome: {(hasOutcome ? "✓" : "✗")}, Activity: {(hasActivity ? "✓" : "✗")}).",
                            Recommendation = "Complétez la chaîne causale pour couvrir l'ensemble des niveaux, des activités jusqu'à l'impact à long terme."
                        });
                        score -= 5;
                    }
                }

                // فحص معايير SMART للأهداف الاستراتيجية
                var stratObjs = foundation.StrategicObjectives.Where(o => !o.IsDeleted && o.IsSpecificObjective).ToList();
                foreach (var obj in stratObjs)
                {
                    bool hasMetrics = Regex.IsMatch(obj.Description + " " + obj.Title, @"\d+");
                    if (!hasMetrics)
                    {
                        report.Findings.Add(new QualityFinding
                        {
                            RuleId = "SMART-01",
                            Category = "Objectifs SMART",
                            Severity = FindingSeverity.Warning,
                            Title = $"L'objectif [{obj.Code}] manque d'indicateur chiffré mesurable",
                            Details = $"La formulation de l'objectif '{obj.Title}' ne comporte aucun chiffre ou volume cible.",
                            Recommendation = "Rendez l'objectif mesurable (M) en intégrant un effectif de bénéficiaires ou un pourcentage ciblé.",
                            EntityRef = obj.Code
                        });
                        score -= 3;
                    }
                }
            }

            // 2. فحص الإطار المنطقي (Logframe)
            if (!objectives.Any())
            {
                report.Findings.Add(new QualityFinding
                {
                    RuleId = "CADR-01",
                    Category = "Cadre Logique",
                    Severity = FindingSeverity.Critical,
                    Title = "Absence d'objectifs dans le cadre logique",
                    Details = "Aucun objectif spécifique n'a été défini dans le cadrage et le cadre logique.",
                    Recommendation = "Utilisez le moteur de propagation pour transférer les objectifs stratégiques vers le cadre logique."
                });
                score -= 20;
            }
            else
            {
                foreach (var obj in objectives)
                {
                    if (!obj.Results.Any())
                    {
                        report.Findings.Add(new QualityFinding
                        {
                            RuleId = "LOG-01",
                            Category = "Cadre Logique",
                            Severity = FindingSeverity.Critical,
                            Title = $"L'objectif [{obj.Code}] n'a aucun résultat associé",
                            Details = $"Aucun résultat attendu n'est rattaché à l'objectif '{obj.Code}'.",
                            Recommendation = "Ajoutez au moins un résultat attendu (Résultat) pour cet objectif.",
                            EntityRef = obj.Code
                        });
                        score -= 8;
                    }

                    foreach (var res in obj.Results)
                    {
                        if (!res.Activities.Any())
                        {
                            report.Findings.Add(new QualityFinding
                            {
                                RuleId = "LOG-02",
                                Category = "Cadre Logique",
                                Severity = FindingSeverity.Critical,
                                Title = $"Le résultat [{res.Code}] n'a aucune activité opérationnelle",
                                Details = $"Le résultat '{res.Description}' ne dispose d'aucune activité pour sa réalisation.",
                                Recommendation = "Ajoutez au moins une activité pour atteindre ce résultat.",
                                EntityRef = res.Code
                            });
                            score -= 8;
                        }

                        foreach (var act in res.Activities)
                        {
                            if (!act.Indicators.Any())
                            {
                                report.Findings.Add(new QualityFinding
                                {
                                    RuleId = "LOG-03",
                                    Category = "Indicateurs",
                                    Severity = FindingSeverity.Warning,
                                    Title = $"L'activité [{act.Code}] manque d'indicateur de suivi",
                                    Details = $"L'activité '{act.Description}' ne dispose d'aucun indicateur de performance SMART.",
                                    Recommendation = "Définissez un indicateur pour mesurer la réalisation de cette activité ainsi que son moyen de vérification.",
                                    EntityRef = act.Code
                                });
                                score -= 3;
                            }
                        }
                    }
                }
            }

            // 3. فحص الميزانية التقديرية
            decimal totalBudget = budgetItems.Sum(b => b.TotalCost);
            if (!budgetItems.Any())
            {
                report.Findings.Add(new QualityFinding
                {
                    RuleId = "BUD-01",
                    Category = "Budget",
                    Severity = FindingSeverity.Warning,
                    Title = "Budget prévisionnel vide",
                    Details = "Aucune ligne de coût n'a encore été chiffrée pour le projet.",
                    Recommendation = "Saisissez les postes de dépenses et leurs coûts dans le module Budget."
                });
                score -= 10;
            }
            else if (foundation != null && foundation.EstimatedBudget > 0 && totalBudget > foundation.EstimatedBudget)
            {
                decimal diff = totalBudget - foundation.EstimatedBudget;
                string cur = CurrencyOf(project);
                report.Findings.Add(new QualityFinding
                {
                    RuleId = "BUD-02",
                    Category = "Budget",
                    Severity = FindingSeverity.Warning,
                    Title = "Le budget dépasse le plafond stratégique estimé",
                    Details = $"Le total ({totalBudget:N0} {cur}) dépasse le plafond estimé ({foundation.EstimatedBudget:N0} {cur}) d'un écart de {diff:N0} {cur}.",
                    Recommendation = "Révisez le coût des activités ou ajustez l'estimation budgétaire avec le bailleur."
                });
                score -= 5;
            }

            // 4. Cohérence transversale entre les modules
            score -= await EvaluateCrossModuleCoherenceAsync(db, project, foundation, objectives, budgetItems, report);

            report.QualityScore = Math.Clamp(score, 0, 100);
            return report;
        }

        private static string CurrencyOf(Project project) => Project.CurrencyOf(project);

        /// <summary>
        /// Règles de cohérence entre modules (arbre à problèmes, cadre logique, chronogramme, budget,
        /// financements, risques, indicateurs, propagation). Chaque règle produit au plus une constatation
        /// agrégée. Retourne le total des points retirés.
        /// </summary>
        private static async Task<int> EvaluateCrossModuleCoherenceAsync(
            AppDbContext db,
            Project project,
            StrategicFoundation? foundation,
            List<Objective> objectives,
            List<BudgetItem> budgetItems,
            QualityReport report)
        {
            int penalty = 0;
            string cur = CurrencyOf(project);
            var activities = objectives.SelectMany(o => o.Results).SelectMany(r => r.Activities).ToList();

            void Add(string ruleId, string category, FindingSeverity severity, int points, string title, string details, string recommendation)
            {
                report.Findings.Add(new QualityFinding
                {
                    RuleId = ruleId,
                    Category = category,
                    Severity = severity,
                    Title = title,
                    Details = details,
                    Recommendation = recommendation
                });
                penalty += points;
            }

            // COH-01 : le problème central du socle stratégique doit être décliné dans l'arbre à problèmes
            var problemNodes = await db.ProblemNodes.Where(n => n.ProjectId == project.Id).ToListAsync();
            bool treeHasCore = problemNodes.Any(n => n.NodeType == "Core");
            if (foundation != null && !string.IsNullOrWhiteSpace(foundation.CoreProblemStatement) && !treeHasCore)
            {
                Add("COH-01", "Cohérence Arbre ↔ Socle", FindingSeverity.Warning, 3,
                    "Le problème central n'est pas décliné dans l'arbre à problèmes",
                    "Le socle stratégique formule un problème central, mais l'arbre à problèmes ne contient pas de nœud « problème central » ni l'analyse causes/effets correspondante.",
                    "Construisez l'arbre à problèmes à partir du problème central du socle, puis utilisez l'inversion positive pour alimenter l'arbre à objectifs.");
            }

            // COH-02 : lignes budgétaires non rattachées à une activité
            if (activities.Any())
            {
                var orphanLines = budgetItems.Where(b => !b.ActivityId.HasValue).ToList();
                if (orphanLines.Any())
                {
                    Add("COH-02", "Cohérence Budget ↔ Cadre logique", FindingSeverity.Warning, 3,
                        $"{orphanLines.Count} ligne(s) budgétaire(s) sans activité rattachée",
                        $"Montant non rattaché : {orphanLines.Sum(b => b.TotalCost):N0} {cur}. Lignes concernées : {string.Join(", ", orphanLines.Take(5).Select(b => b.Description))}{(orphanLines.Count > 5 ? "…" : "")}.",
                        "Rattachez chaque ligne de coût à l'activité qu'elle finance (hors frais de gestion/structure), afin de justifier le budget par les résultats.");
                }

                // COH-03 : activités sans aucun coût prévu (seulement si un budget existe déjà)
                if (budgetItems.Any())
                {
                    var fundedIds = budgetItems.Where(b => b.ActivityId.HasValue).Select(b => b.ActivityId!.Value).ToHashSet();
                    var unfunded = activities.Where(a => !fundedIds.Contains(a.Id)).ToList();
                    if (unfunded.Any())
                    {
                        Add("COH-03", "Cohérence Budget ↔ Cadre logique", FindingSeverity.Warning, 3,
                            $"{unfunded.Count} activité(s) sans ligne budgétaire",
                            $"Activités non chiffrées : {string.Join(", ", unfunded.Take(8).Select(a => a.Code))}{(unfunded.Count > 8 ? "…" : "")}.",
                            "Chiffrez les ressources nécessaires à chaque activité ou justifiez qu'elle est réalisée sans coût (apport en nature).");
                    }
                }
            }

            // COH-04 : total du budget détaillé vs budget global déclaré dans le cadrage
            decimal totalBudget = budgetItems.Sum(b => b.TotalCost);
            if (budgetItems.Any() && project.TotalBudget > 0 && Math.Abs(totalBudget - project.TotalBudget) > project.TotalBudget * 0.01m)
            {
                Add("COH-04", "Cohérence Budget ↔ Cadrage", FindingSeverity.Warning, 3,
                    "Le budget détaillé ne correspond pas au budget global déclaré",
                    $"Budget détaillé : {totalBudget:N0} {cur} — budget global du cadrage : {project.TotalBudget:N0} {cur} (écart {totalBudget - project.TotalBudget:N0} {cur}).",
                    "Mettez à jour le budget global de la fiche de cadrage ou ajustez les lignes budgétaires pour qu'ils concordent.");
            }

            // COH-05 : couverture du budget par les sources de financement
            var fundingSources = await db.FundingSources.Where(f => f.ProjectId == project.Id).ToListAsync();
            if (fundingSources.Any() && totalBudget > 0)
            {
                decimal funded = fundingSources.Sum(f => f.TotalAmountAllocated);
                if (funded < totalBudget * 0.99m)
                {
                    Add("COH-05", "Plan de financement", FindingSeverity.Warning, 3,
                        "Le plan de financement ne couvre pas tout le budget",
                        $"Financements identifiés : {funded:N0} {cur} pour un budget de {totalBudget:N0} {cur} (taux de couverture {funded / totalBudget:P0}).",
                        "Complétez le plan de financement (bailleur principal, cofinancements, apport propre) ou réduisez le budget.");
                }
                else if (funded > totalBudget * 1.01m)
                {
                    Add("COH-05", "Plan de financement", FindingSeverity.Info, 0,
                        "Les financements dépassent le budget",
                        $"Financements identifiés : {funded:N0} {cur} pour un budget de {totalBudget:N0} {cur}.",
                        "Vérifiez les montants alloués par chaque source de financement.");
                }
            }

            // CHR-01/02 : planification temporelle des activités
            if (activities.Any())
            {
                var undated = activities.Where(a => !a.StartDate.HasValue || !a.EndDate.HasValue).ToList();
                if (undated.Any())
                {
                    Add("CHR-01", "Chronogramme", FindingSeverity.Warning, 2,
                        $"{undated.Count} activité(s) non planifiée(s)",
                        $"Activités sans dates de début/fin : {string.Join(", ", undated.Take(8).Select(a => a.Code))}{(undated.Count > 8 ? "…" : "")}.",
                        "Planifiez chaque activité dans le chronogramme pour démontrer la faisabilité dans la durée du projet.");
                }

                var inverted = activities.Where(a => a.StartDate.HasValue && a.EndDate.HasValue && a.EndDate < a.StartDate).ToList();
                if (inverted.Any())
                {
                    Add("CHR-02", "Chronogramme", FindingSeverity.Critical, 5,
                        "Dates d'activité incohérentes (fin avant début)",
                        $"Activités concernées : {string.Join(", ", inverted.Select(a => a.Code))}.",
                        "Corrigez les dates de début et de fin de ces activités.");
                }

                if (project.StartDate.HasValue && project.EndDate.HasValue)
                {
                    var outside = activities.Where(a =>
                        (a.StartDate.HasValue && a.StartDate.Value.Date < project.StartDate.Value.Date) ||
                        (a.EndDate.HasValue && a.EndDate.Value.Date > project.EndDate.Value.Date)).ToList();
                    if (outside.Any())
                    {
                        Add("CHR-03", "Chronogramme", FindingSeverity.Warning, 3,
                            "Activités planifiées hors de la période du projet",
                            $"Période du projet : {project.StartDate:dd/MM/yyyy} – {project.EndDate:dd/MM/yyyy}. Activités hors période : {string.Join(", ", outside.Take(8).Select(a => a.Code))}{(outside.Count > 8 ? "…" : "")}.",
                            "Ramenez ces activités dans la période de mise en œuvre ou ajustez les dates du projet.");
                    }
                }
            }

            // IND-01/02 : qualité des indicateurs (tous niveaux du cadre logique)
            var objectiveIds = objectives.Select(o => o.Id).ToList();
            var resultIds = objectives.SelectMany(o => o.Results).Select(r => r.Id).ToList();
            var activityIds = activities.Select(a => a.Id).ToList();
            var indicators = await db.Indicators.Where(i =>
                    i.ProjectId == project.Id ||
                    (i.ObjectiveId.HasValue && objectiveIds.Contains(i.ObjectiveId.Value)) ||
                    (i.ResultId.HasValue && resultIds.Contains(i.ResultId.Value)) ||
                    (i.ActivityId.HasValue && activityIds.Contains(i.ActivityId.Value)))
                .ToListAsync();

            var noTarget = indicators.Where(i => string.IsNullOrWhiteSpace(i.Target)).ToList();
            if (noTarget.Any())
            {
                Add("IND-01", "Indicateurs", FindingSeverity.Warning, 3,
                    $"{noTarget.Count} indicateur(s) sans valeur cible",
                    $"Indicateurs concernés : {string.Join(", ", noTarget.Take(8).Select(i => string.IsNullOrWhiteSpace(i.Code) ? i.Description : i.Code))}{(noTarget.Count > 8 ? "…" : "")}.",
                    "Un indicateur SMART doit avoir une cible chiffrée et datée.");
            }

            var incomplete = indicators.Where(i => string.IsNullOrWhiteSpace(i.Baseline) || string.IsNullOrWhiteSpace(i.VerificationSource)).ToList();
            if (incomplete.Any())
            {
                Add("IND-02", "Indicateurs", FindingSeverity.Info, 2,
                    $"{incomplete.Count} indicateur(s) sans situation de référence ou source de vérification",
                    "La situation de référence (baseline) et la source de vérification sont exigées par la plupart des bailleurs (UE, PNUD, FEM).",
                    "Renseignez la valeur de référence et le moyen de vérification de chaque indicateur.");
            }

            // RSK-01/02 : registre des risques (module Risques ou socle stratégique)
            var risks = await db.Risks.Where(r => r.ProjectId == project.Id).ToListAsync();
            int strategicRiskCount = foundation?.Risks.Count(r => !r.IsDeleted) ?? 0;
            if (!risks.Any() && strategicRiskCount == 0)
            {
                Add("RSK-01", "Risques", FindingSeverity.Warning, 3,
                    "Aucun risque identifié",
                    "Ni le registre des risques ni le socle stratégique ne contiennent de risque.",
                    "Identifiez au moins les principaux risques (financiers, environnementaux, sociaux) avec leurs mesures d'atténuation.");
            }
            else
            {
                var criticalUnmitigated = risks.Where(r => r.Probability * r.Impact >= 16 && r.Status != "Clos" && string.IsNullOrWhiteSpace(r.MitigationMeasure)).ToList();
                if (criticalUnmitigated.Any())
                {
                    Add("RSK-02", "Risques", FindingSeverity.Warning, 4,
                        $"{criticalUnmitigated.Count} risque(s) critique(s) sans mesure d'atténuation",
                        $"Risques concernés : {string.Join(" ; ", criticalUnmitigated.Take(5).Select(r => r.Description))}.",
                        "Définissez une mesure d'atténuation et un responsable pour chaque risque critique (probabilité × impact ≥ 16, échelle du registre des risques).");
                }
            }

            // SYNC-01 : éléments dérivés à revoir après une modification du socle stratégique
            int staleLinks = await db.StrategicLinks.CountAsync(l => l.ProjectId == project.Id && l.Status == "Stale" && !l.IsDeleted);
            if (staleLinks > 0)
            {
                Add("SYNC-01", "Propagation", FindingSeverity.Warning, 2,
                    $"{staleLinks} élément(s) dérivé(s) à revoir",
                    "Le socle stratégique a été modifié après la génération de ces éléments du cadre logique ou du budget.",
                    "Ouvrez la Planification Stratégique et validez ou ignorez les propositions de mise à jour.");
            }

            // Données présentes à plusieurs étapes : une seule valeur doit faire foi (ADR 25)
            var territory = await db.TerritoryProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.ProjectId == project.Id);
            if (territory?.HasLocation == true && !string.IsNullOrWhiteSpace(project.InterventionZone) &&
                !Research.ProjectIntegrationService.SameMeaning(project.InterventionZone, territory.ZoneLabel) &&
                !Research.ProjectIntegrationService.Key(territory.ZoneLabel).Contains(Research.ProjectIntegrationService.Key(project.InterventionZone)))
            {
                Add("COH-06", "Cohérence Cadrage ↔ Territoire", FindingSeverity.Warning, 2,
                    "La zone d'intervention du cadrage diffère du profil territorial",
                    $"Cadrage : « {project.InterventionZone} » — Territoire : « {territory.ZoneLabel} ».",
                    "La zone se définit dans « Territoire & bénéficiaires » ; enregistrez-y la bonne localisation pour mettre à jour le cadrage.");
            }

            if (foundation != null && !string.IsNullOrWhiteSpace(foundation.Currency) && !string.IsNullOrWhiteSpace(project.Currency) &&
                !string.Equals(foundation.Currency.Trim(), project.Currency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Add("COH-07", "Cohérence Socle ↔ Cadrage", FindingSeverity.Warning, 2,
                    "Devises différentes entre le socle stratégique et le cadrage",
                    $"Socle stratégique : {foundation.Currency} — cadrage et budget : {project.Currency}. Les montants ne sont pas comparables.",
                    "Utilisez une seule devise pour tout le projet (celle du budget et du bailleur).");
            }

            int? projectMonths = project.StartDate.HasValue && project.EndDate.HasValue && project.EndDate > project.StartDate
                ? (int)Math.Round((project.EndDate.Value - project.StartDate.Value).TotalDays / 30.44)
                : null;
            if (foundation != null && projectMonths.HasValue && foundation.EstimatedDurationMonths > 0 &&
                Math.Abs(projectMonths.Value - foundation.EstimatedDurationMonths) > 1)
            {
                Add("COH-08", "Cohérence Socle ↔ Cadrage", FindingSeverity.Info, 1,
                    "Durée du projet différente entre le socle et les dates du cadrage",
                    $"Socle stratégique : {foundation.EstimatedDurationMonths} mois — dates du cadrage : {projectMonths} mois.",
                    "Alignez la durée estimée du socle sur les dates de début et de fin du projet.");
            }

            // DUP-01 : éléments en double dans le cadre logique (même libellé)
            var duplicateLabels = objectives.Select(o => ("Objectif", o.Code, o.Description))
                .Concat(objectives.SelectMany(o => o.Results).Select(r => ("Résultat", r.Code, r.Description)))
                .Concat(activities.Select(a => ("Activité", a.Code, a.Description)))
                .Concat(indicators.Select(i => ("Indicateur", i.Code, i.Description)))
                .Where(x => Research.ProjectIntegrationService.Key(x.Item3).Length >= 8)
                .GroupBy(x => (x.Item1, Research.ProjectIntegrationService.Key(x.Item3)))
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateLabels.Any())
            {
                Add("DUP-01", "Doublons", FindingSeverity.Warning, Math.Min(6, duplicateLabels.Count * 2),
                    $"{duplicateLabels.Count} élément(s) du cadre logique en double",
                    string.Join(" ; ", duplicateLabels.Take(6).Select(g => $"{g.Key.Item1} « {Research.ProjectIntegrationService.Shorten(g.First().Item3, 60)} » ({string.Join(", ", g.Select(x => string.IsNullOrWhiteSpace(x.Item2) ? "sans code" : x.Item2))})")),
                    "Fusionnez les éléments en double (une propagation répétée ou une saisie manuelle peut les avoir créés).");
            }

            // TER / BEN : territoire et bénéficiaires, base du diagnostic et du ciblage
            var beneficiaries = await db.BeneficiaryGroups.AsNoTracking().Where(b => b.ProjectId == project.Id).ToListAsync();
            if (territory == null || !territory.HasLocation)
            {
                Add("TER-01", "Territoire", FindingSeverity.Warning, 3,
                    "Zone d'intervention non décrite",
                    "Le profil du territoire (localisation, milieu, population, problèmes) n'est pas renseigné.",
                    "Complétez « Territoire & bénéficiaires » dans la Planification Stratégique (l'assistant IA peut proposer un premier diagnostic).");
            }
            else if (territory.FilledDiagnosticSections < 4)
            {
                Add("TER-02", "Territoire", FindingSeverity.Info, 1,
                    "Diagnostic territorial succinct",
                    $"{territory.FilledDiagnosticSections} rubrique(s) de diagnostic sur 12 renseignée(s).",
                    "Décrivez au moins le milieu naturel, la population, les activités économiques et les principaux problèmes de la zone.");
            }

            var direct = beneficiaries.Where(b => b.IsDirect).ToList();
            if (!direct.Any())
            {
                Add("BEN-01", "Bénéficiaires", FindingSeverity.Warning, 3,
                    "Aucun bénéficiaire direct identifié",
                    beneficiaries.Any() ? "Seuls des bénéficiaires indirects sont décrits." : "Aucun groupe de bénéficiaires n'est décrit.",
                    "Identifiez les groupes qui bénéficient directement des activités, avec leur effectif estimé.");
            }
            else
            {
                if (direct.Any(b => !b.EstimatedCount.HasValue))
                {
                    Add("BEN-02", "Bénéficiaires", FindingSeverity.Info, 1,
                        "Effectif de bénéficiaires directs non estimé",
                        $"Groupes sans effectif : {string.Join(", ", direct.Where(b => !b.EstimatedCount.HasValue).Select(b => b.Name).Take(6))}.",
                        "Les bailleurs demandent le nombre de bénéficiaires directs ; il sert aussi de cible aux indicateurs.");
                }
                if (direct.All(b => !b.WomenPercent.HasValue))
                {
                    Add("BEN-03", "Bénéficiaires", FindingSeverity.Info, 1,
                        "Pas de ventilation par sexe des bénéficiaires",
                        "Aucune part de femmes n'est indiquée pour les bénéficiaires directs.",
                        "Indiquez la part des femmes (et des jeunes) : l'approche genre est un critère d'évaluation de la plupart des bailleurs.");
                }
            }

            // DOC : conformité aux exigences des appels à projets analysés
            foreach (var call in Research.DocumentIntelligenceService.LoadCallRequirements(db, project.Id))
            {
                string callName = string.IsNullOrWhiteSpace(call.Title) ? "l'appel à projets" : $"« {Research.ProjectIntegrationService.Shorten(call.Title, 80)} »";
                bool sameCurrency = string.IsNullOrWhiteSpace(call.Currency) || string.Equals(call.Currency, cur, StringComparison.OrdinalIgnoreCase);
                decimal requested = totalBudget > 0 ? totalBudget : project.TotalBudget;

                if (call.BudgetMax.HasValue && sameCurrency && requested > 0)
                {
                    decimal ownShare = project.LocalContribution + project.PartnerContribution;
                    decimal askedFromDonor = Math.Max(0, requested - ownShare);
                    if (askedFromDonor > call.BudgetMax.Value)
                        Add("DOC-01", "Conformité à l'appel", FindingSeverity.Critical, 6,
                            $"Montant demandé supérieur au plafond de {callName}",
                            $"Montant demandé au bailleur : {askedFromDonor:N0} {cur} — plafond : {call.BudgetMax:N0} {cur}.",
                            "Réduisez le budget, augmentez les cofinancements ou choisissez un autre appel.");
                    else if (call.BudgetMin.HasValue && call.BudgetMin < call.BudgetMax && askedFromDonor < call.BudgetMin.Value)
                        Add("DOC-01", "Conformité à l'appel", FindingSeverity.Warning, 3,
                            $"Montant demandé inférieur au minimum de {callName}",
                            $"Montant demandé : {askedFromDonor:N0} {cur} — minimum : {call.BudgetMin:N0} {cur}.",
                            "Vérifiez le montant minimum éligible.");
                }

                if (call.MaxDurationMonths.HasValue && projectMonths.HasValue && projectMonths.Value > call.MaxDurationMonths.Value)
                    Add("DOC-02", "Conformité à l'appel", FindingSeverity.Critical, 5,
                        $"Durée du projet supérieure au maximum de {callName}",
                        $"Durée prévue : {projectMonths} mois — maximum : {call.MaxDurationMonths} mois.",
                        "Raccourcissez la période de mise en œuvre ou le chronogramme.");

                if (call.DeadlineDate.HasValue)
                {
                    int days = (int)(call.DeadlineDate.Value.Date - DateTime.Today).TotalDays;
                    if (days < 0)
                        Add("DOC-03", "Conformité à l'appel", FindingSeverity.Warning, 2,
                            $"Date limite de {callName} dépassée",
                            $"La date limite ({call.DeadlineDate:dd/MM/yyyy}) est passée depuis {-days} jour(s).",
                            "Vérifiez s'il existe une nouvelle session de l'appel, ou ciblez un autre bailleur.");
                    else if (days <= 30)
                        Add("DOC-03", "Conformité à l'appel", FindingSeverity.Info, 0,
                            $"Date limite de {callName} dans {days} jour(s)",
                            $"Date limite : {call.DeadlineDate:dd/MM/yyyy}.",
                            "Priorisez les étapes non terminées et l'export du dossier.");
                }

                if (call.CofinancingPercent is > 0 && requested > 0)
                {
                    decimal share = (project.LocalContribution + project.PartnerContribution) / requested * 100m;
                    if (share + 0.5m < call.CofinancingPercent.Value)
                        Add("DOC-04", "Conformité à l'appel", FindingSeverity.Warning, 3,
                            $"Cofinancement insuffisant pour {callName}",
                            $"Cofinancement prévu : {share:0.#} % — exigé : {call.CofinancingPercent:0.#} %.",
                            "Augmentez l'apport propre ou celui des partenaires (en numéraire ou en nature valorisée).");
                }
            }

            return penalty;
        }
    }
}

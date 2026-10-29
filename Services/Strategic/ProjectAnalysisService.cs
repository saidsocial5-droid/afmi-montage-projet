using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services.Strategic
{
    public class StageReadiness
    {
        public LifecycleStage Stage { get; set; }
        public string Label { get; set; } = string.Empty;
        public StageStatus Status { get; set; }
        public bool IsPassed { get; set; }
        public bool IsOverridden { get; set; }
        public List<string> BlockingGaps { get; set; } = new();

        public string StatusLabel => Status switch
        {
            StageStatus.Completed => "Terminé",
            StageStatus.InProgress => "En cours",
            StageStatus.NeedsReview => "À réviser",
            _ => "Non démarré"
        };

        public string StatusColor => Status switch
        {
            StageStatus.Completed => "#10B981",
            StageStatus.InProgress => "#0284C7",
            StageStatus.NeedsReview => "#F59E0B",
            _ => "#94A3B8"
        };

        public string GapsText => BlockingGaps.Count == 0
            ? (IsOverridden ? "Dérogation enregistrée." : "Aucune lacune bloquante.")
            : string.Join(Environment.NewLine, BlockingGaps.Select(g => "• " + g));
    }

    /// <summary>Résultat de l'analyse globale (déterministe, hors IA) d'un projet.</summary>
    public class ProjectAnalysisResult
    {
        public int ProjectId { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.Now;
        public QualityReport Quality { get; set; } = new();
        public List<StageReadiness> Stages { get; set; } = new();

        public int CompletedStages => Stages.Count(s => s.Status == StageStatus.Completed);

        /// <summary>Avancement du montage (étapes terminées / étapes évaluées), en %.</summary>
        public int ReadinessPercent => Stages.Count == 0 ? 0 : (int)Math.Round(100.0 * CompletedStages / Stages.Count);

        public string ScoreLabel => Quality.QualityScore switch
        {
            >= 85 => "Dossier solide",
            >= 65 => "Dossier à consolider",
            >= 40 => "Dossier fragile",
            _ => "Dossier incomplet"
        };

        /// <summary>
        /// Synthèse textuelle des constats, destinée à être fournie à l'IA comme base factuelle
        /// (l'IA explique et priorise ; elle ne redétecte pas ce que les règles ont déjà établi).
        /// </summary>
        public string ToPromptSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Score de qualité déterministe : {Quality.QualityScore}/100 ({ScoreLabel}) — {Quality.CriticalCount} critique(s), {Quality.WarningCount} avertissement(s), {Quality.InfoCount} information(s).");
            sb.AppendLine($"Avancement du montage : {CompletedStages}/{Stages.Count} étapes terminées ({ReadinessPercent} %).");

            var openStages = Stages.Where(s => s.BlockingGaps.Any()).ToList();
            if (openStages.Any())
            {
                sb.AppendLine("Lacunes par étape :");
                foreach (var s in openStages)
                    sb.AppendLine($"- {s.Label} : {string.Join(" ; ", s.BlockingGaps)}");
            }

            if (Quality.Findings.Any())
            {
                sb.AppendLine("Constats des règles RBM et de cohérence transversale :");
                foreach (var f in Quality.Findings.OrderByDescending(f => f.Severity))
                    sb.AppendLine($"- [{f.Severity}] [{f.RuleId}] {f.Title} — {f.Details}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Analyse globale du projet : agrège le contrôle qualité déterministe (règles RBM + cohérence entre modules)
    /// et l'état des portes de maturité des 9 étapes. Point d'entrée unique pour l'écran d'audit et l'audit IA.
    /// </summary>
    public class ProjectAnalysisService
    {
        private static ProjectAnalysisService? _instance;
        public static ProjectAnalysisService Instance => _instance ??= new ProjectAnalysisService();

        private static readonly (LifecycleStage Stage, string Label)[] StageLabels =
        {
            (LifecycleStage.StrategicPlanning, "1. Planification stratégique"),
            (LifecycleStage.Cadrage, "2. Cadrage"),
            (LifecycleStage.ProblemTree, "3. Arbre à problèmes"),
            (LifecycleStage.CadreLogique, "4. Cadre logique"),
            (LifecycleStage.Chronogramme, "5. Chronogramme"),
            (LifecycleStage.Budget, "6. Budget prévisionnel"),
            (LifecycleStage.Canvas, "7. Canevas bailleur"),
            (LifecycleStage.Collaboration, "8. Espace collaboratif"),
            (LifecycleStage.Export, "9. Exports"),
        };

        private readonly DeterministicQualityChecker _qualityChecker;
        private readonly ReadinessGateService _gateService;
        private readonly PropagationEngineService _propagation;

        public ProjectAnalysisService(Func<AppDbContext>? dbFactory = null)
        {
            if (dbFactory == null)
            {
                _qualityChecker = DeterministicQualityChecker.Instance;
                _gateService = ReadinessGateService.Instance;
                _propagation = PropagationEngineService.Instance;
            }
            else
            {
                _qualityChecker = new DeterministicQualityChecker(dbFactory);
                _gateService = new ReadinessGateService(dbFactory);
                _propagation = new PropagationEngineService(dbFactory);
            }
        }

        public async Task<ProjectAnalysisResult> AnalyzeAsync(int projectId)
        {
            // Statuts des liens à jour (une modification faite dans un autre écran peut avoir rendu un élément Stale/Overridden)
            try
            {
                await _propagation.RefreshLinkStatusesAsync(projectId);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(ProjectAnalysisService), "Recalcul des liens de propagation impossible avant l'analyse.", ex);
            }

            var result = new ProjectAnalysisResult
            {
                ProjectId = projectId,
                Quality = await _qualityChecker.EvaluateProjectQualityAsync(projectId)
            };

            foreach (var (stage, label) in StageLabels)
            {
                var gate = await _gateService.EvaluateStageAsync(projectId, stage);
                result.Stages.Add(new StageReadiness
                {
                    Stage = stage,
                    Label = label,
                    Status = gate.Status,
                    IsPassed = gate.IsPassed,
                    IsOverridden = gate.IsOverridden,
                    BlockingGaps = gate.BlockingGaps
                });
            }

            AddSequenceFindings(result);
            return result;
        }

        /// <summary>
        /// SEQ-01 : une étape non démarrée alors qu'une étape postérieure est déjà avancée signifie que le montage
        /// a sauté une étape (ex. budget saisi sans cadre logique) : ce qui suit ne repose pas sur ce qui précède.
        /// L'espace collaboratif et les exports, transversaux, sont exclus. Une dérogation enregistrée lève le constat.
        /// </summary>
        public static void AddSequenceFindings(ProjectAnalysisResult result)
        {
            var ordered = result.Stages
                .Where(s => s.Stage is not LifecycleStage.Collaboration and not LifecycleStage.Export)
                .OrderBy(s => (int)s.Stage)
                .ToList();

            int penalty = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                var stage = ordered[i];
                if (stage.Status != StageStatus.NotStarted || stage.IsOverridden) continue;
                var later = ordered.Skip(i + 1).Where(s => s.Status is StageStatus.InProgress or StageStatus.Completed or StageStatus.NeedsReview).ToList();
                if (!later.Any()) continue;

                result.Quality.Findings.Add(new QualityFinding
                {
                    RuleId = "SEQ-01",
                    Category = "Ordre des étapes",
                    Severity = FindingSeverity.Warning,
                    Title = $"Étape « {stage.Label} » non réalisée alors que des étapes suivantes sont avancées",
                    Details = $"Étapes déjà entamées après elle : {string.Join(", ", later.Select(s => s.Label))}. Leur contenu ne s'appuie pas sur cette étape.",
                    Recommendation = "Réalisez cette étape (ou enregistrez une dérogation motivée), puis révisez les étapes suivantes à partir de son résultat.",
                    EntityRef = stage.Stage.ToString()
                });
                penalty += 3;
            }
            result.Quality.QualityScore = Math.Clamp(result.Quality.QualityScore - Math.Min(penalty, 12), 0, 100);
        }
    }
}

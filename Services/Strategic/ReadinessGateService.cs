using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Security;

namespace AFMI_Montage_projet_2.Services.Strategic
{
    public enum LifecycleStage
    {
        StrategicPlanning = 1,
        Cadrage = 2,
        ProblemTree = 3,
        CadreLogique = 4,
        Chronogramme = 5,
        Budget = 6,
        Canvas = 7,
        Collaboration = 8,
        Export = 9
    }

    public enum StageStatus
    {
        NotStarted,
        InProgress,
        Completed,
        NeedsReview
    }

    public class GateEvaluationResult
    {
        public LifecycleStage Stage { get; set; }
        public bool IsPassed { get; set; }
        public StageStatus Status { get; set; } = StageStatus.NotStarted;
        public List<string> BlockingGaps { get; set; } = new();
        public bool IsOverridden { get; set; }
        public string? OverrideReason { get; set; }
        public DateTime? OverriddenAt { get; set; }
    }

    public class ReadinessGateService
    {
        private static ReadinessGateService? _instance;
        public static ReadinessGateService Instance => _instance ??= new ReadinessGateService();

        private readonly Func<AppDbContext> _dbFactory;

        public ReadinessGateService()
        {
            _dbFactory = () => new AppDbContext();
        }

        public ReadinessGateService(Func<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// تقييم جاهزية المرحلة وفحص النواقص المانعة
        /// </summary>
        public async Task<GateEvaluationResult> EvaluateStageAsync(int projectId, LifecycleStage stage)
        {
            using var db = _dbFactory();
            var result = new GateEvaluationResult { Stage = stage };

            // فحص هل هناك تجاوز مسجل لهذه المرحلة في سجل التدقيق
            string overrideAction = $"OVERRIDE_GATE_{stage}";
            var lastOverride = await db.AuditLogEntries
                .Where(a => a.ProjectId == projectId && a.ActionType == overrideAction && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOverride != null)
            {
                result.IsOverridden = true;
                result.OverrideReason = lastOverride.OverrideReason;
                result.OverriddenAt = lastOverride.CreatedAt;
            }

            var project = await db.Projects
                .Include(p => p.StrategicFoundation)
                .Include(p => p.Objectives)
                    .ThenInclude(o => o.Results)
                        .ThenInclude(r => r.Activities)
                            .ThenInclude(a => a.Indicators)
                .Include(p => p.BudgetItems)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                result.BlockingGaps.Add("Le projet n'existe pas dans la base de données.");
                result.Status = StageStatus.NotStarted;
                result.IsPassed = false;
                return result;
            }

            // فحص هل هناك عناصر موسومة بـ Stale
            bool hasStaleLinks = await db.StrategicLinks
                .AnyAsync(l => l.ProjectId == projectId && l.Status == "Stale" && !l.IsDeleted);

            switch (stage)
            {
                case LifecycleStage.StrategicPlanning:
                    EvaluateStrategicPlanning(db, project, result);
                    break;

                case LifecycleStage.Cadrage:
                    EvaluateCadrage(project, result);
                    break;

                case LifecycleStage.ProblemTree:
                    await EvaluateProblemTreeAsync(db, project.Id, result);
                    break;

                case LifecycleStage.CadreLogique:
                    EvaluateCadreLogique(project, result);
                    break;

                case LifecycleStage.Chronogramme:
                    EvaluateChronogramme(project, result);
                    break;

                case LifecycleStage.Budget:
                    EvaluateBudget(project, result);
                    break;

                case LifecycleStage.Canvas:
                    await EvaluateCanvasAsync(db, project.Id, result);
                    break;

                case LifecycleStage.Collaboration:
                    result.IsPassed = true;
                    result.Status = StageStatus.Completed;
                    break;

                case LifecycleStage.Export:
                    EvaluateExport(project, result);
                    break;
            }

            if (hasStaleLinks && result.Status == StageStatus.Completed)
            {
                result.Status = StageStatus.NeedsReview;
            }

            result.IsPassed = result.BlockingGaps.Count == 0 || result.IsOverridden;
            return result;
        }

        private void EvaluateStrategicPlanning(AppDbContext db, Project project, GateEvaluationResult result)
        {
            var sf = db.StrategicFoundations
                .Include(f => f.StrategicObjectives)
                .Include(f => f.Stakeholders)
                .Include(f => f.Risks)
                .FirstOrDefault(f => f.ProjectId == project.Id);

            if (sf == null)
            {
                result.BlockingGaps.Add("Le socle stratégique du projet n'a pas encore été initialisé.");
                AddTerritoryGaps(db, project, result);
                result.Status = StageStatus.NotStarted;
                return;
            }

            if (string.IsNullOrWhiteSpace(sf.IdeaTitle))
                result.BlockingGaps.Add("L'intitulé de l'idée de projet n'est pas renseigné.");
            if (string.IsNullOrWhiteSpace(sf.CoreProblemStatement))
                result.BlockingGaps.Add("La formulation du problème central est requise.");
            if (!sf.StrategicObjectives.Any(o => !o.IsDeleted))
                result.BlockingGaps.Add("Au moins un objectif stratégique doit être défini.");
            if (!sf.Stakeholders.Any(s => !s.IsDeleted))
                result.BlockingGaps.Add("Au moins une partie prenante doit être enregistrée.");
            if (!sf.Risks.Any(r => !r.IsDeleted))
                result.BlockingGaps.Add("Au moins un risque avec sa mesure d'atténuation doit être enregistré.");

            AddTerritoryGaps(db, project, result);

            if (result.BlockingGaps.Count == 0)
                result.Status = StageStatus.Completed;
            else if (result.BlockingGaps.Count < 7)
                result.Status = StageStatus.InProgress;
            else
                result.Status = StageStatus.NotStarted;
        }

        /// <summary>Territoire et bénéficiaires : socle de toutes les étapes suivantes (ADR 25).</summary>
        private static void AddTerritoryGaps(AppDbContext db, Project project, GateEvaluationResult result)
        {
            var territory = db.TerritoryProfiles.FirstOrDefault(t => t.ProjectId == project.Id);
            if (territory == null || !territory.HasLocation)
                result.BlockingGaps.Add("La zone d'intervention (région, province ou communes) n'est pas renseignée dans « Territoire & bénéficiaires ».");
            if (!db.BeneficiaryGroups.Any(b => b.ProjectId == project.Id && b.BeneficiaryType == BeneficiaryTypes.Direct))
                result.BlockingGaps.Add("Au moins un groupe de bénéficiaires directs doit être identifié dans « Territoire & bénéficiaires ».");
        }

        private void EvaluateCadrage(Project project, GateEvaluationResult result)
        {
            if (string.IsNullOrWhiteSpace(project.Title))
                result.BlockingGaps.Add("Le titre du projet dans la fiche de cadrage n'est pas renseigné.");
            if (string.IsNullOrWhiteSpace(project.GeneralObjective))
                result.BlockingGaps.Add("L'objectif général du projet n'est pas renseigné (الهدف العام).");
            if (!project.Objectives.Any())
                result.BlockingGaps.Add("Au moins un objectif spécifique (OS) doit être défini.");

            if (result.BlockingGaps.Count == 0)
                result.Status = StageStatus.Completed;
            else if (result.BlockingGaps.Count < 3)
                result.Status = StageStatus.InProgress;
            else
                result.Status = StageStatus.NotStarted;
        }

        private async Task EvaluateProblemTreeAsync(AppDbContext db, int projectId, GateEvaluationResult result)
        {
            var nodes = await db.ProblemNodes.Where(n => n.ProjectId == projectId).ToListAsync();
            var core = nodes.FirstOrDefault(n => n.NodeType == "Core");
            if (core == null)
            {
                result.BlockingGaps.Add("Le problème central n'est pas défini dans l'arbre à problèmes.");
                result.Status = StageStatus.NotStarted;
                return;
            }

            bool hasCause = nodes.Any(n => n.NodeType == "Cause");
            bool hasConsequence = nodes.Any(n => n.NodeType == "Consequence");

            if (!hasCause) result.BlockingGaps.Add("Au moins une cause doit être ajoutée dans l'arbre à problèmes.");
            if (!hasConsequence) result.BlockingGaps.Add("Au moins une conséquence/effet doit être ajoutée dans l'arbre à problèmes.");

            if (result.BlockingGaps.Count == 0)
                result.Status = StageStatus.Completed;
            else
                result.Status = StageStatus.InProgress;
        }

        private void EvaluateCadreLogique(Project project, GateEvaluationResult result)
        {
            var results = project.Objectives.SelectMany(o => o.Results).ToList();
            if (!results.Any())
                result.BlockingGaps.Add("Au moins un résultat attendu (Résultat) doit être saisi.");

            var activities = results.SelectMany(r => r.Activities).ToList();
            if (!activities.Any())
                result.BlockingGaps.Add("Au moins une activité opérationnelle (Activité) doit être saisie.");

            var indicators = activities.SelectMany(a => a.Indicators).ToList();
            if (!indicators.Any())
                result.BlockingGaps.Add("Au moins un indicateur de performance (Indicateur) doit être saisi.");

            if (result.BlockingGaps.Count == 0)
                result.Status = StageStatus.Completed;
            else if (results.Any() || activities.Any())
                result.Status = StageStatus.InProgress;
            else
                result.Status = StageStatus.NotStarted;
        }

        private void EvaluateChronogramme(Project project, GateEvaluationResult result)
        {
            var activities = project.Objectives.SelectMany(o => o.Results).SelectMany(r => r.Activities).ToList();
            if (!activities.Any())
            {
                result.BlockingGaps.Add("Aucune activité à planifier dans le chronogramme.");
                result.Status = StageStatus.NotStarted;
                return;
            }

            int scheduled = activities.Count(a => a.StartDate.HasValue && a.EndDate.HasValue);
            if (scheduled < activities.Count)
            {
                result.BlockingGaps.Add($"Il y a {activities.Count - scheduled} activité(s) sans dates de début et de fin définies.");
                result.Status = StageStatus.InProgress;
            }
            else
            {
                result.Status = StageStatus.Completed;
            }
        }

        private void EvaluateBudget(Project project, GateEvaluationResult result)
        {
            if (!project.BudgetItems.Any())
            {
                result.BlockingGaps.Add("Aucune ligne n'a été saisie dans le budget prévisionnel.");
                result.Status = StageStatus.NotStarted;
                return;
            }

            decimal totalItems = project.BudgetItems.Sum(b => b.TotalCost);
            if (totalItems <= 0)
            {
                result.BlockingGaps.Add("Le montant total du budget prévisionnel est égal à zéro.");
                result.Status = StageStatus.InProgress;
            }
            else
            {
                result.Status = StageStatus.Completed;
            }
        }

        private async Task EvaluateCanvasAsync(AppDbContext db, int projectId, GateEvaluationResult result)
        {
            var instance = await db.FormInstances.FirstOrDefaultAsync(f => f.ProjectId == projectId);
            if (instance == null || string.IsNullOrWhiteSpace(instance.JsonAnswers) || instance.JsonAnswers == "{}")
            {
                result.BlockingGaps.Add("Aucun canevas bailleur sélectionné ou les réponses ne sont pas encore renseignées.");
                result.Status = StageStatus.NotStarted;
            }
            else
            {
                result.Status = StageStatus.Completed;
            }
        }

        private void EvaluateExport(Project project, GateEvaluationResult result)
        {
            if (string.IsNullOrWhiteSpace(project.Title))
                result.BlockingGaps.Add("Le titre du projet est manquant pour l'exportation.");
            if (!project.Objectives.Any())
                result.BlockingGaps.Add("Les objectifs sont incomplets pour l'exportation.");

            result.Status = result.BlockingGaps.Count == 0 ? StageStatus.Completed : StageStatus.InProgress;
        }

        /// <summary>
        /// تسجيل تجاوز إداري مبرر مع التوثيق في سجل التدقيق
        /// </summary>
        public async Task<bool> RecordGateOverrideAsync(int projectId, LifecycleStage stage, string reason, string userEmail)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Le motif de dérogation est obligatoire pour la traçabilité dans le journal d'audit.", nameof(reason));

            using var db = _dbFactory();
            var logEntry = new AuditLogEntry
            {
                ProjectId = projectId,
                ActionType = $"OVERRIDE_GATE_{stage}",
                UserEmail = userEmail,
                Details = $"Dérogation administrative accordée pour l'étape {stage}",
                OverrideReason = reason,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userEmail
            };

            db.AuditLogEntries.Add(logEntry);
            await db.SaveChangesAsync();

            AuditLogger.Log($"Dérogation porte {stage}", $"Motif : {reason}", userEmail);
            return true;
        }
    }
}

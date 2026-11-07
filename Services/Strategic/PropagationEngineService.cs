using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;

namespace AFMI_Montage_projet_2.Services.Strategic
{
    public class ProposedPropagationItem
    {
        public string ItemKey { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceEntityName { get; set; } = string.Empty;
        public int SourceEntityId { get; set; }
        public string SourceLabel { get; set; } = string.Empty;
        public string TargetEntityName { get; set; } = string.Empty;
        public int? TargetEntityId { get; set; }
        public string TargetLabel { get; set; } = string.Empty;
        public string ProposedContent { get; set; } = string.Empty;
        public string? CurrentContent { get; set; }
        public bool IsSelected { get; set; } = true;
        public string ActionType { get; set; } = "Create"; // Create, Update, Alert
        public string DiffSummary { get; set; } = string.Empty;
    }

    public class PropagationSummary
    {
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    /// <summary>Bilan d'un recalcul des statuts de liens (ADR 4).</summary>
    public class LinkStatusRefreshResult
    {
        public int SyncedCount { get; set; }
        public int StaleCount { get; set; }
        public int OverriddenCount { get; set; }
        public int OrphanCount { get; set; }
        public int ChangedCount { get; set; }
    }

    /// <summary>
    /// Moteur de propagation : alimente les étapes aval (cadrage, arbre à problèmes, cadre logique)
    /// à partir du socle stratégique, avec validation humaine et traçabilité par StrategicLink.
    /// Statuts des liens (empreintes SHA-256, ADR 4) :
    ///  - Synced     : source et cible identiques au dernier alignement ;
    ///  - Stale      : la source stratégique a changé depuis → la cible est à revoir ;
    ///  - Overridden : la cible a été modifiée à la main → elle est protégée de tout écrasement automatique.
    /// </summary>
    public class PropagationEngineService
    {
        private static PropagationEngineService? _instance;
        public static PropagationEngineService Instance => _instance ??= new PropagationEngineService();

        private const string SourceCoreProblem = "CoreProblem";
        private const string BadgeDerived = "Dérivé du socle stratégique (GAR)";

        private readonly Func<AppDbContext> _dbFactory;
        private readonly ImpactAnalysisService _impactService;

        public PropagationEngineService(Func<AppDbContext>? dbFactory = null)
        {
            _dbFactory = dbFactory ?? (() => new AppDbContext());
            _impactService = new ImpactAnalysisService(_dbFactory);
        }

        private static string ComputeHash(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
            return Convert.ToHexString(bytes);
        }

        private static string ObjectiveContent(StrategicObjective obj) =>
            string.IsNullOrWhiteSpace(obj.Description) ? obj.Title : $"{obj.Title} - {obj.Description}";

        private static string Currency(Project? p) => Project.CurrencyOf(p);

        /// <summary>
        /// Génère l'aperçu des propositions dérivées du socle stratégique pour l'étape cible.
        /// </summary>
        public async Task<List<ProposedPropagationItem>> GenerateProposalsAsync(int projectId, LifecycleStage targetStage)
        {
            var proposals = new List<ProposedPropagationItem>();

            using var db = _dbFactory();
            var foundation = await db.StrategicFoundations
                .Include(f => f.StrategicObjectives)
                .Include(f => f.NeedsAssessments)
                .Include(f => f.TheoryOfChangeNodes)
                .Include(f => f.Stakeholders)
                .Include(f => f.ContextIndicators)
                .FirstOrDefaultAsync(f => f.ProjectId == projectId && !f.IsDeleted);

            if (foundation == null) return proposals;

            var existingLinks = await db.StrategicLinks
                .Where(l => l.ProjectId == projectId && !l.IsDeleted)
                .ToListAsync();

            StrategicLink? FindLink(string sourceType, int sourceId, string targetType) =>
                existingLinks.FirstOrDefault(l => l.SourceEntityType == sourceType && l.SourceEntityId == sourceId && l.TargetEntityType == targetType);

            string SummaryFor(StrategicLink? link, bool exists, string createText, string updateText) =>
                !exists ? createText
                : link?.Status == "Overridden" ? "⚠️ Modifié manuellement (protégé) — sélection facultative"
                : updateText;

            switch (targetStage)
            {
                case LifecycleStage.Cadrage:
                    // 1. Objectif général
                    if (!string.IsNullOrWhiteSpace(foundation.GeneralStrategicObjective))
                    {
                        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                        bool hasCurrent = !string.IsNullOrWhiteSpace(project?.GeneralObjective);
                        if (project?.GeneralObjective?.Trim() != foundation.GeneralStrategicObjective.Trim())
                        {
                            proposals.Add(new ProposedPropagationItem
                            {
                                SourceEntityName = nameof(StrategicFoundation),
                                SourceEntityId = foundation.Id,
                                SourceLabel = "Objectif général stratégique",
                                TargetEntityName = "Project.GeneralObjective",
                                TargetLabel = "Objectif général du projet (cadrage)",
                                ProposedContent = foundation.GeneralStrategicObjective,
                                CurrentContent = project?.GeneralObjective,
                                ActionType = hasCurrent ? "Update" : "Create",
                                // Ne pas écraser par défaut un objectif général déjà rédigé
                                IsSelected = !hasCurrent,
                                DiffSummary = hasCurrent
                                    ? "Remplacer l'objectif général actuel par la formulation stratégique validée"
                                    : "Définir l'objectif général à partir de la planification stratégique"
                            });
                        }
                    }

                    // 2. Objectifs spécifiques (OS1, OS2...)
                    int osIdx = 1;
                    foreach (var obj in foundation.StrategicObjectives.Where(o => !o.IsDeleted && o.IsSpecificObjective))
                    {
                        var code = $"OS{osIdx++}";
                        var link = FindLink(nameof(StrategicObjective), obj.Id, nameof(Objective));
                        Objective? existingTarget = link != null
                            ? await db.Objectives.FirstOrDefaultAsync(o => o.Id == link.TargetEntityId)
                            : null;
                        string content = ObjectiveContent(obj);
                        if (existingTarget != null && existingTarget.Description.Trim() == content.Trim()) continue; // déjà aligné

                        proposals.Add(new ProposedPropagationItem
                        {
                            SourceEntityName = nameof(StrategicObjective),
                            SourceEntityId = obj.Id,
                            SourceLabel = $"{obj.Code} : {obj.Title}",
                            TargetEntityName = nameof(Objective),
                            TargetEntityId = existingTarget?.Id,
                            TargetLabel = $"Objectif spécifique ({existingTarget?.Code ?? code})",
                            ProposedContent = content,
                            CurrentContent = existingTarget?.Description,
                            ActionType = existingTarget == null ? "Create" : "Update",
                            IsSelected = link?.Status != "Overridden",
                            DiffSummary = SummaryFor(link, existingTarget != null,
                                $"Créer l'objectif spécifique {code} lié au socle stratégique",
                                "Aligner la formulation sur la planification stratégique")
                        });
                    }
                    break;

                case LifecycleStage.ProblemTree:
                    if (!string.IsNullOrWhiteSpace(foundation.CoreProblemStatement))
                    {
                        var coreNode = await db.ProblemNodes.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.NodeType == "Core");
                        if (coreNode != null && coreNode.Description.Trim() == foundation.CoreProblemStatement.Trim()) break;
                        var link = coreNode == null ? null : FindLink(SourceCoreProblem, foundation.Id, nameof(ProblemNode));

                        proposals.Add(new ProposedPropagationItem
                        {
                            SourceEntityName = SourceCoreProblem,
                            SourceEntityId = foundation.Id,
                            SourceLabel = "Problème central (socle stratégique)",
                            TargetEntityName = nameof(ProblemNode),
                            TargetEntityId = coreNode?.Id,
                            TargetLabel = "Nœud central de l'arbre à problèmes",
                            ProposedContent = foundation.CoreProblemStatement,
                            CurrentContent = coreNode?.Description,
                            ActionType = coreNode == null ? "Create" : "Update",
                            IsSelected = link?.Status != "Overridden",
                            DiffSummary = SummaryFor(link, coreNode != null,
                                "Créer le problème central de l'arbre à problèmes",
                                "Mettre à jour le problème central avec la formulation stratégique")
                        });
                    }
                    break;

                case LifecycleStage.CadreLogique:
                    // Théorie du changement : Outcome → Résultat, Activity → Activité
                    foreach (var node in foundation.TheoryOfChangeNodes.Where(n => !n.IsDeleted).OrderBy(n => n.OrderIndex))
                    {
                        string? targetType =
                            string.Equals(node.NodeType, "Outcome", StringComparison.OrdinalIgnoreCase) ? nameof(Result) :
                            string.Equals(node.NodeType, "Activity", StringComparison.OrdinalIgnoreCase) ? nameof(Activity) : null;
                        if (targetType == null) continue;

                        var link = FindLink(nameof(TheoryOfChangeNode), node.Id, targetType);
                        string? current = null;
                        int? targetId = null;
                        if (link != null)
                        {
                            current = targetType == nameof(Result)
                                ? (await db.Results.FirstOrDefaultAsync(r => r.Id == link.TargetEntityId))?.Description
                                : (await db.Activities.FirstOrDefaultAsync(a => a.Id == link.TargetEntityId))?.Description;
                            if (current != null) targetId = link.TargetEntityId;
                        }
                        if (current != null && current.Trim() == node.Title.Trim()) continue; // déjà aligné

                        bool isResult = targetType == nameof(Result);
                        proposals.Add(new ProposedPropagationItem
                        {
                            SourceEntityName = nameof(TheoryOfChangeNode),
                            SourceEntityId = node.Id,
                            SourceLabel = $"ToC – {(isResult ? "effet" : "activité")} : {node.Title}",
                            TargetEntityName = targetType,
                            TargetEntityId = targetId,
                            TargetLabel = isResult ? "Résultat du cadre logique" : "Activité du cadre logique",
                            ProposedContent = node.Title,
                            CurrentContent = current,
                            ActionType = targetId == null ? "Create" : "Update",
                            IsSelected = link?.Status != "Overridden",
                            DiffSummary = SummaryFor(link, targetId != null,
                                isResult ? "Dériver un résultat du cadre logique depuis la théorie du changement"
                                         : "Dériver une activité du cadre logique depuis la théorie du changement",
                                "Aligner la formulation sur la théorie du changement")
                        });
                    }
                    break;
            }

            return proposals;
        }

        /// <summary>
        /// Applique les propositions validées par l'utilisateur et trace les liens stratégiques.
        /// </summary>
        public async Task<PropagationSummary> ApplyProposalsAsync(int projectId, List<ProposedPropagationItem> selectedItems, string userEmail)
        {
            var summary = new PropagationSummary();
            using var db = _dbFactory();

            foreach (var item in selectedItems.Where(i => i.IsSelected))
            {
                try
                {
                    switch (item.TargetEntityName)
                    {
                        case "Project.GeneralObjective":
                            var proj = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                            if (proj != null)
                            {
                                proj.GeneralObjective = item.ProposedContent;
                                summary.UpdatedCount++;
                            }
                            break;

                        case nameof(Objective):
                            await ApplyObjectiveAsync(db, projectId, item, userEmail, summary);
                            break;

                        case nameof(ProblemNode):
                            await ApplyCoreProblemAsync(db, projectId, item, userEmail, summary);
                            break;

                        case nameof(Result):
                            await ApplyResultAsync(db, projectId, item, userEmail, summary);
                            break;

                        case nameof(Activity):
                            await ApplyActivityAsync(db, projectId, item, userEmail, summary);
                            break;

                        default:
                            summary.SkippedCount++;
                            summary.Messages.Add($"Type de cible non pris en charge : {item.TargetEntityName}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    summary.SkippedCount++;
                    summary.Messages.Add($"Erreur lors de l'application de « {item.SourceLabel} » : {ex.Message}");
                    AppLogger.Error(nameof(PropagationEngineService), $"Échec de propagation : {item.SourceLabel}", ex);
                }
            }

            await db.SaveChangesAsync();

            db.AuditLogEntries.Add(new AuditLogEntry
            {
                ProjectId = projectId,
                ActionType = "PROPAGATION_APPLIED",
                Details = $"Propagation depuis le socle stratégique : {summary.CreatedCount} création(s), {summary.UpdatedCount} mise(s) à jour, {summary.SkippedCount} ignorée(s)",
                UserEmail = userEmail
            });
            await db.SaveChangesAsync();

            return summary;
        }

        private async Task ApplyObjectiveAsync(AppDbContext db, int projectId, ProposedPropagationItem item, string userEmail, PropagationSummary summary)
        {
            var existing = item.TargetEntityId.HasValue
                ? await db.Objectives.FirstOrDefaultAsync(o => o.Id == item.TargetEntityId.Value)
                : null;

            if (existing != null)
            {
                existing.Description = item.ProposedContent;
                existing.AiSuggestionBadge = BadgeDerived;
                MarkSynced(await FindLinkAsync(db, projectId, item, existing.Id), item.ProposedContent);
                summary.UpdatedCount++;
                return;
            }

            int count = await db.Objectives.CountAsync(o => o.ProjectId == projectId);
            var newObj = new Objective
            {
                ProjectId = projectId,
                Code = $"OS{count + 1}",
                Description = item.ProposedContent,
                Status = "Approved",
                AiSuggestionBadge = BadgeDerived
            };
            db.Objectives.Add(newObj);
            await db.SaveChangesAsync();
            AddLink(db, projectId, item, newObj.Id, "FeedsObjective", userEmail);
            summary.CreatedCount++;
        }

        private async Task ApplyCoreProblemAsync(AppDbContext db, int projectId, ProposedPropagationItem item, string userEmail, PropagationSummary summary)
        {
            var coreNode = await db.ProblemNodes.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.NodeType == "Core");
            if (coreNode != null)
            {
                coreNode.Description = item.ProposedContent;
                coreNode.AiSuggestionBadge = BadgeDerived;
                var link = await FindLinkAsync(db, projectId, item, coreNode.Id);
                if (link != null) MarkSynced(link, item.ProposedContent);
                else AddLink(db, projectId, item, coreNode.Id, "FeedsLogframe", userEmail);
                summary.UpdatedCount++;
                return;
            }

            var newCore = new ProblemNode
            {
                ProjectId = projectId,
                Title = "Problème central",
                Description = item.ProposedContent,
                NodeType = "Core",
                Status = "Approved",
                AiSuggestionBadge = BadgeDerived
            };
            db.ProblemNodes.Add(newCore);
            await db.SaveChangesAsync();
            AddLink(db, projectId, item, newCore.Id, "FeedsLogframe", userEmail);
            summary.CreatedCount++;
        }

        private async Task ApplyResultAsync(AppDbContext db, int projectId, ProposedPropagationItem item, string userEmail, PropagationSummary summary)
        {
            var existing = item.TargetEntityId.HasValue
                ? await db.Results.FirstOrDefaultAsync(r => r.Id == item.TargetEntityId.Value)
                : null;
            if (existing != null)
            {
                existing.Description = item.ProposedContent;
                existing.AiSuggestionBadge = BadgeDerived;
                MarkSynced(await FindLinkAsync(db, projectId, item, existing.Id), item.ProposedContent);
                summary.UpdatedCount++;
                return;
            }

            // Un résultat se rattache à un objectif spécifique : le premier (OS1) par défaut, à réaffecter si besoin.
            var objectives = await db.Objectives.Where(o => o.ProjectId == projectId).OrderBy(o => o.Code).ToListAsync();
            var parent = objectives.FirstOrDefault();
            if (parent == null)
            {
                summary.SkippedCount++;
                summary.Messages.Add($"« {item.ProposedContent} » ignoré : créez d'abord un objectif spécifique (étape Cadrage).");
                return;
            }

            int objIndex = objectives.IndexOf(parent) + 1;
            int count = await db.Results.CountAsync(r => r.ObjectiveId == parent.Id);
            var newResult = new Result
            {
                ObjectiveId = parent.Id,
                Code = $"R{objIndex}.{count + 1}",
                Description = item.ProposedContent,
                Status = "Approved",
                AiSuggestionBadge = BadgeDerived
            };
            db.Results.Add(newResult);
            await db.SaveChangesAsync();
            AddLink(db, projectId, item, newResult.Id, "FeedsLogframe", userEmail);
            await db.SaveChangesAsync(); // le lien doit être visible pour rattacher les activités ToC enfants dans la même passe
            summary.CreatedCount++;
            summary.Messages.Add($"Résultat {newResult.Code} rattaché à {parent.Code} (à réaffecter dans le cadre logique si nécessaire).");
        }

        private async Task ApplyActivityAsync(AppDbContext db, int projectId, ProposedPropagationItem item, string userEmail, PropagationSummary summary)
        {
            var existing = item.TargetEntityId.HasValue
                ? await db.Activities.FirstOrDefaultAsync(a => a.Id == item.TargetEntityId.Value)
                : null;
            if (existing != null)
            {
                existing.Description = item.ProposedContent;
                existing.AiSuggestionBadge = BadgeDerived;
                MarkSynced(await FindLinkAsync(db, projectId, item, existing.Id), item.ProposedContent);
                summary.UpdatedCount++;
                return;
            }

            // Résultat parent : celui dérivé de l'effet ToC parent de ce nœud s'il existe, sinon le premier résultat du projet.
            Result? parent = null;
            var node = await db.TheoryOfChangeNodes.FirstOrDefaultAsync(n => n.Id == item.SourceEntityId);
            if (node?.ParentNodeId != null)
            {
                var parentLink = await db.StrategicLinks.FirstOrDefaultAsync(l => l.ProjectId == projectId && !l.IsDeleted
                    && l.SourceEntityType == nameof(TheoryOfChangeNode) && l.SourceEntityId == node.ParentNodeId.Value
                    && l.TargetEntityType == nameof(Result));
                if (parentLink != null) parent = await db.Results.FirstOrDefaultAsync(r => r.Id == parentLink.TargetEntityId);
            }
            parent ??= await db.Results
                .Where(r => db.Objectives.Any(o => o.Id == r.ObjectiveId && o.ProjectId == projectId))
                .OrderBy(r => r.Code)
                .FirstOrDefaultAsync();

            if (parent == null)
            {
                summary.SkippedCount++;
                summary.Messages.Add($"« {item.ProposedContent} » ignoré : créez d'abord un résultat dans le cadre logique.");
                return;
            }

            int count = await db.Activities.CountAsync(a => a.ResultId == parent.Id);
            string resultSuffix = parent.Code.StartsWith("R", StringComparison.OrdinalIgnoreCase) ? parent.Code.Substring(1) : parent.Code;
            var newActivity = new Activity
            {
                ResultId = parent.Id,
                Code = $"A{resultSuffix}.{count + 1}",
                Description = item.ProposedContent,
                Status = "Approved",
                AiSuggestionBadge = BadgeDerived
            };
            db.Activities.Add(newActivity);
            await db.SaveChangesAsync();
            AddLink(db, projectId, item, newActivity.Id, "FeedsLogframe", userEmail);
            summary.CreatedCount++;
        }

        private static Task<StrategicLink?> FindLinkAsync(AppDbContext db, int projectId, ProposedPropagationItem item, int targetId) =>
            db.StrategicLinks.FirstOrDefaultAsync(l => l.ProjectId == projectId && !l.IsDeleted
                && l.SourceEntityType == item.SourceEntityName
                && l.SourceEntityId == item.SourceEntityId
                && l.TargetEntityType == item.TargetEntityName
                && l.TargetEntityId == targetId);

        private static void MarkSynced(StrategicLink? link, string content)
        {
            if (link == null) return;
            link.Status = "Synced";
            link.LastSyncValue = ComputeHash(content);
            link.LastSyncAt = DateTime.UtcNow;
            link.LastModifiedAt = DateTime.UtcNow;
            link.Version++;
        }

        private static void AddLink(AppDbContext db, int projectId, ProposedPropagationItem item, int targetId, string linkType, string userEmail)
        {
            db.StrategicLinks.Add(new StrategicLink
            {
                ProjectId = projectId,
                SourceEntityType = item.SourceEntityName,
                SourceEntityId = item.SourceEntityId,
                TargetEntityType = item.TargetEntityName,
                TargetEntityId = targetId,
                LinkType = linkType,
                Status = "Synced",
                LastSyncValue = ComputeHash(item.ProposedContent),
                LastSyncAt = DateTime.UtcNow,
                CreatedBy = userEmail
            });
        }

        /// <summary>
        /// Recalcule le statut de tous les liens du projet en comparant les empreintes (ADR 4).
        /// La modification manuelle de la cible (Overridden) prime sur la modification de la source (Stale),
        /// afin de ne jamais proposer d'écraser un travail humain.
        /// À appeler après chaque enregistrement du socle stratégique et avant une analyse globale.
        /// </summary>
        public async Task<LinkStatusRefreshResult> RefreshLinkStatusesAsync(int projectId)
        {
            var result = new LinkStatusRefreshResult();
            using var db = _dbFactory();

            var links = await db.StrategicLinks.Where(l => l.ProjectId == projectId && !l.IsDeleted).ToListAsync();
            if (!links.Any()) return result;

            var foundation = await db.StrategicFoundations
                .Include(f => f.StrategicObjectives)
                .Include(f => f.TheoryOfChangeNodes)
                .FirstOrDefaultAsync(f => f.ProjectId == projectId && !f.IsDeleted);

            foreach (var link in links)
            {
                string? source = ResolveSourceContent(foundation, link);
                string? target = await ResolveTargetContentAsync(db, link);

                string newStatus;
                if (target == null || source == null)
                {
                    result.OrphanCount++;
                    continue; // élément supprimé d'un côté : rien à synchroniser
                }
                else if (link.Status == "Overridden" || ComputeHash(target) != link.LastSyncValue)
                {
                    newStatus = "Overridden";
                }
                else if (ComputeHash(source) != link.LastSyncValue)
                {
                    newStatus = "Stale";
                }
                else
                {
                    newStatus = "Synced";
                }

                if (newStatus != link.Status)
                {
                    link.Status = newStatus;
                    link.LastModifiedAt = DateTime.UtcNow;
                    link.Version++;
                    result.ChangedCount++;
                }

                switch (newStatus)
                {
                    case "Stale": result.StaleCount++; break;
                    case "Overridden": result.OverriddenCount++; break;
                    default: result.SyncedCount++; break;
                }
            }

            if (result.ChangedCount > 0) await db.SaveChangesAsync();
            return result;
        }

        private static string? ResolveSourceContent(StrategicFoundation? foundation, StrategicLink link)
        {
            if (foundation == null) return null;
            switch (link.SourceEntityType)
            {
                case nameof(StrategicObjective):
                    var obj = foundation.StrategicObjectives.FirstOrDefault(o => o.Id == link.SourceEntityId && !o.IsDeleted);
                    return obj == null ? null : ObjectiveContent(obj);
                case SourceCoreProblem:
                    return foundation.Id == link.SourceEntityId ? foundation.CoreProblemStatement : null;
                case nameof(TheoryOfChangeNode):
                    return foundation.TheoryOfChangeNodes.FirstOrDefault(n => n.Id == link.SourceEntityId && !n.IsDeleted)?.Title;
                default:
                    return null;
            }
        }

        private static async Task<string?> ResolveTargetContentAsync(AppDbContext db, StrategicLink link) => link.TargetEntityType switch
        {
            nameof(Objective) => (await db.Objectives.FirstOrDefaultAsync(o => o.Id == link.TargetEntityId))?.Description,
            nameof(ProblemNode) => (await db.ProblemNodes.FirstOrDefaultAsync(p => p.Id == link.TargetEntityId))?.Description,
            nameof(Result) => (await db.Results.FirstOrDefaultAsync(r => r.Id == link.TargetEntityId))?.Description,
            nameof(Activity) => (await db.Activities.FirstOrDefaultAsync(a => a.Id == link.TargetEntityId))?.Description,
            _ => null
        };

        /// <summary>
        /// Contrôle du budget détaillé par rapport au plafond estimé dans le socle stratégique.
        /// </summary>
        public async Task<(bool HasCeilingWarning, decimal EstimatedCeiling, decimal TotalBudget, string Message)> CheckBudgetCeilingAsync(int projectId)
        {
            using var db = _dbFactory();
            var foundation = await db.StrategicFoundations.FirstOrDefaultAsync(f => f.ProjectId == projectId && !f.IsDeleted);
            if (foundation == null || foundation.EstimatedBudget <= 0)
            {
                return (false, 0, 0, "Aucun plafond budgétaire estimatif n'est défini dans la planification stratégique.");
            }

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            string cur = Currency(project);
            var budgetItems = await db.BudgetItems.Where(b => b.ProjectId == projectId).ToListAsync();
            var totalBudget = budgetItems.Sum(b => b.TotalCost);

            if (totalBudget > foundation.EstimatedBudget)
            {
                var diff = totalBudget - foundation.EstimatedBudget;
                return (true, foundation.EstimatedBudget, totalBudget,
                    $"⚠️ Le budget détaillé ({totalBudget:N0} {cur}) dépasse le plafond stratégique estimé ({foundation.EstimatedBudget:N0} {cur}) de {diff:N0} {cur}.");
            }

            return (false, foundation.EstimatedBudget, totalBudget,
                $"Le budget détaillé ({totalBudget:N0} {cur}) respecte le plafond stratégique estimé ({foundation.EstimatedBudget:N0} {cur}).");
        }
    }
}

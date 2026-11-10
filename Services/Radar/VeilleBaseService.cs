using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>Appel repéré (RSS, recherche web, agrégateur ou saisie), avant pré-filtrage.</summary>
    public sealed class VeilleCandidate
    {
        public string Title { get; set; } = string.Empty;
        public string Donor { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Eligibility { get; set; } = string.Empty;
        public DateTime? Deadline { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string Confirmation { get; set; } = VeilleConfirmation.NonConfirme;

        /// <summary>RSS, web, agrégateur ou officiel, avec le nom de la source (« RSS — CEPF »).</summary>
        public string DetectionSource { get; set; } = string.Empty;

        public string FullText => string.Join(" ", Title, Donor, Summary, Eligibility);
    }

    public enum VeilleTable { None, Appels, AVenir, NonRetenus }

    public sealed class PreFilterDecision
    {
        public bool Accepted { get; init; }
        public string Reason { get; init; } = string.Empty;
        public List<string> Axes { get; init; } = new();

        /// <summary>Appel clos mais récurrent : à suivre pour le prochain cycle (A_VENIR).</summary>
        public bool FollowNextCycle { get; init; }
    }

    public enum VeilleOutcomeKind { Added, Duplicate, Rejected, Upcoming }

    public sealed class VeilleOutcome
    {
        public VeilleOutcomeKind Kind { get; init; }
        public string Message { get; init; } = string.Empty;
        public GrantOpportunity? Opportunity { get; init; }
        public VeilleTable DuplicateOf { get; init; }
    }

    public sealed class VeilleStatusChange
    {
        public string VeilleId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string OldStatus { get; init; } = string.Empty;
        public string NewStatus { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;

        public override string ToString() => $"{VeilleId} « {Title} » : {OldStatus} → {NewStatus} ({Reason})";
    }

    public sealed class AxisCoverage
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public int CountA { get; init; }
        public int CountB { get; init; }
        public int CountC { get; init; }
        public int Total => CountA + CountB + CountC;
        public bool IsLeastCovered { get; set; }
        public string Comment => IsLeastCovered ? "Axe le moins couvert : orienter les prochaines requêtes." : string.Empty;
    }

    /// <summary>
    /// Base permanente de veille (APPELS = grants_pipeline, A_VENIR, VUS_NON_RETENUS) :
    /// pré-filtrage, dédoublonnage, identifiants, contrôle des échéances, couverture par axe.
    /// Rien n'est supprimé : seuls les statuts changent, et chaque changement est daté dans les notes.
    /// </summary>
    public class VeilleBaseService
    {
        /// <summary>Un appel « non confirmé » depuis plus de 7 jours dont la date limite approche passe « trop tard ».</summary>
        public const int UnconfirmedGraceDays = 7;
        public const int UnconfirmedNearDeadlineDays = 15;

        private static readonly string[] MoroccoKeywords =
        {
            "maroc", "morocco", "marocain*", "moroccan", "المغرب", "مغربي", "mena", "afrique du nord", "north africa",
            "maghreb", "mediterran*", "southern neighbourhood", "voisinage sud"
        };

        private static readonly string[] ZoneExclusionKeywords =
        {
            "eu member states only", "only eu member states", "etats membres de l'ue uniquement",
            "etats membres de l'union europeenne uniquement", "reserve aux etats membres",
            "sub-saharan africa only", "only sub-saharan", "afrique subsaharienne uniquement",
            "afrique subsaharienne seulement", "us-based organizations only", "united states only",
            "uk-based organisations only", "uk registered charities only"
        };

        private static readonly string[] CompaniesOnlyKeywords =
        {
            "entreprises uniquement", "reserve aux entreprises", "reservee aux entreprises", "startups only",
            "start-ups only", "only startups", "only start-ups", "for-profit companies only", "only for companies",
            "companies only", "smes only", "pme uniquement", "reserve aux start-up", "reserve aux startups"
        };

        private static readonly string[] OrganisationKeywords =
        {
            "association", "ong", "ngo", "non-profit", "nonprofit", "civil society", "societe civile", "osc", "cso", "جمعي"
        };

        private static readonly string[] RecurrentKeywords =
        {
            "annuel*", "annual", "chaque annee", "each year", "every year", "edition", "cycle", "round", "سنوي"
        };

        private readonly Func<AppDbContext> _dbContextFactory;

        public VeilleBaseService(Func<AppDbContext>? dbContextFactory = null)
        {
            _dbContextFactory = dbContextFactory ?? (() => new AppDbContext());
        }

        #region Pré-filtrage (sans base)

        /// <summary>Zone, axes, type de candidat et date limite. Un refus porte toujours son motif.</summary>
        public static PreFilterDecision PreFilter(VeilleCandidate candidate, DateTime today)
        {
            string text = candidate.FullText;

            if (!VeilleAxes.ContainsAny(text, MoroccoKeywords) && VeilleAxes.ContainsAny(text, ZoneExclusionKeywords))
                return new PreFilterDecision { Reason = "zone excluant le Maroc" };

            var axes = VeilleAxes.DetectAxes(text);
            if (axes.Count == 0)
                return new PreFilterDecision { Reason = "hors axes" };

            if (VeilleAxes.ContainsAny(text, CompaniesOnlyKeywords) && !VeilleAxes.ContainsAny(text, OrganisationKeywords))
                return new PreFilterDecision { Reason = "associations non éligibles", Axes = axes };

            if (VeilleScoringService.IsPast(candidate.Deadline, today))
            {
                return new PreFilterDecision
                {
                    Reason = $"clos le {candidate.Deadline!.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
                    Axes = axes,
                    FollowNextCycle = VeilleAxes.ContainsAny(text, RecurrentKeywords)
                };
            }

            return new PreFilterDecision { Accepted = true, Axes = axes };
        }

        /// <summary>Même appel : même bailleur, même intitulé normalisé, et même date limite quand les deux sont connues.</summary>
        public static bool IsSameCall(string donorA, string titleA, DateTime? deadlineA, string donorB, string titleB, DateTime? deadlineB)
        {
            if (VeilleText.NormalizeTitle(titleA) != VeilleText.NormalizeTitle(titleB)) return false;

            string dA = VeilleText.NormalizeTitle(donorA), dB = VeilleText.NormalizeTitle(donorB);
            if (dA.Length > 0 && dB.Length > 0 && dA != dB) return false;

            return deadlineA is not { } a || deadlineB is not { } b || a.Date == b.Date;
        }

        #endregion

        #region Entrée dans la base

        /// <summary>
        /// Étapes 2, 4 et 5 pour un appel repéré : doublon → pré-filtrage → notation → enregistrement.
        /// Un doublon n'est jamais recréé : une information nouvelle (date limite, montant) complète la fiche existante.
        /// </summary>
        public async Task<VeilleOutcome> ProcessCandidateAsync(VeilleCandidate candidate, DateTime today)
        {
            using var db = _dbContextFactory();

            var (table, existing) = await FindDuplicateAsync(db, candidate);
            if (table != VeilleTable.None)
            {
                if (existing != null && CompleteExisting(existing, candidate, today))
                    await db.SaveChangesAsync();
                return new VeilleOutcome
                {
                    Kind = VeilleOutcomeKind.Duplicate,
                    DuplicateOf = table,
                    Opportunity = existing,
                    Message = $"Déjà présent dans {TableLabel(table)} : rien n'est recréé."
                };
            }

            if (candidate.Confirmation == VeilleConfirmation.Annonce)
            {
                var upcoming = await AddUpcomingAsync(db, candidate.Donor, candidate.Title, string.Empty, candidate.Deadline,
                    FormatAmount(candidate), candidate.Link, $"Repéré via {candidate.DetectionSource}.");
                await db.SaveChangesAsync();
                return new VeilleOutcome { Kind = VeilleOutcomeKind.Upcoming, Message = $"Appel annoncé : ajouté à « À venir » ({upcoming.VeilleId})." };
            }

            var decision = PreFilter(candidate, today);
            if (!decision.Accepted)
            {
                db.RejectedCalls.Add(new RejectedCall
                {
                    SeenAt = today,
                    Title = candidate.Title,
                    Donor = candidate.Donor,
                    Link = candidate.Link,
                    Deadline = candidate.Deadline,
                    Reason = decision.Reason
                });
                string message = $"Vu, non retenu : {decision.Reason}.";
                if (decision.FollowNextCycle)
                {
                    var upcoming = await AddUpcomingAsync(db, candidate.Donor, candidate.Title, "prochain cycle [NON PRÉCISÉ — à vérifier]",
                        null, FormatAmount(candidate), candidate.Link, $"Appel récurrent {decision.Reason} : à suivre pour le prochain cycle.");
                    message += $" Appel récurrent : à suivre pour le prochain cycle ({upcoming.VeilleId}).";
                }
                await db.SaveChangesAsync();
                return new VeilleOutcome { Kind = VeilleOutcomeKind.Rejected, Message = message };
            }

            var opp = new GrantOpportunity
            {
                ExternalId = Guid.NewGuid().ToString("N"),
                Title = candidate.Title,
                Donor = string.IsNullOrWhiteSpace(candidate.Donor) ? "[NON PRÉCISÉ — à vérifier]" : candidate.Donor,
                OriginalUrl = candidate.Link,
                EstimatedBudget = candidate.Amount,
                Currency = candidate.Currency,
                Deadline = candidate.Deadline,
                SummaryFr = candidate.Summary,
                Eligibility = candidate.Eligibility,
                Confirmation = candidate.Confirmation,
                VerifiedAt = candidate.Confirmation == VeilleConfirmation.Confirme ? today : null,
                DetectionSource = candidate.DetectionSource,
                AddedAt = today,
                PipelineStatus = VeilleStatus.Nouveau
            };
            await EnterIntoBaseAsync(db, opp, today);
            db.GrantsPipeline.Add(opp);
            await db.SaveChangesAsync();

            return new VeilleOutcome
            {
                Kind = VeilleOutcomeKind.Added,
                Opportunity = opp,
                Message = $"{opp.VeilleId} ajouté : {opp.ScoreTotal}/25, priorité {opp.Priority}."
            };
        }

        /// <summary>
        /// Attribue l'identifiant V-AAAA-NNN et la première notation /25 à un appel qui entre dans la base
        /// (depuis le Radar ou la formulation). Sans effet sur un appel qui a déjà un identifiant.
        /// </summary>
        public async Task EnterIntoBaseAsync(AppDbContext db, GrantOpportunity opp, DateTime today)
        {
            if (!string.IsNullOrEmpty(opp.VeilleId)) return;

            opp.VeilleId = await NextVeilleIdAsync(db, today.Year);
            var coverage = await ComputeAxisCoverageAsync(db);
            bool knownDonor = await db.GrantsPipeline.AnyAsync(g => g.Id != opp.Id && g.Donor == opp.Donor && g.VeilleId != "");

            var card = VeilleScoringService.Score(new VeilleCallInfo
            {
                Title = opp.Title,
                Donor = opp.Donor,
                Summary = opp.SummaryFr,
                Eligibility = opp.Eligibility,
                Amount = opp.EstimatedBudget,
                Currency = opp.Currency,
                Deadline = opp.Deadline,
                Confirmation = opp.Confirmation,
                IsNewDonor = !knownDonor,
                LeastCoveredAxis = coverage.FirstOrDefault(c => c.IsLeastCovered)?.Code
            }, today);
            VeilleScoringService.Apply(card, opp);
        }

        /// <summary>Donne un identifiant et une notation aux appels enregistrés avant la base de veille.</summary>
        public async Task<int> BackfillAsync(DateTime today)
        {
            using var db = _dbContextFactory();
            var legacy = await db.GrantsPipeline.Where(g => !g.IsDeleted && g.VeilleId == "").OrderBy(g => g.AddedAt).ToListAsync();
            foreach (var opp in legacy)
            {
                await EnterIntoBaseAsync(db, opp, opp.AddedAt.Year > 2000 ? opp.AddedAt : today);
                await db.SaveChangesAsync();
            }
            return legacy.Count;
        }

        public async Task<UpcomingCall> AddUpcomingAsync(string donor, string programme, string expectedOpening, DateTime? expectedDeadline, string amount, string link, string notes)
        {
            using var db = _dbContextFactory();
            var upcoming = await AddUpcomingAsync(db, donor, programme, expectedOpening, expectedDeadline, amount, link, notes);
            await db.SaveChangesAsync();
            return upcoming;
        }

        /// <summary>L'appel « À venir » s'est ouvert : il entre dans APPELS avec le statut « Nouveau ».</summary>
        public async Task<GrantOpportunity?> PromoteUpcomingAsync(int upcomingId, DateTime today)
        {
            using var db = _dbContextFactory();
            var upcoming = await db.UpcomingCalls.FindAsync(upcomingId);
            if (upcoming == null || upcoming.PromotedGrantId != null) return null;

            var opp = new GrantOpportunity
            {
                ExternalId = Guid.NewGuid().ToString("N"),
                Title = upcoming.Programme,
                Donor = string.IsNullOrWhiteSpace(upcoming.Donor) ? "[NON PRÉCISÉ — à vérifier]" : upcoming.Donor,
                OriginalUrl = upcoming.Link,
                Deadline = upcoming.ExpectedDeadline,
                SummaryFr = upcoming.Notes,
                DetectionSource = $"À venir {upcoming.VeilleId}",
                AddedAt = today,
                PipelineStatus = VeilleStatus.Nouveau,
                Notes = $"[{today:dd/MM/yyyy}] Ouvert : bascule depuis « À venir » ({upcoming.VeilleId})."
            };
            await EnterIntoBaseAsync(db, opp, today);
            db.GrantsPipeline.Add(opp);
            await db.SaveChangesAsync();

            upcoming.PromotedGrantId = opp.Id;
            upcoming.Notes = AppendNote(upcoming.Notes, today, $"Ouvert : basculé vers les appels ({opp.VeilleId}).");
            await db.SaveChangesAsync();
            return opp;
        }

        #endregion

        #region Statuts et notation manuelle

        /// <summary>Change le statut et le date dans les notes (aucune suppression).</summary>
        public async Task<bool> SetStatusAsync(int id, string newStatus, string reason, DateTime today)
        {
            using var db = _dbContextFactory();
            var opp = await db.GrantsPipeline.FindAsync(id);
            if (opp == null || opp.IsDeleted || opp.PipelineStatus == newStatus) return false;

            opp.Notes = AppendNote(opp.Notes, today, $"Statut : {opp.PipelineStatus} → {newStatus}{(string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason})")}.");
            opp.PipelineStatus = newStatus;
            await db.SaveChangesAsync();
            return true;
        }

        /// <summary>Enregistre les notes corrigées par l'opérateur et recalcule total, plafond et priorité.</summary>
        public async Task<GrantOpportunity?> SaveScoreAsync(int id, int pertinence, int eligibilite, int financier, int faisabilite,
            int strategique, string confirmation, string justification, DateTime today)
        {
            using var db = _dbContextFactory();
            var opp = await db.GrantsPipeline.FindAsync(id);
            if (opp == null || opp.IsDeleted) return null;

            if (confirmation == VeilleConfirmation.Confirme && opp.Confirmation != VeilleConfirmation.Confirme)
                opp.VerifiedAt = today;

            opp.ScorePertinence = pertinence;
            opp.ScoreEligibilite = eligibilite;
            opp.ScoreFinancier = financier;
            opp.ScoreFaisabilite = faisabilite;
            opp.ScoreStrategique = strategique;
            opp.Confirmation = confirmation;
            opp.ScoreJustification = justification;
            VeilleScoringService.Recompute(opp);
            await db.SaveChangesAsync();
            return opp;
        }

        #endregion

        #region Contrôle des échéances

        /// <summary>
        /// À chaque passage, même rapide : DL dépassée → « Clos » ; DL à moins de 10 jours et appel non engagé →
        /// « Trop tard » ; « non confirmé » depuis plus de 7 jours avec une DL proche → « Trop tard » avec une note.
        /// </summary>
        public async Task<List<VeilleStatusChange>> RunDeadlineCheckAsync(DateTime today)
        {
            using var db = _dbContextFactory();
            var changes = new List<VeilleStatusChange>();
            var active = await db.GrantsPipeline.Where(g => !g.IsDeleted && g.Deadline != null).ToListAsync();

            foreach (var opp in active.Where(o => VeilleStatus.IsActive(o.PipelineStatus) && o.PipelineStatus != VeilleStatus.Soumis))
            {
                string? newStatus = null, reason = null;
                var deadline = opp.Deadline!.Value.Date;
                double daysLeft = (deadline - today.Date).TotalDays;

                if (daysLeft < 0)
                {
                    (newStatus, reason) = (VeilleStatus.Clos, $"date limite dépassée ({deadline:dd/MM/yyyy})");
                }
                else if (!VeilleStatus.IsEngaged(opp.PipelineStatus) && VeilleScoringService.IsTooLate(deadline, today))
                {
                    (newStatus, reason) = (VeilleStatus.TropTard, $"date limite dans {daysLeft:0} jour(s), appel non engagé");
                }
                else if (!VeilleStatus.IsEngaged(opp.PipelineStatus) && opp.Confirmation == VeilleConfirmation.NonConfirme
                         && (today.Date - opp.AddedAt.Date).TotalDays > UnconfirmedGraceDays && daysLeft < UnconfirmedNearDeadlineDays)
                {
                    (newStatus, reason) = (VeilleStatus.TropTard, $"non confirmé depuis plus de {UnconfirmedGraceDays} jours, date limite le {deadline:dd/MM/yyyy}");
                }

                if (newStatus == null) continue;

                changes.Add(new VeilleStatusChange
                {
                    VeilleId = opp.VeilleId,
                    Title = opp.Title,
                    OldStatus = opp.PipelineStatus,
                    NewStatus = newStatus,
                    Reason = reason!
                });
                opp.Notes = AppendNote(opp.Notes, today, $"Contrôle des échéances : {opp.PipelineStatus} → {newStatus} ({reason}).");
                opp.PipelineStatus = newStatus;
            }

            await db.SaveChangesAsync();
            return changes;
        }

        #endregion

        #region Lecture

        public async Task<List<UpcomingCall>> GetUpcomingAsync()
        {
            using var db = _dbContextFactory();
            return await db.UpcomingCalls.OrderBy(u => u.PromotedGrantId != null).ThenBy(u => u.ExpectedDeadline).ToListAsync();
        }

        public async Task<List<RejectedCall>> GetRejectedAsync()
        {
            using var db = _dbContextFactory();
            return await db.RejectedCalls.OrderByDescending(r => r.SeenAt).ToListAsync();
        }

        public async Task<List<AxisCoverage>> ComputeAxisCoverageAsync()
        {
            using var db = _dbContextFactory();
            return await ComputeAxisCoverageAsync(db);
        }

        /// <summary>Appels actifs par axe et par priorité ; l'axe le moins couvert est marqué (le premier en cas d'égalité).</summary>
        public static async Task<List<AxisCoverage>> ComputeAxisCoverageAsync(AppDbContext db)
        {
            var rows = await db.GrantsPipeline.Where(g => !g.IsDeleted)
                .Select(g => new { g.PipelineStatus, g.Priority, g.VeilleAxes })
                .ToListAsync();
            var active = rows.Where(r => VeilleStatus.IsActive(r.PipelineStatus)).ToList();

            var coverage = VeilleAxes.All.Select(axis =>
            {
                var touching = active.Where(r => VeilleAxes.Split(r.VeilleAxes).Contains(axis.Code)).ToList();
                return new AxisCoverage
                {
                    Code = axis.Code,
                    Label = axis.Label,
                    CountA = touching.Count(r => r.Priority == "A"),
                    CountB = touching.Count(r => r.Priority == "B"),
                    CountC = touching.Count(r => r.Priority == "C"),
                };
            }).ToList();

            int min = coverage.Min(c => c.Total);
            coverage.First(c => c.Total == min).IsLeastCovered = true;
            return coverage;
        }

        #endregion

        #region Outils

        public static async Task<string> NextVeilleIdAsync(AppDbContext db, int year)
        {
            string prefix = $"V-{year}-";
            var ids = await db.GrantsPipeline.IgnoreQueryFilters().Where(g => g.VeilleId.StartsWith(prefix)).Select(g => g.VeilleId).ToListAsync();
            ids.AddRange(db.GrantsPipeline.Local.Where(g => g.VeilleId.StartsWith(prefix)).Select(g => g.VeilleId));
            return $"{prefix}{MaxSequence(ids, prefix) + 1:000}";
        }

        public static async Task<string> NextUpcomingIdAsync(AppDbContext db)
        {
            const string prefix = "AV-";
            var ids = await db.UpcomingCalls.Select(u => u.VeilleId).ToListAsync();
            ids.AddRange(db.UpcomingCalls.Local.Select(u => u.VeilleId));
            return $"{prefix}{MaxSequence(ids, prefix) + 1:000}";
        }

        private static int MaxSequence(IEnumerable<string> ids, string prefix) =>
            ids.Select(id => int.TryParse(id.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0)
               .DefaultIfEmpty(0)
               .Max();

        private static async Task<(VeilleTable, GrantOpportunity?)> FindDuplicateAsync(AppDbContext db, VeilleCandidate c)
        {
            var appels = await db.GrantsPipeline.Where(g => !g.IsDeleted).ToListAsync();
            var match = appels.FirstOrDefault(g =>
                (!string.IsNullOrWhiteSpace(c.Link) && g.OriginalUrl == c.Link) ||
                IsSameCall(g.Donor, g.Title, g.Deadline, c.Donor, c.Title, c.Deadline));
            if (match != null) return (VeilleTable.Appels, match);

            var upcoming = await db.UpcomingCalls.Where(u => u.PromotedGrantId == null).ToListAsync();
            if (upcoming.Any(u => IsSameCall(u.Donor, u.Programme, u.ExpectedDeadline, c.Donor, c.Title, c.Deadline)))
                return (VeilleTable.AVenir, null);

            var rejected = await db.RejectedCalls.ToListAsync();
            if (rejected.Any(r => IsSameCall(r.Donor, r.Title, r.Deadline, c.Donor, c.Title, c.Deadline)))
                return (VeilleTable.NonRetenus, null);

            return (VeilleTable.None, null);
        }

        /// <summary>Ajoute à la fiche existante une date limite ou un montant jusque-là inconnus, avec la date de vérification.</summary>
        private static bool CompleteExisting(GrantOpportunity existing, VeilleCandidate c, DateTime today)
        {
            var added = new List<string>();
            if (existing.Deadline == null && c.Deadline != null)
            {
                existing.Deadline = c.Deadline;
                added.Add($"date limite {c.Deadline:dd/MM/yyyy}");
            }
            if (existing.EstimatedBudget == null && c.Amount != null)
            {
                existing.EstimatedBudget = c.Amount;
                existing.Currency ??= c.Currency;
                added.Add($"montant {FormatAmount(c)}");
            }
            if (c.Confirmation == VeilleConfirmation.Confirme && existing.Confirmation != VeilleConfirmation.Confirme)
            {
                existing.Confirmation = VeilleConfirmation.Confirme;
                existing.VerifiedAt = today;
                VeilleScoringService.Recompute(existing);
                added.Add("confirmé sur la page officielle");
            }
            if (added.Count == 0) return false;

            existing.Notes = AppendNote(existing.Notes, today, $"Complété via {c.DetectionSource} : {string.Join(", ", added)}.");
            return true;
        }

        private static async Task<UpcomingCall> AddUpcomingAsync(AppDbContext db, string donor, string programme, string expectedOpening,
            DateTime? expectedDeadline, string amount, string link, string notes)
        {
            var upcoming = new UpcomingCall
            {
                VeilleId = await NextUpcomingIdAsync(db),
                Donor = donor,
                Programme = programme,
                ExpectedOpening = string.IsNullOrWhiteSpace(expectedOpening) ? "[NON PRÉCISÉ — à vérifier]" : expectedOpening,
                ExpectedDeadline = expectedDeadline,
                Amount = string.IsNullOrWhiteSpace(amount) ? "[NON PRÉCISÉ — à vérifier]" : amount,
                Link = link,
                Notes = notes
            };
            db.UpcomingCalls.Add(upcoming);
            return upcoming;
        }

        private static string FormatAmount(VeilleCandidate c) =>
            c.Amount is { } a ? $"{a.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} {c.Currency}".Trim() : string.Empty;

        internal static string AppendNote(string? notes, DateTime today, string line)
        {
            string entry = $"[{today:dd/MM/yyyy}] {line}";
            return string.IsNullOrWhiteSpace(notes) ? entry : $"{notes}\n{entry}";
        }

        private static string TableLabel(VeilleTable table) => table switch
        {
            VeilleTable.Appels => "les appels suivis",
            VeilleTable.AVenir => "« À venir »",
            VeilleTable.NonRetenus => "« Vus et non retenus »",
            _ => string.Empty
        };

        #endregion
    }
}

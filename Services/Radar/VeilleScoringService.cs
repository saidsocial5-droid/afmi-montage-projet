using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>Données d'un appel utiles à la notation (issues du Radar, d'une saisie ou, plus tard, de la page officielle).</summary>
    public sealed class VeilleCallInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Donor { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Eligibility { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime? Deadline { get; set; }
        public string Confirmation { get; set; } = VeilleConfirmation.NonConfirme;

        /// <summary>Statuts, PV, audits, états financiers… : vrai si le texte exige des pièces lourdes.</summary>
        public bool HeavyDocuments { get; set; }

        /// <summary>Le bailleur n'apparaît pas encore dans la base de veille.</summary>
        public bool IsNewDonor { get; set; }

        /// <summary>Axe le moins couvert de la base (bonus de valeur stratégique).</summary>
        public string? LeastCoveredAxis { get; set; }

        public string FullText => string.Join(" ", Title, Donor, Summary, Eligibility);
    }

    /// <summary>Notation /25 d'un appel : cinq notes de 0 à 5, chacune justifiée en une phrase.</summary>
    public sealed class VeilleScoreCard
    {
        public int Pertinence { get; set; }
        public int Eligibilite { get; set; }
        public int Financier { get; set; }
        public int Faisabilite { get; set; }
        public int Strategique { get; set; }

        public string PertinenceJustification { get; set; } = string.Empty;
        public string EligibiliteJustification { get; set; } = string.Empty;
        public string FinancierJustification { get; set; } = string.Empty;
        public string FaisabiliteJustification { get; set; } = string.Empty;
        public string StrategiqueJustification { get; set; } = string.Empty;

        public List<string> Axes { get; set; } = new();
        public string ConsortiumAdvice { get; set; } = string.Empty;

        /// <summary>Candidature réservée aux personnes physiques : gardée en C avec cette mention.</summary>
        public bool IsIndividualOnly { get; set; }

        public int RawTotal => Pertinence + Eligibilite + Financier + Faisabilite + Strategique;
        public int Total { get; set; }
        public bool IsCapped { get; set; }
        public string Priority { get; set; } = string.Empty;

        public string Justification => string.Join(Environment.NewLine, new[]
        {
            $"Pertinence {Pertinence}/5 : {PertinenceJustification}",
            $"Éligibilité {Eligibilite}/5 : {EligibiliteJustification}",
            $"Intérêt financier {Financier}/5 : {FinancierJustification}",
            $"Faisabilité {Faisabilite}/5 : {FaisabiliteJustification}",
            $"Valeur stratégique {Strategique}/5 : {StrategiqueJustification}",
        }.Concat(IsCapped ? new[] { $"Plafond : total ramené de {RawTotal} à {VeilleScoringService.UnconfirmedCap} (appel non confirmé sur la page officielle)." } : Array.Empty<string>()));
    }

    /// <summary>
    /// Notation /25 et classement A/B/C de la méthode de veille AFMI, sans IA.
    /// Règle financière du bureau : aucun plafond de montant, un montant élevé n'est jamais pénalisé.
    /// Les notes calculées ici sont un premier jet : l'opérateur (ou l'IA, lot 2) peut les corriger,
    /// puis <see cref="Recompute"/> réapplique le plafond et le classement.
    /// </summary>
    public static class VeilleScoringService
    {
        public const int MaxNote = 5;
        public const int UnconfirmedCap = 14;
        public const int PriorityAThreshold = 20;
        public const int PriorityBThreshold = 14;

        /// <summary>« Trop tard » : date limite à moins de 10 jours de la date du passage.</summary>
        public const int TooLateDays = 10;

        public const string IndividualMention = "Candidature individuelle (membre de l'équipe)";

        private static readonly string[] OrganisationKeywords =
        {
            "association", "ong", "ngo", "non-profit", "nonprofit", "not-for-profit", "sans but lucratif",
            "civil society", "societe civile", "osc", "cso", "community-based", "organisation communautaire",
            "جمعي", "منظمات المجتمع المدني"
        };

        private static readonly string[] IndividualKeywords =
        {
            "personnes physiques", "personne physique", "individuals", "individual applicant", "candidature individuelle",
            "bourse individuelle", "fellowship", "storyteller", "journalist", "photograph*"
        };

        private static readonly string[] ConsortiumKeywords =
        {
            "consortium", "chef de file", "lead applicant", "co-applicant", "co-demandeur", "partenaires de pays",
            "transnational", "at least three partners", "at least 3 partners", "au moins trois partenaires"
        };

        private static readonly string[] NetworkKeywords =
        {
            "rmfm", "rafm", "rifm", "imfn", "foret modele", "model forest", "reseau international de forets modeles"
        };

        private static readonly string[] ContinuityKeywords =
        {
            "rescom", "apac", "icca", "cepf", "pmf", "small grants programme", "programme de microfinancements",
            "uicn", "iucn", "fem", "gef"
        };

        private static readonly string[] HeavyDocumentKeywords =
        {
            "audit", "etats financiers", "financial statements", "rapport d'audit", "audited", "chiffre d'affaires",
            "turnover", "garantie bancaire", "bank guarantee"
        };

        /// <summary>Premier jet de la notation à partir du texte de l'appel.</summary>
        public static VeilleScoreCard Score(VeilleCallInfo info, DateTime today)
        {
            string text = info.FullText;
            var card = new VeilleScoreCard { Axes = VeilleAxes.DetectAxes(text) };

            // 1. Pertinence
            bool core = VeilleAxes.IsCoreMission(text);
            (card.Pertinence, card.PertinenceJustification) = card.Axes.Count switch
            {
                0 => (0, "aucun axe de veille touché."),
                _ when core => (5, $"cœur de mission AFMI (cédraie, Agdal, APAC ou paysage) ; axes {VeilleAxes.Join(card.Axes)}."),
                >= 4 => (5, $"{card.Axes.Count} axes touchés ({VeilleAxes.Join(card.Axes)})."),
                >= 2 => (3, $"{card.Axes.Count} axes touchés ({VeilleAxes.Join(card.Axes)})."),
                _ => (1, $"un seul axe ({VeilleAxes.Join(card.Axes)}), lien marginal."),
            };

            // 2. Éligibilité (d'après le texte : à confirmer sur la page officielle)
            bool org = VeilleAxes.ContainsAny(text, OrganisationKeywords);
            bool individual = VeilleAxes.ContainsAny(text, IndividualKeywords);
            bool consortium = VeilleAxes.ContainsAny(text, ConsortiumKeywords);
            if (individual && !org)
            {
                card.IsIndividualOnly = true;
                (card.Eligibilite, card.EligibiliteJustification) = (1, "réservé aux personnes physiques : candidature individuelle d'un membre de l'équipe.");
            }
            else if (consortium)
                (card.Eligibilite, card.EligibiliteJustification) = (3, "consortium exigé ou prévu : AFMI éligible en partenaire.");
            else if (org)
                (card.Eligibilite, card.EligibiliteJustification) = (5, "associations / OSC explicitement éligibles (d'après le texte de l'annonce).");
            else
                (card.Eligibilite, card.EligibiliteJustification) = (2, "éligibilité des associations non précisée : à confirmer auprès du bailleur.");

            // 3. Intérêt financier : aucun plafond, un gros montant n'est jamais pénalisé
            if (info.Amount is not { } amount || amount <= 0)
            {
                (card.Financier, card.FinancierJustification) = (2, "montant [NON PRÉCISÉ — à vérifier].");
            }
            else if (IsLargeAmount(amount, info.Currency))
            {
                (card.Financier, card.FinancierJustification) = (4, $"montant élevé ({FormatAmount(amount, info.Currency)}), non pénalisé (règle du bureau).");
                card.ConsortiumAdvice = "Consortium conseillé avec : un chef de file expérimenté (ONG internationale ou université), "
                                      + "les institutions du territoire (ANEF/PNI, communes) et les associations pastorales.";
            }
            else
            {
                (card.Financier, card.FinancierJustification) = (3, $"montant {FormatAmount(amount, info.Currency)} ; part AFMI, frais de structure et cofinancement à vérifier.");
            }

            // 4. Faisabilité : délai de préparation restant et pièces exigées
            bool heavy = info.HeavyDocuments || VeilleAxes.ContainsAny(text, HeavyDocumentKeywords);
            (card.Faisabilite, card.FaisabiliteJustification) = ScoreFeasibility(info.Deadline, today, heavy);

            // 5. Valeur stratégique
            var reasons = new List<string>();
            int strategic = 2;
            if (VeilleAxes.ContainsAny(text, NetworkKeywords)) { strategic++; reasons.Add("réseau Forêts Modèles (RMFM, RAFM, RIFM)"); }
            if (VeilleAxes.ContainsAny(text, ContinuityKeywords)) { strategic++; reasons.Add("continuité de RESCOM, de l'APAC ou d'un bailleur partenaire"); }
            if (info.IsNewDonor) { strategic++; reasons.Add("nouveau bailleur"); }
            if (!string.IsNullOrEmpty(info.LeastCoveredAxis) && card.Axes.Contains(info.LeastCoveredAxis, StringComparer.OrdinalIgnoreCase))
            {
                strategic++;
                reasons.Add($"bonus : {info.LeastCoveredAxis} est l'axe le moins couvert de la base");
            }
            card.Strategique = Math.Min(MaxNote, strategic);
            card.StrategiqueJustification = reasons.Count == 0 ? "pas d'apport stratégique particulier repéré." : string.Join(" ; ", reasons) + ".";

            ApplyCapAndPriority(card, info.Confirmation);
            return card;
        }

        /// <summary>Réapplique bornes, plafond et classement après une correction manuelle des notes.</summary>
        public static void Recompute(GrantOpportunity opp)
        {
            opp.ScorePertinence = Clamp(opp.ScorePertinence);
            opp.ScoreEligibilite = Clamp(opp.ScoreEligibilite);
            opp.ScoreFinancier = Clamp(opp.ScoreFinancier);
            opp.ScoreFaisabilite = Clamp(opp.ScoreFaisabilite);
            opp.ScoreStrategique = Clamp(opp.ScoreStrategique);

            int raw = opp.ScorePertinence + opp.ScoreEligibilite + opp.ScoreFinancier + opp.ScoreFaisabilite + opp.ScoreStrategique;
            opp.ScoreTotal = CappedTotal(raw, opp.Confirmation);
            opp.Priority = PriorityOf(opp.ScoreTotal);
        }

        /// <summary>Copie la notation dans l'appel du pipeline.</summary>
        public static void Apply(VeilleScoreCard card, GrantOpportunity opp)
        {
            opp.ScorePertinence = card.Pertinence;
            opp.ScoreEligibilite = card.Eligibilite;
            opp.ScoreFinancier = card.Financier;
            opp.ScoreFaisabilite = card.Faisabilite;
            opp.ScoreStrategique = card.Strategique;
            opp.ScoreTotal = card.Total;
            opp.Priority = card.Priority;
            opp.ScoreJustification = card.Justification;
            opp.VeilleAxes = VeilleAxes.Join(card.Axes);
            opp.ConsortiumAdvice = card.ConsortiumAdvice;
            if (card.IsIndividualOnly && !opp.Notes.Contains(IndividualMention, StringComparison.Ordinal))
            {
                opp.Notes = string.IsNullOrWhiteSpace(opp.Notes) ? IndividualMention : $"{opp.Notes}\n{IndividualMention}";
            }
        }

        public static int CappedTotal(int rawTotal, string? confirmation) =>
            confirmation == VeilleConfirmation.Confirme ? rawTotal : Math.Min(rawTotal, UnconfirmedCap);

        public static string PriorityOf(int total) =>
            total >= PriorityAThreshold ? "A" : total >= PriorityBThreshold ? "B" : "C";

        /// <summary>Date limite à moins de 10 jours (et pas encore passée).</summary>
        public static bool IsTooLate(DateTime? deadline, DateTime today) =>
            deadline is { } d && d.Date >= today.Date && (d.Date - today.Date).TotalDays < TooLateDays;

        public static bool IsPast(DateTime? deadline, DateTime today) =>
            deadline is { } d && d.Date < today.Date;

        private static void ApplyCapAndPriority(VeilleScoreCard card, string confirmation)
        {
            card.Total = CappedTotal(card.RawTotal, confirmation);
            card.IsCapped = card.Total < card.RawTotal;
            card.Priority = PriorityOf(card.Total);
        }

        private static (int, string) ScoreFeasibility(DateTime? deadline, DateTime today, bool heavy)
        {
            if (deadline is not { } d)
                return (3, "date limite [NON PRÉCISÉ — à vérifier].");

            double weeks = (d.Date - today.Date).TotalDays / 7.0;
            string delay = weeks < 0 ? "date limite dépassée" : $"{weeks.ToString("0.#", CultureInfo.GetCultureInfo("fr-FR"))} semaines de préparation";
            string docs = heavy ? "pièces lourdes (audits, états financiers…)" : "pièces courantes";

            if (weeks < 0) return (0, $"{delay}.");
            if (weeks > 6) return heavy ? (3, $"{delay}, {docs}.") : (5, $"{delay}, {docs}.");
            if (weeks >= 3) return (3, $"{delay}, {docs}.");
            return heavy ? (1, $"{delay}, {docs}.") : (2, $"{delay}, {docs}.");
        }

        /// <summary>
        /// Seuil indicatif à partir duquel un consortium est conseillé (≈ 500 000 EUR).
        /// Il ne change jamais la note à la baisse.
        /// </summary>
        private static bool IsLargeAmount(decimal amount, string? currency) =>
            (currency ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "MAD" or "DH" or "DHS" => amount >= 5_000_000m,
                "FCFA" or "XOF" or "XAF" => amount >= 300_000_000m,
                _ => amount >= 500_000m,
            };

        private static string FormatAmount(decimal amount, string? currency) =>
            $"{amount.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} {(string.IsNullOrWhiteSpace(currency) ? "[devise NON PRÉCISÉE]" : currency)}";

        private static int Clamp(int note) => Math.Clamp(note, 0, MaxNote);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Services.Research
{
    public static class DocumentKinds
    {
        public const string CallForProposals = "AppelAProjets";
        public const string Study = "EtudeDiagnostic";
        public const string Report = "Rapport";
        public const string Template = "Canevas";
        public const string Statistics = "DonneesStatistiques";
        public const string WebArticle = "ArticleWeb";
        public const string Other = "Autre";

        public static string Label(string? kind) => kind switch
        {
            CallForProposals => "Appel à projets",
            Study => "Étude / diagnostic",
            Report => "Rapport",
            Template => "Canevas / formulaire",
            Statistics => "Données statistiques",
            WebArticle => "Article / page web",
            _ => "Autre document"
        };
    }

    public class KeyStatistic
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>Valeur retrouvée telle quelle dans le texte source (contrôle anti-invention).</summary>
        public bool Verified { get; set; }
    }

    public class BeneficiaryMention
    {
        public string Group { get; set; } = string.Empty;
        public string Count { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    /// <summary>Résultat structuré de l'analyse d'un document ou d'une page web (ADR 25).</summary>
    public class DocumentInsights
    {
        public string DocumentKind { get; set; } = DocumentKinds.Other;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;

        // Exigences d'un appel à projets
        public string Donor { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string Deadline { get; set; } = string.Empty;
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int? MaxDurationMonths { get; set; }
        public decimal? CofinancingPercent { get; set; }
        public List<string> EligibilityCriteria { get; set; } = new();
        public List<string> PriorityThemes { get; set; } = new();
        public List<string> RequiredSections { get; set; } = new();
        public List<string> EvaluationCriteria { get; set; } = new();

        // Connaissances utiles au montage
        public List<KeyStatistic> Statistics { get; set; } = new();
        public List<string> Problems { get; set; } = new();
        public List<string> Needs { get; set; } = new();
        public List<string> Opportunities { get; set; } = new();
        public List<string> Threats { get; set; } = new();
        public List<BeneficiaryMention> Beneficiaries { get; set; } = new();
        public string TerritoryInfo { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();

        /// <summary>« IA » ou « Extraction automatique » (sans IA).</summary>
        public string Method { get; set; } = "IA";

        /// <summary>Faits chiffrés de la réponse IA introuvables dans le texte source (à vérifier).</summary>
        public List<string> UnverifiedFacts { get; set; } = new();

        [JsonIgnore]
        public DateTime? DeadlineDate => FactExtraction.TryParseDate(Deadline);

        [JsonIgnore]
        public bool IsCallForProposals => DocumentKind == DocumentKinds.CallForProposals;

        /// <summary>Rendu Markdown lisible (affichage et compatibilité avec l'ancien champ AiSummary).</summary>
        public string ToMarkdown(string currencyFallback = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### {(string.IsNullOrWhiteSpace(Title) ? DocumentKinds.Label(DocumentKind) : Title)}");
            sb.AppendLine($"*{DocumentKinds.Label(DocumentKind)} — analyse : {Method}*");
            if (!string.IsNullOrWhiteSpace(Summary)) sb.AppendLine().AppendLine(Summary);

            string cur = string.IsNullOrWhiteSpace(Currency) ? currencyFallback : Currency;
            var req = new List<string>();
            if (!string.IsNullOrWhiteSpace(Donor)) req.Add($"**Bailleur :** {Donor}{(string.IsNullOrWhiteSpace(Program) ? "" : $" — {Program}")}");
            if (!string.IsNullOrWhiteSpace(Deadline)) req.Add($"**Date limite :** {Deadline}");
            if (BudgetMin.HasValue || BudgetMax.HasValue) req.Add($"**Montant :** {(BudgetMin.HasValue ? $"{BudgetMin:N0}" : "?")} – {(BudgetMax.HasValue ? $"{BudgetMax:N0}" : "?")} {cur}");
            if (MaxDurationMonths.HasValue) req.Add($"**Durée maximale :** {MaxDurationMonths} mois");
            if (CofinancingPercent.HasValue) req.Add($"**Cofinancement exigé :** {CofinancingPercent:0.#} %");
            if (req.Any()) sb.AppendLine().AppendLine(string.Join("  \n", req));

            void Section(string title, IEnumerable<string> items)
            {
                var list = items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
                if (!list.Any()) return;
                sb.AppendLine().AppendLine($"**{title}**");
                foreach (var i in list) sb.AppendLine($"- {i}");
            }

            Section("Critères d'éligibilité", EligibilityCriteria);
            Section("Thématiques prioritaires", PriorityThemes);
            Section("Rubriques exigées", RequiredSections);
            Section("Critères d'évaluation", EvaluationCriteria);
            Section("Données chiffrées", Statistics.Select(s => $"{s.Label} : {s.Value} {s.Unit}{(string.IsNullOrWhiteSpace(s.Year) ? "" : $" ({s.Year})")}{(string.IsNullOrWhiteSpace(s.Location) ? "" : $" — {s.Location}")}{(s.Verified ? "" : " ⚠ à vérifier")}"));
            Section("Problèmes relevés", Problems);
            Section("Besoins", Needs);
            Section("Bénéficiaires", Beneficiaries.Select(b => $"{b.Group}{(string.IsNullOrWhiteSpace(b.Count) ? "" : $" ({b.Count})")}{(string.IsNullOrWhiteSpace(b.Details) ? "" : $" : {b.Details}")}"));
            Section("Opportunités", Opportunities);
            Section("Menaces", Threats);
            Section("Recommandations", Recommendations);
            if (!string.IsNullOrWhiteSpace(TerritoryInfo)) sb.AppendLine().AppendLine($"**Territoire :** {TerritoryInfo}");
            if (UnverifiedFacts.Any()) sb.AppendLine().AppendLine($"⚠ {UnverifiedFacts.Count} donnée(s) chiffrée(s) non retrouvée(s) dans le texte source : {string.Join(" ; ", UnverifiedFacts.Take(6))}");
            return sb.ToString().Trim();
        }
    }

    /// <summary>
    /// Extraction déterministe (sans IA) et vérification des faits chiffrés.
    /// Sert de repli hors connexion et de garde-fou contre les chiffres inventés par l'IA.
    /// </summary>
    public static class FactExtraction
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Compiled;
        private static readonly Regex DigitsOnly = new(@"\d", RegexOptions.Compiled);
        private static readonly Regex AmountRegex = new(@"(\d{1,3}(?:[   .,]\d{3})+|\d+(?:[.,]\d+)?)\s*(millions?|mds?|milliards?|k)?\s*(€|eur(?:os)?|euros?|\$|usd|dollars?|mad|dh|dirhams?)(?!\p{L})", Opts);
        private static readonly Regex DurationRegex = new(@"(\d{1,3})\s*mois", Opts);
        private static readonly Regex DurationYearsRegex = new(@"(\d{1,2})\s*ans?\b", Opts);
        private static readonly Regex PercentRegex = new(@"(\d{1,3}(?:[.,]\d+)?)\s*%", Opts);
        private static readonly Regex DeadlineRegex = new(@"(date\s+limite|deadline|au\s+plus\s+tard\s+le|avant\s+le|clôture|cloture|closing\s+date)[^\n\d]{0,40}(\d{1,2}[\s/.-]+(?:\d{1,2}|[a-zéûô]+)[\s/.-]+\d{2,4})", Opts);
        private static readonly string[] CallKeywords = { "appel à projets", "appel a projets", "appel à propositions", "appel a propositions", "call for proposals", "call for applications", "candidature", "éligibilité", "eligibilite", "date limite de soumission" };

        /// <summary>Vrai si les chiffres significatifs de <paramref name="value"/> figurent dans le texte (séparateurs ignorés).</summary>
        public static bool AppearsInSource(string? value, string sourceDigitsOnly)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            string digits = string.Concat(DigitsOnly.Matches(value).Select(m => m.Value)).TrimStart('0');
            if (digits.Length == 0) return true; // aucune donnée chiffrée à vérifier
            // « 12,5 » → « 125 » ; « 120 000 » → « 120000 » ; « 1.2 million » ne peut pas être vérifié littéralement
            return sourceDigitsOnly.Contains(digits, StringComparison.Ordinal);
        }

        /// <summary>Texte réduit à ses chiffres et lettres (séparateurs de milliers et décimales retirés entre chiffres).</summary>
        public static string NormalizeDigits(string text) =>
            Regex.Replace(text ?? string.Empty, @"(?<=\d)[\s  .,'](?=\d)", "");

        /// <summary>Marque les statistiques vérifiées et liste les faits chiffrés introuvables dans la source.</summary>
        public static void VerifyAgainstSource(DocumentInsights insights, string sourceText)
        {
            string source = NormalizeDigits(sourceText);
            insights.UnverifiedFacts.Clear();
            foreach (var stat in insights.Statistics)
            {
                stat.Verified = AppearsInSource(stat.Value, source);
                if (!stat.Verified) insights.UnverifiedFacts.Add($"{stat.Label} = {stat.Value}");
            }
            void Check(string label, decimal? value)
            {
                if (value.HasValue && !AppearsInSource(value.Value.ToString("0.##", CultureInfo.InvariantCulture), source))
                    insights.UnverifiedFacts.Add($"{label} = {value:N0}");
            }
            Check("Montant minimum", insights.BudgetMin);
            Check("Montant maximum", insights.BudgetMax);
            if (insights.MaxDurationMonths.HasValue && !AppearsInSource(insights.MaxDurationMonths.Value.ToString(CultureInfo.InvariantCulture), source)
                && !AppearsInSource((insights.MaxDurationMonths.Value / 12).ToString(CultureInfo.InvariantCulture), source))
                insights.UnverifiedFacts.Add($"Durée maximale = {insights.MaxDurationMonths} mois");
        }

        /// <summary>Analyse de base sans IA : nature, montants, durée, cofinancement, date limite, résumé.</summary>
        public static DocumentInsights Heuristic(string text, string title)
        {
            var insights = new DocumentInsights { Title = title, Method = "Extraction automatique (sans IA)" };
            string lower = text.ToLowerInvariant();
            int callHits = CallKeywords.Count(k => lower.Contains(k));
            insights.DocumentKind = callHits >= 2 ? DocumentKinds.CallForProposals : DocumentKinds.Other;

            var amounts = AmountRegex.Matches(text)
                .Select(m => (Value: ParseAmount(m.Groups[1].Value, m.Groups[2].Value), Currency: NormalizeCurrency(m.Groups[3].Value)))
                .Where(a => a.Value > 0)
                .ToList();
            if (amounts.Count > 0)
            {
                insights.BudgetMax = amounts.Max(a => a.Value);
                if (amounts.Count > 1) insights.BudgetMin = amounts.Min(a => a.Value);
                insights.Currency = amounts.GroupBy(a => a.Currency).OrderByDescending(g => g.Count()).First().Key;
            }

            var months = DurationRegex.Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Where(v => v is >= 3 and <= 120).ToList();
            if (months.Count > 0) insights.MaxDurationMonths = months.Max();
            else
            {
                var years = DurationYearsRegex.Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Where(v => v is >= 1 and <= 10).ToList();
                if (years.Count > 0 && insights.IsCallForProposals) insights.MaxDurationMonths = years.Max() * 12;
            }

            foreach (Match m in PercentRegex.Matches(text))
            {
                int start = Math.Max(0, m.Index - 80);
                string around = lower.Substring(start, Math.Min(lower.Length - start, 160));
                if (around.Contains("cofinanc") || around.Contains("co-financ") || around.Contains("contribution propre") || around.Contains("apport"))
                {
                    insights.CofinancingPercent = decimal.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                    break;
                }
            }

            var deadline = DeadlineRegex.Match(text);
            if (deadline.Success) insights.Deadline = deadline.Groups[2].Value.Trim();

            string compact = Regex.Replace(text, @"\s+", " ").Trim();
            insights.Summary = compact.Length > 700 ? compact[..697] + "…" : compact;
            return insights;
        }

        private static decimal ParseAmount(string number, string multiplier)
        {
            string digits = Regex.Replace(number, @"[   ]", "");
            // « 1.200.000 » / « 1,200,000 » : séparateurs de milliers ; « 1,5 » : décimale
            if (Regex.IsMatch(digits, @"^\d{1,3}([.,]\d{3})+$")) digits = digits.Replace(".", "").Replace(",", "");
            else digits = digits.Replace(',', '.');
            if (!decimal.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return 0;
            return multiplier.ToLowerInvariant() switch
            {
                var m when m.StartsWith("million") => value * 1_000_000m,
                var m when m.StartsWith("milliard") || m.StartsWith("md") => value * 1_000_000_000m,
                "k" => value * 1_000m,
                _ => value
            };
        }

        private static string NormalizeCurrency(string raw) => raw.ToLowerInvariant() switch
        {
            "€" or "eur" or "euro" or "euros" => "EUR",
            "$" or "usd" or "dollar" or "dollars" => "USD",
            _ => "MAD"
        };

        private static readonly string[] DateFormats = { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d.M.yyyy", "yyyy-MM-dd", "d MMMM yyyy", "dd MMMM yyyy", "d MMM yyyy" };

        public static DateTime? TryParseDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim().TrimEnd('.');
            foreach (var culture in new[] { new CultureInfo("fr-FR"), CultureInfo.InvariantCulture, new CultureInfo("en-US") })
            {
                if (DateTime.TryParseExact(t, DateFormats, culture, DateTimeStyles.None, out var exact)) return exact;
            }
            var iso = Regex.Match(t, @"\d{4}-\d{2}-\d{2}");
            if (iso.Success && DateTime.TryParse(iso.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate)) return isoDate;
            return DateTime.TryParse(t, new CultureInfo("fr-FR"), DateTimeStyles.None, out var fr) ? fr : null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>Un axe du périmètre de veille (A1 à A10, approche paysage).</summary>
    public sealed class VeilleAxis
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;

        /// <summary>
        /// Mots-clés FR/EN/AR, sans accents. « mot* » = racine (forestier, forestière) ;
        /// « mot » = mot entier, pluriel en s/x/es admis ; un mot arabe est cherché tel quel (préfixes « ال »…).
        /// </summary>
        public string[] Keywords { get; init; } = Array.Empty<string>();

        public string Display => $"{Code} {Label}";
    }

    /// <summary>
    /// Les 10 axes de veille de l'AFMI (méthode de veille du 10/11/2026) et la détection sans IA des axes touchés.
    /// Distincts des 5 axes du plan stratégique (afmi_axes.json), qui restent utilisés par le profil et la formulation.
    /// </summary>
    public static class VeilleAxes
    {
        public static readonly IReadOnlyList<VeilleAxis> All = new List<VeilleAxis>
        {
            new() { Code = "A1", Label = "Forêts et biodiversité", Keywords = new[]
            {
                "foret", "forest", "forestier*", "forestry", "reboisement", "reforest*", "afforest*", "boisement",
                "cedre", "cedraie", "cedar", "biodiversit*", "aire protegee", "aires protegees", "protected area",
                "parc national", "national park", "apac", "icca", "incendie", "wildfire", "feux de foret",
                "faune", "flore", "wildlife", "ecosystem*", "ecosysteme*", "conservation",
                "غاب", "تشجير", "التنوع البيولوجي", "الأرز", "المحميات", "حرائق"
            }},
            new() { Code = "A2", Label = "Agriculture, élevage, pastoralisme", Keywords = new[]
            {
                "agricult*", "agroecolog*", "agroforest*", "elevage", "livestock", "pastoral*", "parcours",
                "rangeland", "agdal", "transhuman*", "montagne", "mountain", "cooperative", "apicult*",
                "beekeep*", "securite alimentaire", "food security", "farmer", "agriculteur", "eleveur", "herder",
                "فلاح", "زراع", "رعي", "مراعي", "تعاوني", "نحل", "الأمن الغذائي"
            }},
            new() { Code = "A3", Label = "Eau et sols", Keywords = new[]
            {
                "eau", "water", "bassin versant", "watershed", "erosion", "desertification", "sol", "soil",
                "land degradation", "degradation des terres", "irrigation", "hydrique", "drought", "secheresse",
                "nappe", "groundwater",
                "ماء", "مياه", "تعرية", "تصحر", "تربة", "جفاف"
            }},
            new() { Code = "A4", Label = "Énergie et climat", Keywords = new[]
            {
                "energ*", "renouvelable", "renewable", "solaire", "solar", "foyer ameliore", "foyers ameliores",
                "cookstove", "efficacite energetique", "energy efficiency", "climat*", "climate", "adaptation",
                "attenuation", "mitigation", "carbone", "carbon", "solutions fondees sur la nature",
                "nature-based solution", "sfn", "resilien*",
                "طاق", "مناخ", "تكيف", "كربون"
            }},
            new() { Code = "A5", Label = "Développement local et économie verte", Keywords = new[]
            {
                "developpement local", "local development", "economie verte", "green economy", "economie sociale",
                "social economy", "ess", "entrepreneuriat", "entrepreneur*", "activites generatrices",
                "income generat*", "livelihood", "agr", "terroir", "produits forestiers non ligneux", "pfnl",
                "non-timber", "ntfp", "emploi", "employment", "green job", "value chain", "chaine de valeur",
                "chaines de valeur", "filiere",
                "تنمية محلية", "اقتصاد أخضر", "اقتصاد اجتماعي", "تشغيل", "مدر للدخل", "منتجات مجالية"
            }},
            new() { Code = "A6", Label = "Écotourisme, culture et patrimoine amazigh", Keywords = new[]
            {
                "ecotouris*", "tourisme durable", "sustainable tourism", "tourisme rural", "rural tourism",
                "patrimoine", "heritage", "culture", "cultural", "culturel*", "amazigh*", "berbere", "tamazight",
                "artisanat", "handicraft", "crafts",
                "سياح", "تراث", "ثقاف", "أمازيغ"
            }},
            new() { Code = "A7", Label = "Femmes, jeunes, communautés", Keywords = new[]
            {
                "femme", "women", "woman", "genre", "gender", "jeune", "youth", "young people", "communaut*",
                "communit*", "autochtone", "indigenous", "girls", "filles",
                "نساء", "المرأة", "شباب", "الساكنة", "مجتمع محلي"
            }},
            new() { Code = "A8", Label = "Gouvernance participative, capacités des OSC, coopération décentralisée", Keywords = new[]
            {
                "gouvernance", "governance", "participati*", "societe civile", "civil society", "osc", "cso",
                "renforcement des capacites", "capacity building", "capacity development", "cooperation decentralisee",
                "decentralized cooperation", "decentralised cooperation", "plaidoyer", "advocacy",
                "collectivites territoriales", "local authorit*",
                "حكامة", "مجتمع مدني", "تقوية القدرات", "تعاون لامركزي"
            }},
            new() { Code = "A9", Label = "Éducation environnementale, recherche-action, numérique et SIG", Keywords = new[]
            {
                "education environnementale", "environmental education", "sensibilisation", "awareness",
                "education", "recherche", "research", "recherche-action", "action research", "scientifi*",
                "innovation", "numerique", "digital", "sig", "gis", "teledetection", "remote sensing",
                "تربية بيئية", "تحسيس", "بحث علمي", "رقمي", "نظم المعلومات الجغرافية"
            }},
            new() { Code = "A10", Label = "One Health, déchets, cadre de vie rural", Keywords = new[]
            {
                "one health", "une seule sante", "sante", "health", "zoonos*", "dechet*", "waste", "recycl*",
                "assainissement", "sanitation", "pollution", "plastique", "plastic", "cadre de vie",
                "living conditions", "hygiene",
                "صحة", "نفايات", "تدوير", "تطهير", "تلوث"
            }},
        };

        /// <summary>Cœur de mission (note de pertinence maximale) : cédraie, Agdal, APAC, paysage, Forêt Modèle.</summary>
        public static readonly string[] CoreMissionKeywords =
        {
            "cedraie", "cedre", "cedar", "agdal", "apac", "icca", "paysage", "landscape", "foret modele",
            "model forest", "moyen atlas", "middle atlas", "ifrane", "الأرز", "أكدال", "إفران"
        };

        private static readonly Dictionary<string, Regex> RegexCache = new();
        private static readonly object CacheLock = new();

        public static VeilleAxis? Find(string code) =>
            All.FirstOrDefault(a => string.Equals(a.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Axes A1 à A10 touchés par le texte (titre + résumé), dans l'ordre des axes.</summary>
        public static List<string> DetectAxes(string? text)
        {
            string normalized = VeilleText.Normalize(text);
            if (normalized.Length == 0) return new List<string>();

            return All.Where(axis => axis.Keywords.Any(k => ContainsKeyword(normalized, k)))
                      .Select(axis => axis.Code)
                      .ToList();
        }

        public static bool IsCoreMission(string? text) => ContainsAny(text, CoreMissionKeywords);

        /// <summary>Vrai si le texte contient l'un des mots-clés (même syntaxe que <see cref="VeilleAxis.Keywords"/>).</summary>
        public static bool ContainsAny(string? text, IEnumerable<string> keywords)
        {
            string normalized = VeilleText.Normalize(text);
            return normalized.Length > 0 && keywords.Any(k => ContainsKeyword(normalized, k));
        }

        /// <summary>Codes triés et dédoublonnés (« A1, A2, A10 »).</summary>
        public static string Join(IEnumerable<string> codes) =>
            string.Join(", ", codes.Select(c => c.Trim().ToUpperInvariant())
                                   .Where(c => Find(c) != null)
                                   .Distinct()
                                   .OrderBy(c => int.Parse(c.AsSpan(1), CultureInfo.InvariantCulture)));

        public static List<string> Split(string? codes) =>
            (codes ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(c => c.Trim().ToUpperInvariant())
                                   .Where(c => Find(c) != null)
                                   .Distinct()
                                   .ToList();

        private static bool ContainsKeyword(string normalizedText, string keyword)
        {
            if (keyword.Any(IsArabic))
                return normalizedText.Contains(VeilleText.Normalize(keyword), StringComparison.Ordinal);

            Regex regex;
            lock (CacheLock)
            {
                if (!RegexCache.TryGetValue(keyword, out regex!))
                {
                    bool isStem = keyword.EndsWith('*');
                    string core = Regex.Escape(VeilleText.Normalize(keyword.TrimEnd('*'))).Replace(@"\ ", @"\s+");
                    string pattern = isStem
                        ? $@"(?<![\p{{L}}\p{{N}}]){core}"
                        : $@"(?<![\p{{L}}\p{{N}}]){core}(?:s|x|es)?(?![\p{{L}}\p{{N}}])";
                    regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
                    RegexCache[keyword] = regex;
                }
            }
            return regex.IsMatch(normalizedText);
        }

        private static bool IsArabic(char c) => c >= '؀' && c <= 'ۿ';
    }

    /// <summary>Normalisation de texte pour la veille : minuscules, sans accents, apostrophes et espaces unifiés.</summary>
    public static class VeilleText
    {
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex Years = new(@"(?<!\d)(19|20)\d{2}(?!\d)", RegexOptions.Compiled);
        private static readonly Regex Punctuation = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            string decomposed = text.Replace('’', '\'').Replace('‘', '\'').ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return Spaces.Replace(sb.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
        }

        /// <summary>Intitulé normalisé pour le dédoublonnage : sans accents, sans année, sans ponctuation.</summary>
        public static string NormalizeTitle(string? title)
        {
            string normalized = Years.Replace(Normalize(title), " ");
            return Spaces.Replace(Punctuation.Replace(normalized, " "), " ").Trim();
        }
    }
}

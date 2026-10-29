using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Services.Language
{
    /// <summary>Langues de travail de l'application (ADR 26).</summary>
    public static class AppLanguages
    {
        public const string French = "fr";
        public const string Arabic = "ar";
        public const string English = "en";
        public const string Auto = "auto";

        public static readonly string[] All = { French, Arabic, English };

        public static string Name(string? code) => code switch
        {
            French => "français",
            Arabic => "arabe",
            English => "anglais",
            _ => "langue d'origine"
        };

        public static string NativeName(string? code) => code switch
        {
            French => "Français",
            Arabic => "العربية",
            English => "English",
            _ => "Automatique"
        };

        /// <summary>Normalise un libellé (« Francais », « Arabe », « English », « ar »…) en code de langue.</summary>
        public static string? FromLabel(string? label)
        {
            string l = (label ?? string.Empty).Trim().ToLowerInvariant();
            if (l.StartsWith("fr")) return French;
            if (l.StartsWith("ar") || l.Contains("عرب")) return Arabic;
            if (l.StartsWith("en") || l.StartsWith("ang")) return English;
            return null;
        }
    }

    public sealed class LanguageDetection
    {
        public string Language { get; init; } = string.Empty;
        public double Confidence { get; init; }
        public bool IsReliable => !string.IsNullOrEmpty(Language) && Confidence >= LanguageGuard.ReliableConfidence;
    }

    /// <summary>
    /// Détection légère (sans IA) de la langue d'un texte parmi le français, l'arabe et l'anglais :
    /// part des lettres arabes, puis mots outils caractéristiques du français et de l'anglais.
    /// Sert à vérifier que la réponse de l'IA est dans la langue attendue (sinon elle est traduite).
    /// </summary>
    public static class LanguageGuard
    {
        public const double ReliableConfidence = 0.7;

        private static readonly Regex CodeBlockRegex = new(@"```.*?```", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex UrlRegex = new(@"https?://\S+|\[à compléter[^\]]*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WordRegex = new(@"\p{L}+", RegexOptions.Compiled);

        private static readonly HashSet<string> FrenchWords = new(StringComparer.Ordinal)
        {
            "le", "la", "les", "des", "du", "de", "un", "une", "et", "est", "pour", "dans", "sur", "avec", "par", "aux", "au",
            "ce", "cette", "ces", "qui", "que", "sont", "être", "pas", "plus", "leur", "leurs", "afin", "ainsi", "entre", "selon",
            "projet", "objectif", "activités", "résultats", "bénéficiaires", "renforcer", "améliorer", "mise", "œuvre", "également"
        };

        private static readonly HashSet<string> EnglishWords = new(StringComparer.Ordinal)
        {
            "the", "and", "of", "to", "in", "for", "is", "are", "with", "on", "by", "this", "that", "these", "will", "be", "as",
            "from", "their", "which", "an", "at", "it", "or", "has", "have", "project", "objective", "activities", "results",
            "beneficiaries", "strengthen", "improve", "implementation", "also", "through", "should"
        };

        /// <summary>Langue dominante du texte (code fr/ar/en) et niveau de confiance (0-1).</summary>
        public static LanguageDetection Detect(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new LanguageDetection();

            string cleaned = UrlRegex.Replace(CodeBlockRegex.Replace(text, " "), " ");
            int arabicLetters = 0, latinLetters = 0;
            foreach (char c in cleaned)
            {
                if (c >= '؀' && c <= 'ۿ' || c >= 'ݐ' && c <= 'ݿ' || c >= 'ﭐ' && c <= '﷿' || c >= 'ﹰ' && c <= '﻿') arabicLetters++;
                else if (char.IsLetter(c) && c < 'ɐ') latinLetters++;
            }

            int letters = arabicLetters + latinLetters;
            if (letters < 12) return new LanguageDetection();

            double arabicShare = arabicLetters / (double)letters;
            if (arabicShare >= 0.5) return new LanguageDetection { Language = AppLanguages.Arabic, Confidence = arabicShare };

            var words = WordRegex.Matches(cleaned.ToLowerInvariant()).Select(m => m.Value).Where(w => w.Length < 20).ToList();
            int fr = words.Count(FrenchWords.Contains);
            int en = words.Count(EnglishWords.Contains);
            // Lettres accentuées : indice fort de français
            fr += cleaned.Count(c => "éèêàùçôîâœ".Contains(c)) / 3;

            if (fr + en == 0) return new LanguageDetection { Language = string.Empty, Confidence = 0 };
            double latinShare = 1 - arabicShare;
            return fr >= en
                ? new LanguageDetection { Language = AppLanguages.French, Confidence = latinShare * fr / (double)(fr + en) }
                : new LanguageDetection { Language = AppLanguages.English, Confidence = latinShare * en / (double)(fr + en) };
        }

        /// <summary>Vrai si la réponse est clairement dans une autre langue que la langue attendue.</summary>
        public static bool IsWrongLanguage(string? response, string expectedLanguage)
        {
            if (string.IsNullOrWhiteSpace(response) || !AppLanguages.All.Contains(expectedLanguage)) return false;
            if (LooksLikeStructuredData(response)) return false;
            var detected = Detect(response);
            return detected.IsReliable && detected.Language != expectedLanguage;
        }

        /// <summary>Réponse JSON / tableau : sa langue n'est pas contrôlée (les champs peuvent être bilingues).</summary>
        public static bool LooksLikeStructuredData(string text)
        {
            string t = text.TrimStart();
            return t.StartsWith("{") || t.StartsWith("[") || t.StartsWith("```json", StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Regex MultilingualRequest = new(@"bilingue|bilingual|trilingue|traduis|tradui[st]|translate|ترجم|ثنائي اللغة", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ArabicRequest = new(@"\b(en|in|vers l')\s*(arabe|arabic)\b|بالعربية|باللغة العربية", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EnglishRequest = new(@"\b(en|in|vers l')\s*(anglais|english)\b|بالإنجليزية|بالانجليزية", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FrenchRequest = new(@"\b(en|in|vers le)\s*(français|francais|french)\b|بالفرنسية", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Langue demandée explicitement par la consigne (« réponds en arabe »…). Renvoie « multi » si plusieurs langues
        /// sont demandées (bilingue, traduction), null si aucune.
        /// </summary>
        public static string? ExplicitRequestedLanguage(string prompt, string systemInstruction)
        {
            string text = prompt + "\n" + systemInstruction;
            if (MultilingualRequest.IsMatch(text)) return "multi";
            var requested = new List<string>();
            if (ArabicRequest.IsMatch(text)) requested.Add(AppLanguages.Arabic);
            if (EnglishRequest.IsMatch(text)) requested.Add(AppLanguages.English);
            if (FrenchRequest.IsMatch(text)) requested.Add(AppLanguages.French);
            return requested.Count switch { 0 => null, 1 => requested[0], _ => "multi" };
        }

        /// <summary>
        /// Langue attendue par défaut d'une invite : celle explicitement demandée (null si plusieurs langues sont demandées),
        /// sinon la langue dominante de l'invite (hors documents externes).
        /// </summary>
        public static string? InferExpectedLanguage(string prompt, string systemInstruction)
        {
            string? explicitLanguage = ExplicitRequestedLanguage(prompt, systemInstruction);
            if (explicitLanguage != null) return explicitLanguage == "multi" ? null : explicitLanguage;
            string ownText = Regex.Replace(prompt, @"<<<UNTRUSTED_DOCUMENT_DATA>>>.*?(<<<END_UNTRUSTED_DOCUMENT_DATA>>>|$)", " ", RegexOptions.Singleline);
            var detected = Detect(ownText + "\n" + systemInstruction);
            return detected.IsReliable ? detected.Language : null;
        }

        /// <summary>Consigne de langue ajoutée aux instructions système.</summary>
        public static string LanguageInstruction(string language) => language switch
        {
            AppLanguages.Arabic => "LANGUE DE RÉPONSE OBLIGATOIRE : arabe standard (العربية الفصحى). Réponds entièrement en arabe, en gardant les chiffres occidentaux (0-9), les sigles et les noms propres tels quels.",
            AppLanguages.English => "MANDATORY RESPONSE LANGUAGE: English. Write the whole answer in English, keeping figures, acronyms and proper names unchanged.",
            _ => "LANGUE DE RÉPONSE OBLIGATOIRE : français. Rédige toute la réponse en français, en gardant les chiffres, sigles et noms propres tels quels."
        };
    }
}

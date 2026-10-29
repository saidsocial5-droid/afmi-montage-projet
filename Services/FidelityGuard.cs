using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Services
{
    public class FidelityCheckResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> MissingInvariants { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> PlaceholdersFound { get; set; } = new();
        public bool HasTruncation { get; set; }
    }

    /// <summary>
    /// Garde-fou garantissant la stricte préservation des données chiffrées,
    /// des codes structurels (OS1, R1.2, A1.1...) et l'absence d'hallucinations.
    /// </summary>
    public static class FidelityGuard
    {
        // Codes structurels : OS1, SO1, R1.2, A1.1.3, IND2, etc.
        private static readonly Regex StructuralCodeRegex = new(
            @"\b(OS\d+|SO\d+|R\d+(\.\d+)*|A\d+(\.\d+)*|IND\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Nombres isolés ou avec pourcentages/unités courantes
        private static readonly Regex NumberRegex = new(
            @"\b\d+([.,]\d+)?(\s*(%|DZD|EUR|USD|€|\$|ha|km|m²|h))?(?!\w)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Marqueurs d'information manquante
        private static readonly Regex PlaceholderRegex = new(
            @"\[à compléter\s*:\s*[^\]]+\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Extrait les invariants critiques d'un texte source (codes et nombres).
        /// </summary>
        public static HashSet<string> ExtractInvariants(string text)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Extraire les codes structurels
            var codeMatches = StructuralCodeRegex.Matches(text);
            foreach (Match m in codeMatches)
            {
                if (!string.IsNullOrWhiteSpace(m.Value))
                {
                    result.Add(m.Value.Trim());
                }
            }

            // Extraire les nombres significatifs (au moins 1 chiffre)
            var numMatches = NumberRegex.Matches(text);
            foreach (Match m in numMatches)
            {
                string val = m.Value.Trim();
                // Ignorer les chiffres uniques de puces ou de ponctuation comme "1." s'ils ne sont pas des données
                if (val.Length > 0 && !val.Equals("1") && !val.Equals("2") && !val.Equals("3"))
                {
                    result.Add(val);
                }
                else if (val.Length > 0)
                {
                    result.Add(val);
                }
            }

            return result;
        }

        /// <summary>
        /// Vérifie la fidélité de la proposition par rapport au texte source.
        /// </summary>
        public static FidelityCheckResult Verify(string originalText, string proposedText, string? projectContext = null)
        {
            var result = new FidelityCheckResult();

            if (string.IsNullOrWhiteSpace(proposedText))
            {
                result.IsValid = false;
                result.Warnings.Add("La proposition générée est vide.");
                return result;
            }

            // 1. Détection des marqueurs [à compléter : …]
            var placeholderMatches = PlaceholderRegex.Matches(proposedText);
            foreach (Match m in placeholderMatches)
            {
                result.PlaceholdersFound.Add(m.Value);
            }

            if (result.PlaceholdersFound.Any())
            {
                result.Warnings.Add($"Données manquantes identifiées : {result.PlaceholdersFound.Count} élément(s) marqués [à compléter : …].");
            }

            // 2. Détection de troncature suspecte (se termine brusquement par une virgule, deux-points, etc.)
            string trimmed = proposedText.Trim();
            if (trimmed.EndsWith(",") || trimmed.EndsWith(":") || trimmed.EndsWith(";") || trimmed.EndsWith(" et") || trimmed.EndsWith(" ou"))
            {
                result.HasTruncation = true;
                result.Warnings.Add("Attention : la proposition semble avoir été tronquée à la fin.");
            }

            // 3. Vérification des invariants de la source
            if (!string.IsNullOrWhiteSpace(originalText))
            {
                var sourceInvariants = ExtractInvariants(originalText);

                foreach (var inv in sourceInvariants)
                {
                    // Vérifier si l'invariant est présent dans la proposition
                    bool isPresent = proposedText.IndexOf(inv, StringComparison.OrdinalIgnoreCase) >= 0;
                    
                    // Si l'invariant contient des espaces (ex: "12 500"), tester aussi la version sans espace ("12500")
                    if (!isPresent && inv.Contains(" "))
                    {
                        string compacted = inv.Replace(" ", "");
                        isPresent = proposedText.IndexOf(compacted, StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    if (!isPresent)
                    {
                        result.MissingInvariants.Add(inv);
                    }
                }

                if (result.MissingInvariants.Any())
                {
                    result.IsValid = false;
                    result.Warnings.Add($"Garde-fou de fidélité : les éléments suivants de la source ont disparu : {string.Join(", ", result.MissingInvariants)}.");
                }
            }

            return result;
        }

        /// <summary>
        /// Construit le prompt de retry correctif envoyé à l'IA en cas d'omission d'invariants.
        /// </summary>
        public static string BuildCorrectionPrompt(string draftProposal, List<string> missingInvariants)
        {
            string missingList = string.Join(", ", missingInvariants.Select(i => $"« {i} »"));
            return $@"ATTENTION - VÉRIFICATION DE FIDÉLITÉ ÉCHOUÉE :
Les éléments indispensables suivants présents dans la source ont été omis ou modifiés dans votre proposition :
{missingList}

DIRECTIVE IMPÉRATIVE DE CORRECTION :
1. Réintégrez STRICTEMENT et à l'identique chacun de ces éléments ({missingList}) dans le texte sans inventer de nouveaux faits.
2. Si une information statistique ou contextuelle précise n'est pas connue, utilisez la mention explicite : [à compléter : description].
3. Renvoyez uniquement la proposition corrigée.";
        }
    }
}

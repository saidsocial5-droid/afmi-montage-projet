using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Helpers
{
    public enum DiffChunkType
    {
        Unchanged,
        Added,
        Modified,
        Placeholder
    }

    public class DiffChunk
    {
        public string Text { get; set; } = string.Empty;
        public DiffChunkType Type { get; set; } = DiffChunkType.Unchanged;
    }

    /// <summary>
    /// Utilitaire de calcul de comparaison différentielle mot à mot (Word-by-word Diff)
    /// et de rendu stylisé dans un RichTextBox WPF.
    /// </summary>
    public static class TextDiffHelper
    {
        private static readonly Regex WordSplitRegex = new(@"(\s+|[.,!?;:()""«»\[\]{}]+|[^\s.,!?;:()""«»\[\]{}]+)", RegexOptions.Compiled);
        private static readonly Regex PlaceholderRegex = new(@"\[à compléter\s*:\s*[^\]]+\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Calcule les fragments différentiels entre l'original et la proposition.
        /// </summary>
        public static List<DiffChunk> ComputeWordDiff(string original, string proposed, TextAssistActionType action)
        {
            var chunks = new List<DiffChunk>();

            if (string.IsNullOrEmpty(proposed))
                return chunks;

            if (string.IsNullOrEmpty(original))
            {
                chunks.Add(new DiffChunk
                {
                    Text = proposed,
                    Type = DiffChunkType.Added
                });
                return chunks;
            }

            // Découper en tokens (mots et séparateurs)
            var origTokens = Tokenize(original);
            var propTokens = Tokenize(proposed);

            var origWordSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in origTokens)
            {
                if (!char.IsWhiteSpace(t[0]) && !char.IsPunctuation(t[0]))
                    origWordSet.Add(t);
            }

            int p = 0;
            while (p < propTokens.Count)
            {
                string token = propTokens[p];

                // Vérifier si c'est un marqueur [à compléter : ...]
                if (token.StartsWith("[") && p + 4 < propTokens.Count)
                {
                    string candidate = string.Concat(propTokens.GetRange(p, Math.Min(10, propTokens.Count - p)));
                    var match = PlaceholderRegex.Match(candidate);
                    if (match.Success && match.Index == 0)
                    {
                        chunks.Add(new DiffChunk
                        {
                            Text = match.Value,
                            Type = DiffChunkType.Placeholder
                        });
                        // Avancer de la longueur consommée
                        int charsCount = match.Value.Length;
                        int consumed = 0;
                        while (p < propTokens.Count && consumed < charsCount)
                        {
                            consumed += propTokens[p].Length;
                            p++;
                        }
                        continue;
                    }
                }

                if (char.IsWhiteSpace(token[0]))
                {
                    chunks.Add(new DiffChunk { Text = token, Type = DiffChunkType.Unchanged });
                }
                else if (char.IsPunctuation(token[0]))
                {
                    chunks.Add(new DiffChunk { Text = token, Type = DiffChunkType.Unchanged });
                }
                else
                {
                    bool isWordInOriginal = origWordSet.Contains(token);

                    if (!isWordInOriginal)
                    {
                        var chunkType = (action == TextAssistActionType.Reformuler)
                            ? DiffChunkType.Modified
                            : DiffChunkType.Added;

                        chunks.Add(new DiffChunk { Text = token, Type = chunkType });
                    }
                    else
                    {
                        chunks.Add(new DiffChunk { Text = token, Type = DiffChunkType.Unchanged });
                    }
                }

                p++;
            }

            return chunks;
        }

        private static List<string> Tokenize(string text)
        {
            var matches = WordSplitRegex.Matches(text);
            var list = new List<string>(matches.Count);
            foreach (Match m in matches)
            {
                list.Add(m.Value);
            }
            return list;
        }

        /// <summary>
        /// Remplit un document FlowDocument WPF avec mise en surbrillance colorée des ajouts et reformulations.
        /// </summary>
        public static FlowDocument CreateHighlightedDiffDocument(string original, string proposed, TextAssistActionType action)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(6),
                LineHeight = 1.4
            };

            var paragraph = new Paragraph();

            var chunks = ComputeWordDiff(original, proposed, action);

            // Couleurs de surbrillance
            var addedBg = new SolidColorBrush(Color.FromRgb(220, 252, 231));      // Vert très clair #DCFCE7
            var addedFg = new SolidColorBrush(Color.FromRgb(22, 101, 52));        // Vert foncé #166534
            
            var modifiedBg = new SolidColorBrush(Color.FromRgb(224, 242, 254));   // Bleu très clair #E0F2FE
            var modifiedFg = new SolidColorBrush(Color.FromRgb(3, 105, 161));     // Bleu foncé #0369A1

            var placeholderBg = new SolidColorBrush(Color.FromRgb(254, 240, 138));// Jaune #FEF08A
            var placeholderFg = new SolidColorBrush(Color.FromRgb(133, 77, 14));  // Marron foncé #854D0E

            var defaultFg = new SolidColorBrush(Color.FromRgb(15, 23, 42));       // Noir ardoise #0F172A

            foreach (var chunk in chunks)
            {
                var run = new Run(chunk.Text);

                switch (chunk.Type)
                {
                    case DiffChunkType.Added:
                        run.Background = addedBg;
                        run.Foreground = addedFg;
                        run.FontWeight = FontWeights.SemiBold;
                        run.ToolTip = "Contenu enrichi / développé par l'IA";
                        break;

                    case DiffChunkType.Modified:
                        run.Background = modifiedBg;
                        run.Foreground = modifiedFg;
                        run.ToolTip = "Terme reformulé pour clarté ou registre bailleur";
                        break;

                    case DiffChunkType.Placeholder:
                        run.Background = placeholderBg;
                        run.Foreground = placeholderFg;
                        run.FontWeight = FontWeights.Bold;
                        run.ToolTip = "Donnée manquante à renseigner manuellement";
                        break;

                    case DiffChunkType.Unchanged:
                    default:
                        run.Foreground = defaultFg;
                        break;
                }

                paragraph.Inlines.Add(run);
            }

            doc.Blocks.Add(paragraph);
            return doc;
        }
    }
}

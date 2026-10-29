using System;
using System.Collections.Generic;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public enum DiffType
    {
        Unchanged,
        Added,
        Deleted
    }

    public class DiffPiece
    {
        public DiffType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public int? OldLineNumber { get; set; }
        public int? NewLineNumber { get; set; }
    }

    /// <summary>
    /// خوارزمية مقارنة النصوص (Diff Engine) لاستخراج الفروقات بين القيمة الأصلية والقيمة المقترحة
    /// </summary>
    public static class TextDiffHelper
    {
        public static List<DiffPiece> CalculateWordDiff(string? oldText, string? newText)
        {
            oldText ??= string.Empty;
            newText ??= string.Empty;

            var oldWords = SplitIntoWords(oldText);
            var newWords = SplitIntoWords(newText);

            int n = oldWords.Count;
            int m = newWords.Count;
            int[,] lcs = new int[n + 1, m + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (oldWords[i] == newWords[j])
                        lcs[i + 1, j + 1] = lcs[i, j] + 1;
                    else
                        lcs[i + 1, j + 1] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }

            var diff = new List<DiffPiece>();
            int x = n, y = m;
            var stack = new Stack<DiffPiece>();

            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0 && oldWords[x - 1] == newWords[y - 1])
                {
                    stack.Push(new DiffPiece { Type = DiffType.Unchanged, Text = oldWords[x - 1] });
                    x--;
                    y--;
                }
                else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
                {
                    stack.Push(new DiffPiece { Type = DiffType.Added, Text = newWords[y - 1] });
                    y--;
                }
                else if (x > 0 && (y == 0 || lcs[x, y - 1] < lcs[x - 1, y]))
                {
                    stack.Push(new DiffPiece { Type = DiffType.Deleted, Text = oldWords[x - 1] });
                    x--;
                }
            }

            while (stack.Count > 0)
            {
                diff.Add(stack.Pop());
            }

            return diff;
        }

        private static List<string> SplitIntoWords(string text)
        {
            var words = new List<string>();
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]) || char.IsPunctuation(text[i]))
                {
                    if (i > start)
                        words.Add(text.Substring(start, i - start));
                    words.Add(text[i].ToString());
                    start = i + 1;
                }
            }
            if (start < text.Length)
            {
                words.Add(text.Substring(start));
            }
            return words;
        }

        public static List<DiffPiece> CalculateLineDiff(string? oldText, string? newText)
        {
            oldText ??= string.Empty;
            newText ??= string.Empty;

            var oldLines = oldText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var newLines = newText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int n = oldLines.Length;
            int m = newLines.Length;
            int[,] lcs = new int[n + 1, m + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (oldLines[i] == newLines[j])
                        lcs[i + 1, j + 1] = lcs[i, j] + 1;
                    else
                        lcs[i + 1, j + 1] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }

            var diff = new List<DiffPiece>();
            int x = n, y = m;
            var stack = new Stack<DiffPiece>();

            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
                {
                    stack.Push(new DiffPiece { Type = DiffType.Unchanged, Text = oldLines[x - 1], OldLineNumber = x, NewLineNumber = y });
                    x--;
                    y--;
                }
                else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
                {
                    stack.Push(new DiffPiece { Type = DiffType.Added, Text = newLines[y - 1], NewLineNumber = y });
                    y--;
                }
                else if (x > 0 && (y == 0 || lcs[x, y - 1] < lcs[x - 1, y]))
                {
                    stack.Push(new DiffPiece { Type = DiffType.Deleted, Text = oldLines[x - 1], OldLineNumber = x });
                    x--;
                }
            }

            while (stack.Count > 0)
            {
                diff.Add(stack.Pop());
            }

            return diff;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace AFMI_Montage_projet_2.Services.Research
{
    public sealed class ExtractedPage
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
    }

    /// <summary>
    /// Extraction du texte lisible d'une page web : retire scripts, styles, menus, en-têtes, pieds de page
    /// et formulaires ; privilégie &lt;main&gt; / &lt;article&gt; ; conserve la structure en paragraphes et listes.
    /// (L'ancienne extraction gardait le code JavaScript et les menus dans le texte envoyé à l'IA.)
    /// </summary>
    public static class HtmlTextExtractor
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

        private static readonly Regex CommentRegex = new(@"<!--.*?-->", Opts);
        private static readonly Regex NoiseBlockRegex = new(@"<(script|style|noscript|svg|iframe|template|canvas|object)\b[^>]*>.*?</\1\s*>", Opts);
        private static readonly Regex LayoutBlockRegex = new(@"<(nav|header|footer|aside|form|button|select)\b[^>]*>.*?</\1\s*>", Opts);
        private static readonly Regex MainRegex = new(@"<(main|article)\b[^>]*>(.*?)</\1\s*>", Opts);
        private static readonly Regex TitleRegex = new(@"<title[^>]*>(.*?)</title>", Opts);
        private static readonly Regex MetaRegex = new(@"<meta\b[^>]*>", Opts);
        private static readonly Regex AttrRegex = new(@"([a-zA-Z:-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.Compiled);
        private static readonly Regex ListItemRegex = new(@"<li\b[^>]*>", Opts);
        private static readonly Regex BlockRegex = new(@"</?(p|div|section|br|h[1-6]|tr|table|ul|ol|li|dd|dt|blockquote|pre|hr)\b[^>]*>", Opts);
        private static readonly Regex CellRegex = new(@"</t[dh]\s*>", Opts);
        private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex SpacesRegex = new(@"[ \t   ]+", RegexOptions.Compiled);

        public static ExtractedPage Extract(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return new ExtractedPage();

            string title = Clean(TitleRegex.Match(html).Groups[1].Value);
            string description = string.Empty;
            foreach (Match meta in MetaRegex.Matches(html))
            {
                var attrs = AttrRegex.Matches(meta.Value)
                    .GroupBy(m => m.Groups[1].Value.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().Groups[2].Success && g.First().Groups[2].Length > 0 ? g.First().Groups[2].Value : g.First().Groups[3].Value, StringComparer.OrdinalIgnoreCase);
                string key = attrs.GetValueOrDefault("name") ?? attrs.GetValueOrDefault("property") ?? string.Empty;
                if (key is "description" or "og:description" && string.IsNullOrEmpty(description)) description = Clean(attrs.GetValueOrDefault("content") ?? string.Empty);
                if (key == "og:title" && string.IsNullOrEmpty(title)) title = Clean(attrs.GetValueOrDefault("content") ?? string.Empty);
            }

            string body = CommentRegex.Replace(html, " ");
            body = NoiseBlockRegex.Replace(body, " ");

            // Contenu principal s'il est identifié et substantiel
            var mains = MainRegex.Matches(body).Select(m => m.Groups[2].Value).Where(m => ToText(m).Length > 400).ToList();
            string content = mains.Count > 0 ? string.Join("\n", mains) : LayoutBlockRegex.Replace(body, " ");

            string text = ToText(content);
            return new ExtractedPage { Title = title, Description = description, Text = text };
        }

        private static string ToText(string html)
        {
            string s = ListItemRegex.Replace(html, "\n• ");
            s = CellRegex.Replace(s, " | ");
            s = BlockRegex.Replace(s, "\n");
            s = TagRegex.Replace(s, " ");
            s = WebUtility.HtmlDecode(s);

            var lines = new List<string>();
            var seenShort = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in s.Split('\n'))
            {
                string line = SpacesRegex.Replace(raw, " ").Trim().Trim('|').Trim();
                if (line.Length == 0 || line == "•") continue;
                // Lignes courtes répétées (liens de menu résiduels, « Partager », « Lire la suite »…)
                if (line.Length < 40 && !seenShort.Add(line)) continue;
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        private static string Clean(string value) => SpacesRegex.Replace(WebUtility.HtmlDecode(TagRegex.Replace(value, " ")), " ").Trim();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>Résultat de l'analyse d'une réponse : flux reconnu, ou page HTML avec ses flux annoncés.</summary>
    public class ParsedFeed
    {
        public bool IsFeed { get; set; }
        public bool IsHtml { get; set; }
        public string? Title { get; set; }
        public List<RssItemCache> Items { get; set; } = new();

        /// <summary>Flux RSS/Atom déclarés par la page HTML (balises &lt;link rel="alternate"&gt;), en URL absolues.</summary>
        public List<string> DiscoveredFeedUrls { get; set; } = new();
    }

    /// <summary>
    /// محلل مرن وشامل لخلاصات RSS 2.0 و Atom و RDF/Dublin Core.
    /// يعتمد كلياً على System.Xml.Linq المدمجة في .NET 9،
    /// مع معالجة مرنة للترميزات والتواريخ المتباينة وتنظيف وسوم HTML.
    /// </summary>
    public static class RssParser
    {
        public const int MaxTitleLength = 500;
        public const int MaxDescriptionLength = 5000;
        public const int MaxExternalIdLength = 255;

        private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex ScriptOrStyleRegex = new(@"<(script|style)[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex XmlDeclEncodingRegex = new(@"^\s*<\?xml[^>]*encoding\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LinkTagRegex = new(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AttributeRegex = new(@"([a-zA-Z:-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.Compiled);
        private static readonly Regex TrailingZoneRegex = new(@"\s([A-Z]{2,5})$", RegexOptions.Compiled);

        /// <summary>Abréviations de fuseaux fréquentes dans les flux (non reconnues par .NET).</summary>
        private static readonly Dictionary<string, string> ZoneAbbreviations = new(StringComparer.Ordinal)
        {
            ["UT"] = "+00:00", ["UTC"] = "+00:00", ["GMT"] = "+00:00", ["Z"] = "+00:00",
            ["EST"] = "-05:00", ["EDT"] = "-04:00", ["CST"] = "-06:00", ["CDT"] = "-05:00",
            ["MST"] = "-07:00", ["MDT"] = "-06:00", ["PST"] = "-08:00", ["PDT"] = "-07:00",
            ["WET"] = "+00:00", ["WEST"] = "+01:00", ["BST"] = "+01:00",
            ["CET"] = "+01:00", ["CEST"] = "+02:00", ["EET"] = "+02:00", ["EEST"] = "+03:00"
        };

        private static readonly string[] DateFormats = new[]
        {
            "r",
            "ddd, dd MMM yyyy HH:mm:ss zzz",
            "ddd, dd MMM yyyy HH:mm:ss",
            "ddd, d MMM yyyy HH:mm:ss zzz",
            "ddd, d MMM yyyy HH:mm:ss",
            "ddd, dd MMM yyyy HH:mm zzz",
            "ddd, d MMM yyyy HH:mm zzz",
            "dd MMM yyyy HH:mm:ss zzz",
            "d MMM yyyy HH:mm:ss zzz",
            "dd MMM yyyy HH:mm:ss",
            "d MMM yyyy HH:mm:ss",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:ss.fffffffzzz",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "yyyy/MM/dd"
        };

        static RssParser()
        {
            // windows-1252, iso-8859-15… déclarés par certains flux institutionnels
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static List<RssItemCache> ParseFeed(string xmlContent, RssFeedConfig feed) =>
            ParseDocument(xmlContent, feed).Items;

        /// <summary>
        /// Analyse une réponse brute. L'encodage est déterminé par le BOM ou la déclaration XML, puis par
        /// l'en-tête HTTP (charset), et à défaut UTF-8.
        /// </summary>
        public static ParsedFeed ParseDocument(byte[] content, RssFeedConfig feed, string? charsetHint = null)
        {
            if (content.Length == 0) return new ParsedFeed();
            return ParseDocument(DecodeText(content, charsetHint), feed);
        }

        public static ParsedFeed ParseDocument(string content, RssFeedConfig feed)
        {
            var result = new ParsedFeed();
            if (string.IsNullOrWhiteSpace(content)) return result;

            // Détection et suppression du BOM éventuel
            content = content.TrimStart('﻿', '​');

            var doc = TryLoadXml(content);
            var root = doc?.Root;
            string rootName = root?.Name.LocalName.ToLowerInvariant() ?? string.Empty;

            if (root == null || rootName == "html")
            {
                if (LooksLikeHtml(content))
                {
                    result.IsHtml = true;
                    result.DiscoveredFeedUrls = DiscoverFeedLinks(content, feed.Url);
                }
                return result;
            }

            if (rootName == "rss" || rootName == "rdf")
            {
                // Format RSS 2.0 / RSS 1.0 (RDF)
                result.IsFeed = true;
                var channel = root.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("channel", StringComparison.OrdinalIgnoreCase));
                if (channel != null) result.Title = NullIfEmpty(CleanText(GetElementValue(channel, "title")));

                var items = root.Descendants().Where(e => e.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase));
                foreach (var item in items)
                {
                    var parsed = ParseRssItem(item, feed);
                    if (parsed != null) result.Items.Add(parsed);
                }
            }
            else if (rootName == "feed")
            {
                // Format Atom
                result.IsFeed = true;
                result.Title = NullIfEmpty(CleanText(GetElementValue(root, "title")));
                string baseUrl = ResolveUrl(root.Attribute(XNamespace.Xml + "base")?.Value, feed.Url) ?? feed.Url;

                var entries = root.Descendants().Where(e => e.Name.LocalName.Equals("entry", StringComparison.OrdinalIgnoreCase));
                foreach (var entry in entries)
                {
                    var parsed = ParseAtomEntry(entry, feed, baseUrl);
                    if (parsed != null) result.Items.Add(parsed);
                }
            }

            return result;
        }

        private static XDocument? TryLoadXml(string content)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore, // Sécurité anti-XXE
                XmlResolver = null,
                IgnoreComments = true
            };

            try
            {
                using var reader = XmlReader.Create(new StringReader(content), settings);
                return XDocument.Load(reader);
            }
            catch (XmlException)
            {
                // Si échec de chargement direct (ex: entités HTML non déclarées &eacute;, etc.)
                try
                {
                    using var reader = XmlReader.Create(new StringReader(SanitizeMalformedXmlEntities(content)), settings);
                    return XDocument.Load(reader);
                }
                catch (XmlException)
                {
                    return null; // Impossible à parser
                }
            }
        }

        /// <summary>Décode les octets reçus selon le BOM, la déclaration XML, l'en-tête HTTP, puis UTF-8.</summary>
        public static string DecodeText(byte[] content, string? charsetHint)
        {
            Encoding? encoding = null;

            // 1. BOM
            if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF) encoding = Encoding.UTF8;
            else if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE) encoding = Encoding.Unicode;
            else if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF) encoding = Encoding.BigEndianUnicode;

            // 2. Déclaration XML (lue en ASCII, toujours présente en tête)
            if (encoding == null)
            {
                string head = Encoding.ASCII.GetString(content, 0, Math.Min(content.Length, 256));
                var match = XmlDeclEncodingRegex.Match(head);
                if (match.Success) encoding = TryGetEncoding(match.Groups[1].Value);
            }

            // 3. En-tête HTTP, 4. UTF-8
            encoding ??= TryGetEncoding(charsetHint) ?? new UTF8Encoding(false);

            string text = encoding.GetString(content);
            return text.TrimStart('﻿');
        }

        private static Encoding? TryGetEncoding(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            try { return Encoding.GetEncoding(name.Trim().Trim('"')); }
            catch (ArgumentException) { return null; }
        }

        private static bool LooksLikeHtml(string content)
        {
            string head = content.Length > 2000 ? content[..2000] : content;
            return head.Contains("<html", StringComparison.OrdinalIgnoreCase) || head.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Découverte automatique : flux RSS/Atom annoncés dans l'en-tête d'une page web
        /// (&lt;link rel="alternate" type="application/rss+xml" href="..."&gt;).
        /// </summary>
        public static List<string> DiscoverFeedLinks(string html, string pageUrl)
        {
            var found = new List<string>();
            foreach (Match tag in LinkTagRegex.Matches(html))
            {
                var attrs = AttributeRegex.Matches(tag.Value)
                    .GroupBy(m => m.Groups[1].Value.ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups[2].Success && g.First().Groups[2].Value.Length > 0
                        ? g.First().Groups[2].Value
                        : g.First().Groups[3].Success && g.First().Groups[3].Value.Length > 0 ? g.First().Groups[3].Value : g.First().Groups[4].Value));

                if (!attrs.TryGetValue("rel", out var rel) || !rel.Split(' ').Contains("alternate", StringComparer.OrdinalIgnoreCase)) continue;
                if (!attrs.TryGetValue("type", out var type)) continue;
                type = type.ToLowerInvariant();
                if (!type.Contains("rss") && !type.Contains("atom") && !type.Contains("rdf")) continue;
                if (!attrs.TryGetValue("href", out var href)) continue;

                var absolute = ResolveUrl(href, pageUrl);
                if (absolute != null && !found.Contains(absolute, StringComparer.OrdinalIgnoreCase)) found.Add(absolute);
            }

            // RSS 2.0 de préférence à Atom quand les deux sont annoncés (même contenu, meilleure compatibilité)
            return found.OrderBy(u => u.Contains("atom", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList();
        }

        private static RssItemCache? ParseRssItem(XElement item, RssFeedConfig feed)
        {
            try
            {
                string title = GetElementValue(item, "title");
                string link = GetElementValue(item, "link");
                string desc = GetElementValue(item, "description");
                string contentEncoded = GetElementValue(item, "encoded"); // content:encoded
                var guidElem = item.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("guid", StringComparison.OrdinalIgnoreCase));
                string guid = guidElem?.Value.Trim() ?? string.Empty;
                string pubDateStr = FirstNonEmpty(item, "pubDate", "date", "published", "updated", "lastBuildDate");

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(contentEncoded))
                    return null;

                // <link> absent : un guid permalien (isPermaLink par défaut à true) fait office de lien
                if (string.IsNullOrWhiteSpace(link) && guid.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(guidElem?.Attribute("isPermaLink")?.Value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    link = guid;
                }
                link = ResolveUrl(link, feed.Url) ?? link;

                string rawBody = !string.IsNullOrWhiteSpace(contentEncoded) ? contentEncoded : desc;
                return BuildItem(feed, title, link, rawBody, guid, pubDateStr);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or UriFormatException)
            {
                return null;
            }
        }

        private static RssItemCache? ParseAtomEntry(XElement entry, RssFeedConfig feed, string baseUrl)
        {
            try
            {
                string title = GetElementValue(entry, "title");
                string entryBase = ResolveUrl(entry.Attribute(XNamespace.Xml + "base")?.Value, baseUrl) ?? baseUrl;

                // Lien de l'entrée : rel="alternate" (ou sans rel), plutôt que « self », « edit » ou « enclosure »
                var links = entry.Elements().Where(e => e.Name.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase)).ToList();
                var linkElem = links.FirstOrDefault(l => (l.Attribute("rel")?.Value ?? "alternate") == "alternate" &&
                                                         (l.Attribute("type") == null || l.Attribute("type")!.Value.Contains("html", StringComparison.OrdinalIgnoreCase)))
                               ?? links.FirstOrDefault(l => (l.Attribute("rel")?.Value ?? "alternate") == "alternate")
                               ?? links.FirstOrDefault();
                string link = linkElem?.Attribute("href")?.Value ?? linkElem?.Value ?? string.Empty;
                link = ResolveUrl(link, entryBase) ?? link;

                string summary = GetElementValue(entry, "summary");
                string content = GetElementValue(entry, "content");
                string id = GetElementValue(entry, "id");
                string publishedStr = FirstNonEmpty(entry, "published", "updated", "date", "issued", "pubDate");

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(content))
                    return null;

                string rawBody = !string.IsNullOrWhiteSpace(content) ? content : summary;
                return BuildItem(feed, title, link, rawBody, id, publishedStr);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or UriFormatException)
            {
                return null;
            }
        }

        private static RssItemCache BuildItem(RssFeedConfig feed, string title, string link, string rawBody, string id, string dateString)
        {
            string normalizedLink = NormalizeUrl(link);
            string externalId = !string.IsNullOrWhiteSpace(id) ? id.Trim() : normalizedLink;
            if (string.IsNullOrWhiteSpace(externalId)) externalId = ComputeFallbackId(title, link);
            if (externalId.Length > MaxExternalIdLength) externalId = ComputeFallbackId(externalId, string.Empty);

            return new RssItemCache
            {
                FeedId = feed.Id,
                FeedName = feed.Name,
                Donor = ExtractDonorName(feed),
                ExternalId = externalId,
                Title = Truncate(CleanText(title), MaxTitleLength),
                CleanDescription = Truncate(CleanHtml(rawBody), MaxDescriptionLength),
                Link = !string.IsNullOrWhiteSpace(normalizedLink) ? normalizedLink : link,
                PublishDate = ParseDate(dateString),
                FetchedAt = DateTime.UtcNow,
                IsRead = false,
                IsHidden = false,
                IsInPipeline = false,
                IsScored = false
            };
        }

        private static string FirstNonEmpty(XElement parent, params string[] localNames)
        {
            foreach (var name in localNames)
            {
                var value = GetElementValue(parent, name);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            return string.Empty;
        }

        private static string GetElementValue(XElement parent, string localName)
        {
            var elem = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
            return elem?.Value?.Trim() ?? string.Empty;
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

        /// <summary>Résout un lien éventuellement relatif par rapport à l'adresse du flux ou de la page.</summary>
        public static string? ResolveUrl(string? href, string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;
            href = href.Trim();
            if (Uri.TryCreate(href, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
                return absolute.AbsoluteUri;
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, href, out var combined) && (combined.Scheme == Uri.UriSchemeHttp || combined.Scheme == Uri.UriSchemeHttps))
                return combined.AbsoluteUri;
            return null;
        }

        public static string CleanHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // 1. Supprimer scripts et styles
            string noScript = ScriptOrStyleRegex.Replace(html, " ");

            // 2. Supprimer les balises HTML
            string noTags = HtmlTagRegex.Replace(noScript, " ");

            // 3. Décoder les entités HTML (&amp; &eacute; etc.)
            string decoded = WebUtility.HtmlDecode(noTags);

            // 4. Normaliser les apostrophes et guillemets typographiques pour la recherche
            decoded = decoded.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"');

            // 5. Normaliser les espaces et sauts de ligne
            string clean = MultiSpaceRegex.Replace(decoded, " ").Trim();

            return clean;
        }

        public static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string decoded = WebUtility.HtmlDecode(HtmlTagRegex.Replace(text, " "));
            decoded = decoded.Replace('’', '\'').Replace('‘', '\'').Replace('“', '"').Replace('”', '"');
            return MultiSpaceRegex.Replace(decoded, " ").Trim();
        }

        public static DateTime? ParseDate(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString)) return null;

            string cleaned = MultiSpaceRegex.Replace(dateString.Trim(), " ");

            // Abréviation de fuseau en fin de chaîne (EST, CEST, PDT…) : remplacée par son décalage
            var zone = TrailingZoneRegex.Match(cleaned);
            if (zone.Success && ZoneAbbreviations.TryGetValue(zone.Groups[1].Value, out var offset))
                cleaned = cleaned[..zone.Index] + " " + offset;

            // 1. Essai direct via DateTimeOffset (reconnaît nativement les formats RFC 822 / 1123 / ISO 8601 avec offsets)
            if (DateTimeOffset.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dto))
            {
                return dto.UtcDateTime;
            }

            // 2. Formats standards prédéfinis
            foreach (var format in DateFormats)
            {
                if (DateTimeOffset.TryParseExact(cleaned, format, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var exact))
                {
                    return exact.UtcDateTime;
                }
            }

            // 3. Essai avec les formats de date français (ex: "19 octobre 2026")
            if (DateTime.TryParse(cleaned, new CultureInfo("fr-FR"), DateTimeStyles.None, out var frDt))
            {
                return DateTime.SpecifyKind(frDt, DateTimeKind.Utc);
            }

            // 4. Essai avec les formats de date arabes
            if (DateTime.TryParse(cleaned, new CultureInfo("ar-MA"), DateTimeStyles.None, out var arDt))
            {
                return DateTime.SpecifyKind(arDt, DateTimeKind.Utc);
            }

            return null;
        }

        public static string NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            try
            {
                var uri = new Uri(url.Trim());
                // Retirer les paramètres UTM et de pistage
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var cleanQuery = new StringBuilder();
                foreach (string? key in queryParams.AllKeys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    string lowerKey = key.ToLowerInvariant();
                    if (lowerKey.StartsWith("utm_") || lowerKey == "fbclid" || lowerKey == "gclid" || lowerKey == "ref")
                        continue;

                    if (cleanQuery.Length > 0) cleanQuery.Append("&");
                    cleanQuery.Append($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(queryParams[key] ?? "")}");
                }

                var builder = new UriBuilder(uri)
                {
                    Query = cleanQuery.ToString()
                };

                string result = builder.Uri.ToString();
                if (result.EndsWith("/") && builder.Path.Length > 1)
                {
                    result = result.TrimEnd('/');
                }
                return result;
            }
            catch (UriFormatException)
            {
                return url.Trim();
            }
        }

        private static string ComputeFallbackId(string title, string link)
        {
            string combined = (title + "|" + link).Trim().ToLowerInvariant();
            byte[] hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(combined));
            return "auto_" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }

        public static string ExtractDonorName(RssFeedConfig feed)
        {
            string name = feed.Name.ToUpperInvariant();
            if (name.Contains("GEF")) return "GEF";
            if (name.Contains("PNUD") || name.Contains("UNDP")) return "PNUD";
            if (name.Contains("UNION EUROPÉENNE") || name.Contains("EUROPEAN") || name.Contains("COMMISSION") || name.Contains("UE")) return "Union Européenne";
            if (name.Contains("BANQUE MONDIALE") || name.Contains("WORLD BANK")) return "Banque Mondiale";
            if (name.Contains("AFD") || name.Contains("FFEM")) return "AFD";
            if (name.Contains("UICN") || name.Contains("IUCN")) return "UICN";
            if (name.Contains("FAO")) return "FAO";
            if (name.Contains("CEPF")) return "CEPF";
            if (name.Contains("ADAPTATION")) return "Fonds d'adaptation";
            if (name.Contains("ONU") || name.Contains("UN NEWS") || name.Contains("NATIONS UNIES")) return "ONU";

            return feed.Name;
        }

        private static string SanitizeMalformedXmlEntities(string xml)
        {
            // Remplacer les entités nommées non standards (hors amp, lt, gt, quot, apos) par leur équivalent
            var entityReplacements = new Dictionary<string, string>
            {
                { "&nbsp;", " " },
                { "&eacute;", "é" },
                { "&egrave;", "è" },
                { "&agrave;", "à" },
                { "&acirc;", "â" },
                { "&ecirc;", "ê" },
                { "&icirc;", "î" },
                { "&ocirc;", "ô" },
                { "&ucirc;", "û" },
                { "&ccedil;", "ç" },
                { "&euml;", "ë" },
                { "&iuml;", "ï" },
                { "&ouml;", "ö" },
                { "&uuml;", "ü" },
                { "&laquo;", "«" },
                { "&raquo;", "»" },
                { "&copy;", "©" },
                { "&reg;", "®" },
                { "&euro;", "€" },
                { "&rsquo;", "'" },
                { "&lsquo;", "'" },
                { "&rdquo;", "\"" },
                { "&ldquo;", "\"" },
                { "&ndash;", "-" },
                { "&mdash;", "--" },
                { "&hellip;", "..." }
            };

            foreach (var kvp in entityReplacements)
            {
                xml = xml.Replace(kvp.Key, kvp.Value);
            }

            return xml;
        }
    }
}

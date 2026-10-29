using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// Robustesse du service RSS : requêtes conditionnelles, redirections, nouvel essai sélectif, encodages,
    /// liens relatifs, fuseaux horaires, découverte automatique, mise à jour et purge du cache, migration des flux par défaut.
    /// Les adresses sont des IP publiques littérales : aucune résolution DNS ni connexion réelle.
    /// </summary>
    public class RssFeedServiceRobustnessTests : IDisposable
    {
        private const string FeedUrl = "https://93.184.215.14/rss.xml";
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public RssFeedServiceRobustnessTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var db = new AppDbContext(_options);
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext Db() => new(_options);

        private sealed class ScriptedHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
            public List<HttpRequestMessage> Requests { get; } = new();
            public ScriptedHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_respond(request, Requests.Count));
            }
        }

        private RssFeedService Service(ScriptedHandler handler) =>
            new(new HttpClient(handler), Db) { RetryBaseDelay = TimeSpan.Zero };

        private static HttpResponseMessage Xml(string body, string? etag = null)
        {
            var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/rss+xml") };
            if (etag != null) r.Headers.ETag = new EntityTagHeaderValue(etag);
            return r;
        }

        private static string Rss(string guid, string title, string description = "Texte", string? pubDate = null) => $@"<rss version=""2.0""><channel><title>Bailleur test</title>
<item><title>{title}</title><link>https://93.184.215.14/appel/{guid}</link><description>{description}</description><guid>{guid}</guid>{(pubDate != null ? $"<pubDate>{pubDate}</pubDate>" : "")}</item>
</channel></rss>";

        private static RssFeedConfig Feed() => new() { Id = "f1", Name = "Bailleur test", Url = FeedUrl };

        [Fact]
        public async Task NotModified_IsReportedAsUpToDate_NotAsFailure_AndNotRetried()
        {
            var handler = new ScriptedHandler((req, n) =>
                req.Headers.IfNoneMatch.Any(t => t.Tag == "\"v1\"")
                    ? new HttpResponseMessage(HttpStatusCode.NotModified)
                    : Xml(Rss("a1", "Appel A"), "\"v1\""));
            var service = Service(handler);
            var feed = Feed();

            await service.FetchSingleFeedWithRetryAsync(feed);
            Assert.Equal("\"v1\"", feed.ETag);

            var statuses = new List<string>();
            var items = await service.FetchSingleFeedWithRetryAsync(feed, new SyncProgress(s => statuses.Add(s.Status)));

            Assert.Empty(items);
            Assert.Equal(2, handler.Requests.Count);
            Assert.Equal(FeedFetchStatus.NotModified, statuses.Last());
            Assert.StartsWith("À jour", feed.LastStatus);
            Assert.Equal(0, feed.ConsecutiveFailures);
        }

        [Fact]
        public async Task Redirect_ToPrivateAddress_IsRefused()
        {
            var handler = new ScriptedHandler((req, n) =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.Found);
                r.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data/");
                return r;
            });
            var feed = Feed();

            await Service(handler).FetchSingleFeedWithRetryAsync(feed);

            Assert.Single(handler.Requests);
            Assert.Equal(1, feed.ConsecutiveFailures);
            Assert.Contains("Redirection refusée", feed.LastError);
        }

        [Fact]
        public async Task RedirectLoop_StopsAfterLimit()
        {
            var handler = new ScriptedHandler((req, n) =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.PermanentRedirect);
                r.Headers.Location = new Uri($"https://93.184.215.14/boucle/{n}");
                return r;
            });
            var feed = Feed();

            await Service(handler).FetchSingleFeedWithRetryAsync(feed);

            Assert.Equal(RssFeedService.MaxRedirects + 1, handler.Requests.Count);
            Assert.Contains("Trop de redirections", feed.LastError);
        }

        [Fact]
        public async Task PermanentErrors_AreNotRetried_TransientErrorsAre()
        {
            var notFound = new ScriptedHandler((req, n) => new HttpResponseMessage(HttpStatusCode.NotFound));
            var feed = Feed();
            await Service(notFound).FetchSingleFeedWithRetryAsync(feed);
            Assert.Single(notFound.Requests);
            Assert.Contains("404", feed.LastError);
            Assert.Contains("introuvable", feed.LastError);

            var flaky = new ScriptedHandler((req, n) => n == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Xml(Rss("b1", "Appel B")));
            var feed2 = Feed();
            var items = await Service(flaky).FetchSingleFeedWithRetryAsync(feed2);
            Assert.Equal(2, flaky.Requests.Count);
            Assert.Single(items);
            Assert.Equal(0, feed2.ConsecutiveFailures);
        }

        [Fact]
        public async Task OversizedFeed_IsRejected()
        {
            string huge = "<rss><channel><item><title>x</title><description>" + new string('a', (int)RssFeedService.MaxResponseSizeBytes) + "</description></item></channel></rss>";
            var handler = new ScriptedHandler((req, n) => Xml(huge));
            var feed = Feed();

            await Service(handler).FetchSingleFeedWithRetryAsync(feed);

            Assert.Single(handler.Requests);
            Assert.Contains("5", feed.LastError);
        }

        [Fact]
        public async Task Feed_IsSuspended_AfterRepeatedFailures()
        {
            var handler = new ScriptedHandler((req, n) => Xml(Rss("c1", "C")));
            var feed = Feed();
            feed.ConsecutiveFailures = RssFeedService.SuspendAfterFailures;
            feed.LastFetchedAt = DateTime.UtcNow.AddHours(-1);
            feed.LastError = "HTTP 404";

            var statuses = new List<string>();
            await Service(handler).FetchSingleFeedWithRetryAsync(feed, new SyncProgress(s => statuses.Add(s.Status)));

            Assert.Empty(handler.Requests);
            Assert.Equal(FeedFetchStatus.Suspended, statuses.Last());
        }

        [Fact]
        public async Task HtmlPage_InsteadOfFeed_ReportsDiscoveredFeed()
        {
            const string html = @"<!DOCTYPE html><html><head><link rel=""alternate"" type=""application/rss+xml"" title=""Actualités"" href=""/fr/flux.xml""></head><body>Bienvenue</body></html>";
            var handler = new ScriptedHandler((req, n) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") });
            var feed = Feed();

            await Service(handler).FetchSingleFeedWithRetryAsync(feed);

            Assert.Contains("https://93.184.215.14/fr/flux.xml", feed.LastError);
        }

        [Fact]
        public async Task TestFeed_DiscoversFeedFromWebPage()
        {
            const string html = @"<html><head><link href='/actualites/rss' rel='alternate' type='application/rss+xml'></head></html>";
            var handler = new ScriptedHandler((req, n) => req.RequestUri!.AbsolutePath == "/actualites/rss"
                ? Xml(Rss("d1", "Appel D").Replace("Bailleur test", "Fonds Méditerranée"))
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") });

            var result = await Service(handler).TestFeedAsync("https://93.184.215.14/");

            Assert.True(result.Success, result.Message);
            Assert.True(result.UrlChanged);
            Assert.Equal("https://93.184.215.14/actualites/rss", result.ResolvedUrl);
            Assert.Equal("Fonds Méditerranée", result.FeedTitle);
            Assert.Equal(1, result.ItemCount);
        }

        [Fact]
        public async Task ChangedItem_IsUpdated_AndMarkedForRescoring_WithoutDuplicate()
        {
            string description = "Date limite : 15 mars 2027";
            var handler = new ScriptedHandler((req, n) => Xml(Rss("e1", "Appel E", description)));
            var service = Service(handler);
            var feed = Feed();

            await service.FetchSingleFeedWithRetryAsync(feed);
            using (var db = Db())
            {
                var item = db.RssItemsCache.Single();
                item.IsScored = true;
                item.IsRead = true;
                db.SaveChanges();
            }

            description = "Date limite prolongée : 30 avril 2027";
            var statuses = new List<FeedFetchStatus>();
            await service.FetchSingleFeedWithRetryAsync(feed, new SyncProgress(s => statuses.Add(s)));

            using var check = Db();
            var updated = check.RssItemsCache.Single();
            Assert.Contains("30 avril 2027", updated.CleanDescription);
            Assert.False(updated.IsScored);
            Assert.True(updated.IsRead);
            Assert.Equal(1, statuses.Last().UpdatedItemsCount);
        }

        [Fact]
        public async Task OldItems_ArePurged_ExceptPipeline_AndNotReAdded()
        {
            using (var db = Db())
            {
                var old = DateTime.UtcNow.AddDays(-RssFeedService.RetentionDays - 30);
                db.RssItemsCache.AddRange(
                    new RssItemCache { FeedId = "f1", FeedName = "x", Donor = "x", ExternalId = "old", Title = "Ancien", Link = "https://93.184.215.14/old", PublishDate = old },
                    new RssItemCache { FeedId = "f1", FeedName = "x", Donor = "x", ExternalId = "kept", Title = "Suivi", Link = "https://93.184.215.14/kept", PublishDate = old, IsInPipeline = true },
                    new RssItemCache { FeedId = "f1", FeedName = "x", Donor = "x", ExternalId = "recent", Title = "Récent", Link = "https://93.184.215.14/recent", PublishDate = DateTime.UtcNow });
                db.SaveChanges();
            }

            var handler = new ScriptedHandler((req, n) => Xml(Rss("old2", "Très ancien", pubDate: "Mon, 01 Jan 2024 10:00:00 GMT")));
            var service = Service(handler);
            Assert.Equal(1, await service.PurgeOldItemsAsync());
            await service.FetchSingleFeedWithRetryAsync(Feed());

            using var check = Db();
            Assert.Equal(new[] { "kept", "recent" }, check.RssItemsCache.Select(i => i.ExternalId).OrderBy(x => x).ToArray());
        }

        [Fact]
        public void Parser_HonoursDeclaredOrHeaderEncoding()
        {
            var latin1 = Encoding.Latin1;
            byte[] declared = latin1.GetBytes("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><rss><channel><item><title>Écotourisme à Ifrane</title><guid>x</guid></item></channel></rss>");
            var parsed = RssParser.ParseDocument(declared, Feed());
            Assert.Equal("Écotourisme à Ifrane", parsed.Items.Single().Title);

            byte[] headerOnly = latin1.GetBytes("<rss><channel><item><title>Forêt de cèdres</title><guid>y</guid></item></channel></rss>");
            Assert.Equal("Forêt de cèdres", RssParser.ParseDocument(headerOnly, Feed(), "iso-8859-1").Items.Single().Title);
        }

        [Fact]
        public void Parser_ResolvesRelativeLinks_AndPrefersAlternateAtomLink()
        {
            const string atom = @"<feed xmlns=""http://www.w3.org/2005/Atom"" xml:base=""https://93.184.215.14/fr/"">
  <title>Appels Atom</title>
  <entry><title>Appel F</title><id>f-1</id>
    <link rel=""self"" href=""https://93.184.215.14/api/entries/f-1""/>
    <link rel=""alternate"" type=""text/html"" href=""appels/f-1""/>
    <updated>2026-07-20T14:30:00Z</updated></entry>
</feed>";
            var atomItem = RssParser.ParseDocument(atom, Feed()).Items.Single();
            Assert.Equal("https://93.184.215.14/fr/appels/f-1", atomItem.Link);

            const string rss = @"<rss><channel>
  <item><title>Sans lien</title><guid>https://93.184.215.14/appel/g-1</guid></item>
  <item><title>Lien relatif</title><link>/appel/g-2</link><guid isPermaLink=""false"">g-2</guid></item>
</channel></rss>";
            var items = RssParser.ParseDocument(rss, Feed()).Items;
            Assert.Equal("https://93.184.215.14/appel/g-1", items[0].Link);
            Assert.Equal("https://93.184.215.14/appel/g-2", items[1].Link);
        }

        [Theory]
        [InlineData("Wed, 10 Jun 2026 08:00:00 EDT", 12)]
        [InlineData("Wed, 10 Jun 2026 08:00:00 CEST", 6)]
        [InlineData("Wed, 10 Jun 2026 08:00:00 GMT", 8)]
        [InlineData("Wed, 10 Jun 2026 08:00 +0100", 7)]
        public void Parser_UnderstandsTimeZoneAbbreviations(string input, int expectedUtcHour)
        {
            var date = RssParser.ParseDate(input);
            Assert.NotNull(date);
            Assert.Equal(new DateTime(2026, 6, 10, expectedUtcHour, 0, 0), date!.Value);
        }

        [Fact]
        public void Parser_CapsVeryLongFields()
        {
            string longGuid = new string('g', 400);
            string rss = $"<rss><channel><item><title>{new string('t', 900)}</title><description>{new string('d', 20000)}</description><guid>{longGuid}</guid></item></channel></rss>";
            var item = RssParser.ParseDocument(rss, Feed()).Items.Single();
            Assert.True(item.Title.Length <= RssParser.MaxTitleLength);
            Assert.True(item.CleanDescription.Length <= RssParser.MaxDescriptionLength);
            Assert.True(item.ExternalId.Length <= RssParser.MaxExternalIdLength);
        }

        [Theory]
        [InlineData("::ffff:10.0.0.1")]
        [InlineData("::ffff:127.0.0.1")]
        [InlineData("100.64.1.1")]
        [InlineData("64:ff9b::7f00:1")]
        [InlineData("198.18.0.5")]
        public void Ssrf_BlocksEmbeddedAndSharedRanges(string address)
        {
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse(address)));
        }

        [Fact]
        public void Ssrf_AllowsPublicAddressEvenWhenMappedToIpv6()
        {
            Assert.False(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("::ffff:8.8.8.8")));
        }

        [Fact]
        public void DefaultFeeds_ObsoleteUrlsAreMigrated_UserChangesKept_NewSourcesAdded()
        {
            var feeds = new List<RssFeedConfig>
            {
                new() { Id = "feed-gef-sgp", Url = "https://www.thegef.org/newsroom/all/rss.xml", IsDefault = true, Enabled = true, ConsecutiveFailures = 4 },
                new() { Id = "feed-fao-forestry", Url = "https://exemple.org/flux-choisi-par-l-utilisateur", IsDefault = true, Enabled = true },
                new() { Id = "feed-cepf-med", Url = "https://www.cepf.net/rss.xml", IsDefault = true, Enabled = true },
                new() { Id = "perso", Url = "https://exemple.org/perso", IsDefault = false }
            };

            Assert.True(ProjectRadarConfigService.MigrateDefaultFeeds(feeds));

            var gef = feeds.Single(f => f.Id == "feed-gef-sgp");
            Assert.Equal("https://www.thegef.org/rss.xml", gef.Url);
            Assert.Equal(0, gef.ConsecutiveFailures);
            Assert.Equal("https://exemple.org/flux-choisi-par-l-utilisateur", feeds.Single(f => f.Id == "feed-fao-forestry").Url);
            Assert.False(feeds.Single(f => f.Id == "feed-cepf-med").Enabled);
            Assert.Contains(feeds, f => f.Id == "feed-adaptation-fund");
            Assert.Contains(feeds, f => f.Id == "perso");
            Assert.False(ProjectRadarConfigService.MigrateDefaultFeeds(feeds));
        }

        /// <summary>IProgress synchrone (Progress&lt;T&gt; publie de manière asynchrone).</summary>
        private sealed class SyncProgress : IProgress<FeedFetchStatus>
        {
            private readonly Action<FeedFetchStatus> _onReport;
            public SyncProgress(Action<FeedFetchStatus> onReport) => _onReport = onReport;
            public void Report(FeedFetchStatus value) => _onReport(value);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_Projet.Tests
{
    public class Stage3RssServiceTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;

        public Stage3RssServiceTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"afmi_s3_test_{Guid.NewGuid():N}.db");
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var db = new AppDbContext(_dbOptions);
            db.Database.EnsureCreated();
            db.SeedDefaultData();
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            }
            catch { }
        }

        private AppDbContext CreateDbContext() => new AppDbContext(_dbOptions);

        #region 1. SSRF Protection Tests

        [Theory]
        [InlineData("http://localhost/feed.xml")]
        [InlineData("http://127.0.0.1:80/rss")]
        [InlineData("http://10.0.0.5/api/rss")]
        [InlineData("http://192.168.1.100/data.xml")]
        [InlineData("http://172.16.50.1/feed")]
        [InlineData("http://169.254.169.254/latest/meta-data/")]
        [InlineData("ftp://ftp.example.org/feed.xml")]
        [InlineData("file:///C:/Windows/win.ini")]
        [InlineData("http://example.com:22/feed.xml")]
        [InlineData("http://example.com:3306/feed.xml")]
        public void SsrfValidator_RejectsForbiddenAndPrivateUrls(string maliciousUrl)
        {
            var (isValid, reason) = SsrfValidator.ValidateUrl(maliciousUrl);
            Assert.False(isValid);
            Assert.NotEmpty(reason);
        }

        [Fact]
        public void SsrfValidator_IpCheck_IdentifiesAllPrivateRanges()
        {
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("127.0.0.1")));
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("10.200.1.1")));
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("192.168.0.1")));
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("172.25.1.1")));
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("169.254.1.1")));
            Assert.True(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.IPv6Loopback));

            Assert.False(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("8.8.8.8")));
            Assert.False(SsrfValidator.IsPrivateOrRestrictedIp(IPAddress.Parse("1.1.1.1")));
        }

        #endregion

        #region 2. RSS 2.0 & Atom Parsing Tests

        [Fact]
        public void RssParser_ParsesRss20WithEncodingAndHtmlCleaning()
        {
            string rssSample = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"">
  <channel>
    <title>Appels à Projets - UICN Méditerranée</title>
    <link>https://www.iucn.org</link>
    <description>Flux officiel</description>
    <item>
      <title>Subvention Restauration Cédraies &amp; Zones Humides</title>
      <link>https://www.iucn.org/grants/cedre-2026?utm_source=rss&amp;utm_medium=feed</link>
      <description><![CDATA[<p>Ce programme finance la <b>conservation active</b> des forêts.<script>alert('xss')</script></p>]]></description>
      <pubDate>Mon, 15 Jun 2026 10:00:00 GMT</pubDate>
      <guid>iucn-item-001</guid>
    </item>
  </channel>
</rss>";

            var feedConfig = new RssFeedConfig
            {
                Id = "iucn-test",
                Name = "UICN Méditerranée",
                Url = "https://www.iucn.org/rss.xml"
            };

            var items = RssParser.ParseFeed(rssSample, feedConfig);

            Assert.Single(items);
            var item = items[0];

            Assert.Equal("Subvention Restauration Cédraies & Zones Humides", item.Title);
            Assert.Equal("iucn-item-001", item.ExternalId);
            Assert.Equal("UICN", item.Donor);
            // Vérification du nettoyage HTML (script et balises supprimés)
            Assert.DoesNotContain("<script>", item.CleanDescription);
            Assert.DoesNotContain("<p>", item.CleanDescription);
            Assert.Contains("Ce programme finance la conservation active des forêts.", item.CleanDescription);
            // Vérification de la normalisation de l'URL (paramètres UTM supprimés)
            Assert.Equal("https://www.iucn.org/grants/cedre-2026", item.Link);
            Assert.NotNull(item.PublishDate);
            Assert.Equal(2026, item.PublishDate!.Value.Year);
            Assert.Equal(6, item.PublishDate.Value.Month);
        }

        [Fact]
        public void RssParser_ParsesAtomFeedCorrectly()
        {
            string atomSample = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed xmlns=""http://www.w3.org/2005/Atom"">
  <title>PNUD Maroc Appels</title>
  <entry>
    <title>Appel à Propositions : Autonomisation des Coopératives Rurales</title>
    <id>urn:uuid:1225c695-cfb8-4ebb-aaaa-80da344efa6a</id>
    <link href=""https://www.undp.org/maroc/appels/coop-2026"" />
    <updated>2026-07-20T14:30:00Z</updated>
    <summary>Le PNUD lance un nouvel appel à destination des associations locales d'Ifrane.</summary>
  </entry>
</feed>";

            var feedConfig = new RssFeedConfig
            {
                Id = "undp-test",
                Name = "PNUD Maroc",
                Url = "https://www.undp.org/maroc/rss.xml"
            };

            var items = RssParser.ParseFeed(atomSample, feedConfig);

            Assert.Single(items);
            var item = items[0];

            Assert.Contains("Autonomisation des Coopératives", item.Title);
            Assert.Equal("urn:uuid:1225c695-cfb8-4ebb-aaaa-80da344efa6a", item.ExternalId);
            Assert.Equal("https://www.undp.org/maroc/appels/coop-2026", item.Link);
            Assert.Contains("associations locales d'Ifrane", item.CleanDescription);
            Assert.Equal(2026, item.PublishDate?.Year);
        }

        #endregion

        #region 3. URL Normalization and HTML Cleaning

        [Theory]
        [InlineData("https://example.com/item/?utm_source=twitter&utm_medium=social", "https://example.com/item")]
        [InlineData("http://donor.org/call.html?fbclid=IwAR234&id=42", "http://donor.org/call.html?id=42")]
        public void RssParser_NormalizeUrl_StripsTrackingTokens(string input, string expected)
        {
            string normalized = RssParser.NormalizeUrl(input);
            Assert.Equal(expected, normalized);
        }

        [Fact]
        public void RssParser_CleanHtml_DecodesEntitiesAndStripsTags()
        {
            string raw = "<div>Projet d&rsquo;&eacute;cotourisme &amp; biodiversit&eacute;</div><style>body{color:red;}</style>";
            string cleaned = RssParser.CleanHtml(raw);

            Assert.Equal("Projet d'écotourisme & biodiversité", cleaned);
        }

        #endregion

        #region 4. Mocked Service & Deduplication Test

        [Fact]
        public async Task RssFeedService_DeduplicatesItems_AcrossMultipleFetches()
        {
            string sampleXml = @"<rss version=""2.0"">
  <channel>
    <item>
      <title>Appel Répété Unique</title>
      <link>https://cepf.net/grant-1</link>
      <description>Description unique</description>
      <guid>unique-guid-999</guid>
    </item>
  </channel>
</rss>";

            var mockHandler = new MockHttpMessageHandler(sampleXml);
            var mockClient = new HttpClient(mockHandler);

            var service = new RssFeedService(mockClient, CreateDbContext);
            var feedConfig = new RssFeedConfig
            {
                Id = "feed-mock",
                Name = "CEPF Méditerranée",
                Url = "https://www.cepf.net/rss.xml"
            };

            // First fetch
            var items1 = await service.FetchSingleFeedWithRetryAsync(feedConfig);
            Assert.Single(items1);

            // Second fetch with identical XML
            var items2 = await service.FetchSingleFeedWithRetryAsync(feedConfig);
            Assert.Single(items2);

            // Database cache should contain exactly 1 item
            using var db = CreateDbContext();
            int count = await db.RssItemsCache.CountAsync(i => i.ExternalId == "unique-guid-999");
            Assert.Equal(1, count);
        }

        #endregion
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;
        public MockHttpMessageHandler(string responseContent) => _responseContent = responseContent;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/rss+xml")
            };
            return Task.FromResult(response);
        }
    }
}

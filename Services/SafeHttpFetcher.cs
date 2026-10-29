using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.Services
{
    /// <summary>Erreur de récupération d'une ressource web, avec indication du caractère temporaire.</summary>
    public class SafeFetchException : Exception
    {
        public bool IsTransient { get; }
        public SafeFetchException(string message, bool isTransient, Exception? inner = null) : base(message, inner) => IsTransient = isTransient;
    }

    public sealed class SafeFetchResponse
    {
        public bool NotModified { get; init; }
        public byte[] Body { get; init; } = Array.Empty<byte>();
        public string? Charset { get; init; }
        public string? MediaType { get; init; }
        public string FinalUrl { get; init; } = string.Empty;
        public string? ETag { get; init; }
        public DateTimeOffset? LastModified { get; init; }
    }

    /// <summary>
    /// Accès web sécurisé commun (flux RSS, analyse de liens, recherche de projets similaires) :
    /// protection SSRF à la connexion et à chaque redirection, redirections limitées, taille limitée,
    /// requêtes conditionnelles, nouvel essai sur erreur temporaire uniquement (ADR 24, ADR 25).
    /// </summary>
    public class SafeHttpFetcher
    {
        public const long DefaultMaxBytes = 5 * 1024 * 1024;
        public const int MaxRedirects = 5;

        private static readonly Lazy<HttpClient> SharedClient = new(CreateClient);
        private readonly HttpClient _httpClient;

        public long MaxBytes { get; init; } = DefaultMaxBytes;
        public int MaxAttempts { get; init; } = 3;
        public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        public SafeHttpFetcher(HttpClient? httpClient = null) => _httpClient = httpClient ?? SharedClient.Value;

        /// <summary>Client HTTP partagé : vérifie l'adresse IP effectivement contactée (protection « DNS rebinding »).</summary>
        public static HttpClient DefaultClient => SharedClient.Value;

        private static HttpClient CreateClient()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false, // redirections suivies et revalidées manuellement
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                ConnectTimeout = TimeSpan.FromSeconds(15),
                ConnectCallback = ConnectToPublicAddressAsync
            };

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AFMI-Project-Radar/2.1; +https://afmi.ma)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.9, text/html;q=0.8, application/json;q=0.8, */*;q=0.5");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("fr, en;q=0.8, ar;q=0.6");
            return client;
        }

        private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
            if (addresses.Length == 0)
                throw new HttpRequestException($"Hôte introuvable : {context.DnsEndPoint.Host}");
            if (addresses.Any(SsrfValidator.IsPrivateOrRestrictedIp))
                throw new HttpRequestException($"Connexion refusée : {context.DnsEndPoint.Host} pointe vers une adresse privée ou réservée (protection SSRF).");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        public static bool IsTransient(Exception ex) => ex switch
        {
            SafeFetchException f => f.IsTransient,
            HttpRequestException h => h.StatusCode == null || (int)h.StatusCode >= 500,
            TaskCanceledException => true, // délai HTTP dépassé
            IOException => true,
            _ => false
        };

        /// <summary>Récupère une ressource avec nouvel essai (délai progressif) sur erreur temporaire.</summary>
        public async Task<SafeFetchResponse> FetchWithRetryAsync(string url, string? etag = null, DateTimeOffset? lastModified = null, CancellationToken cancellationToken = default,
            System.Collections.Generic.IDictionary<string, string>? headers = null)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await FetchAsync(url, etag, lastModified, cancellationToken, headers);
                }
                catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex) && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(RetryBaseDelay.TotalMilliseconds * Math.Pow(3, attempt - 1)), cancellationToken);
                }
            }
        }

        /// <summary>Une tentative : redirections suivies manuellement (chaque saut revalidé), corps limité à <see cref="MaxBytes"/>.</summary>
        public async Task<SafeFetchResponse> FetchAsync(string url, string? etag = null, DateTimeOffset? lastModified = null, CancellationToken cancellationToken = default,
            System.Collections.Generic.IDictionary<string, string>? headers = null)
        {
            var (response, finalUrl) = await SendFollowingRedirectsAsync(url, etag, lastModified, headers, cancellationToken);
            using (response)
            {
                if (response.StatusCode == HttpStatusCode.NotModified)
                    return new SafeFetchResponse { NotModified = true, FinalUrl = finalUrl, ETag = etag, LastModified = lastModified };

                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength > MaxBytes)
                    throw new SafeFetchException($"Réponse trop volumineuse ({contentLength.Value / (1024 * 1024)} Mo). Limite autorisée : {MaxBytes / (1024 * 1024)} Mo.", isTransient: false);

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                byte[] body = await ReadLimitedAsync(stream, MaxBytes, cancellationToken);

                return new SafeFetchResponse
                {
                    Body = body,
                    Charset = response.Content.Headers.ContentType?.CharSet,
                    MediaType = response.Content.Headers.ContentType?.MediaType,
                    FinalUrl = finalUrl,
                    ETag = response.Headers.ETag?.ToString(),
                    LastModified = response.Content.Headers.LastModified
                };
            }
        }

        /// <summary>
        /// Télécharge un fichier volumineux (installateur…) directement sur disque, avec les mêmes contrôles
        /// (SSRF à chaque redirection, taille maximale) et la progression (0-1). Renvoie le nombre d'octets écrits.
        /// </summary>
        public async Task<long> DownloadToFileAsync(string url, string destinationPath, long maxBytes, IProgress<double>? progress = null,
            System.Collections.Generic.IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
        {
            var (response, _) = await SendFollowingRedirectsAsync(url, null, null, headers, cancellationToken);
            using (response)
            {
                long? total = response.Content.Headers.ContentLength;
                if (total > maxBytes)
                    throw new SafeFetchException($"Fichier trop volumineux ({total.Value / (1024 * 1024)} Mo, limite {maxBytes / (1024 * 1024)} Mo).", isTransient: false);

                string tempPath = destinationPath + ".part";
                long written = 0;
                try
                {
                    await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
                    await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                        {
                            written += read;
                            if (written > maxBytes) throw new SafeFetchException($"Fichier trop volumineux (plus de {maxBytes / (1024 * 1024)} Mo).", isTransient: false);
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                            if (total > 0) progress?.Report(written / (double)total.Value);
                        }
                    }
                    File.Move(tempPath, destinationPath, overwrite: true);
                    progress?.Report(1);
                    return written;
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }
        }

        public static bool SameHost(string originalUrl, string currentUrl) =>
            Uri.TryCreate(originalUrl, UriKind.Absolute, out var a) && Uri.TryCreate(currentUrl, UriKind.Absolute, out var b) &&
            a.Scheme == b.Scheme && a.Port == b.Port && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Envoie la requête en suivant manuellement les redirections (chaque saut revalidé contre le SSRF).
        /// Renvoie une réponse réussie ou 304 (à libérer par l'appelant) ; lève une exception sinon.
        /// </summary>
        private async Task<(HttpResponseMessage Response, string FinalUrl)> SendFollowingRedirectsAsync(string url, string? etag, DateTimeOffset? lastModified,
            System.Collections.Generic.IDictionary<string, string>? headers, CancellationToken cancellationToken)
        {
            string current = url;
            for (int hop = 0; hop <= MaxRedirects; hop++)
            {
                var (isValid, reason) = SsrfValidator.ValidateUrl(current);
                if (!isValid)
                {
                    bool dnsIssue = reason.StartsWith("Erreur de résolution DNS", StringComparison.Ordinal);
                    throw new SafeFetchException(hop == 0 ? $"Adresse refusée : {reason}" : $"Redirection refusée ({current}) : {reason}", isTransient: dnsIssue);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                if (!string.IsNullOrEmpty(etag) && EntityTagHeaderValue.TryParse(etag, out var tag)) request.Headers.IfNoneMatch.Add(tag);
                if (lastModified.HasValue) request.Headers.IfModifiedSince = lastModified;
                // En-têtes (dont l'authentification) envoyés uniquement à l'hôte d'origine, jamais après une redirection vers un autre hôte
                if (headers != null && SameHost(url, current))
                    foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotModified) return (response, current);

                if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location;
                    response.Dispose();
                    if (location == null) throw new SafeFetchException($"Redirection HTTP {(int)response.StatusCode} sans destination.", isTransient: false);
                    current = (location.IsAbsoluteUri ? location : new Uri(new Uri(current), location)).AbsoluteUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    string reasonPhrase = response.ReasonPhrase ?? string.Empty;
                    response.Dispose();
                    string hint = code switch
                    {
                        404 => " (adresse introuvable : la ressource a probablement été déplacée ou supprimée)",
                        403 => " (accès refusé par le site, qui bloque peut-être les lecteurs automatiques)",
                        429 => " (trop de requêtes, réessayer plus tard)",
                        _ => string.Empty
                    };
                    throw new SafeFetchException($"HTTP {code} {reasonPhrase}{hint}", isTransient: code >= 500 || code is 408 or 429);
                }

                return (response, current);
            }

            throw new SafeFetchException($"Trop de redirections (plus de {MaxRedirects}).", isTransient: false);
        }

        private static async Task<byte[]> ReadLimitedAsync(Stream stream, long maxBytes, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                    throw new SafeFetchException($"Contenu trop volumineux (dépassement de la limite de sécurité de {maxBytes / (1024 * 1024)} Mo).", isTransient: false);
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }
    }
}

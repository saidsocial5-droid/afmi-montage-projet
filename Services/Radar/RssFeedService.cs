using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    public partial class FeedFetchStatus : ObservableObject
    {
        public const string Running = "En cours";
        public const string Succeeded = "Succès";
        public const string NotModified = "À jour";
        public const string Failed = "Échec";
        public const string Suspended = "Suspendu";

        public string FeedId { get; set; } = string.Empty;
        public string FeedName { get; set; } = string.Empty;

        [ObservableProperty]
        private string _status = string.Empty; // "En cours", "Succès", "À jour", "Échec", "Suspendu"

        [ObservableProperty]
        private int _newItemsCount;

        [ObservableProperty]
        private int _updatedItemsCount;

        [ObservableProperty]
        private string? _errorMessage;

        public bool IsDone => Status != Running && !string.IsNullOrEmpty(Status);

        partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(IsDone));
    }

    /// <summary>Bilan d'une actualisation de tous les flux.</summary>
    public class FeedRefreshSummary
    {
        public int Succeeded { get; set; }
        public int NotModified { get; set; }
        public int Failed { get; set; }
        public int Suspended { get; set; }
        public int NewItems { get; set; }
        public int UpdatedItems { get; set; }
        public int PurgedItems { get; set; }

        public string ToMessage() =>
            $"{NewItems} nouvelle(s) opportunité(s), {UpdatedItems} mise(s) à jour — flux : {Succeeded} actualisé(s), {NotModified} inchangé(s)" +
            (Failed > 0 ? $", {Failed} en échec" : "") +
            (Suspended > 0 ? $", {Suspended} suspendu(s) après des échecs répétés" : "") + ".";
    }

    /// <summary>Résultat du test d'une adresse de flux (avec découverte automatique dans une page web).</summary>
    public class FeedTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public string? FeedTitle { get; set; }

        /// <summary>Adresse du flux à enregistrer (différente de l'adresse saisie si un flux a été découvert dans la page).</summary>
        public string ResolvedUrl { get; set; } = string.Empty;
        public bool UrlChanged { get; set; }
    }

    /// <summary>Erreur propre au contenu d'un flux (page web au lieu d'un flux, contenu illisible…).</summary>
    public class FeedFetchException : SafeFetchException
    {
        public FeedFetchException(string message, bool isTransient, Exception? inner = null) : base(message, isTransient, inner) { }
    }

    /// <summary>
    /// خدمة جلب ومعالجة خلاصات RSS/Atom المؤسسية بالتوازي
    /// مع التخزين المؤقت، حماية SSRF، وإلغاء العمليات.
    /// - Protection SSRF à la connexion (adresse IP réellement contactée), y compris après redirection ;
    /// - requêtes conditionnelles (ETag / Last-Modified) mémorisées dans rss_feeds.json ;
    /// - nouvel essai uniquement sur erreur temporaire ; suspension 24 h après 5 échecs consécutifs ;
    /// - mise à jour des opportunités modifiées par la source et purge des éléments anciens.
    /// </summary>
    public class RssFeedService
    {
        private readonly SafeHttpFetcher _fetcher;
        private readonly Func<AppDbContext> _dbContextFactory;

        // Limite maximale de taille de réponse (5 Mo) pour prévenir les attaques DoS
        public const long MaxResponseSizeBytes = SafeHttpFetcher.DefaultMaxBytes;
        public const int MaxRedirects = SafeHttpFetcher.MaxRedirects;
        public const int MaxAttempts = 3;
        public const int SuspendAfterFailures = 5;
        public static readonly TimeSpan SuspensionDuration = TimeSpan.FromHours(24);
        public static readonly TimeSpan PerFeedTimeout = TimeSpan.FromSeconds(45);

        /// <summary>Opportunités publiées il y a plus d'un an : purgées du cache (sauf si suivies dans le pipeline).</summary>
        public const int RetentionDays = 365;

        /// <summary>Délai entre deux essais (modifiable dans les tests).</summary>
        public TimeSpan RetryBaseDelay
        {
            get => _fetcher.RetryBaseDelay;
            set => _fetcher.RetryBaseDelay = value;
        }

        /// <summary>Bilan de la dernière actualisation complète.</summary>
        public FeedRefreshSummary? LastSummary { get; private set; }

        public RssFeedService(HttpClient? customClient = null, Func<AppDbContext>? dbContextFactory = null)
        {
            _fetcher = new SafeHttpFetcher(customClient) { MaxAttempts = MaxAttempts };
            _dbContextFactory = dbContextFactory ?? (() => new AppDbContext());
        }

        /// <summary>
        /// استعلام جميع الخلاصات المفعّلة بالتوازي مع سقف توازٍ محدد وتقرير التقدم لكل خلاصة.
        /// </summary>
        public async Task<List<RssItemCache>> RefreshAllFeedsAsync(
            int maxConcurrency = 4,
            IProgress<FeedFetchStatus>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var feeds = ProjectRadarConfigService.LoadFeeds().Where(f => f.Enabled).ToList();
            if (feeds.Count == 0) return await GetCachedItemsAsync();

            var summary = new FeedRefreshSummary();
            var summaryLock = new object();
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var tasks = feeds.Select(feed => Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var status = await FetchFeedAsync(feed, progress, cancellationToken);
                    lock (summaryLock)
                    {
                        switch (status.Status)
                        {
                            case FeedFetchStatus.Succeeded: summary.Succeeded++; break;
                            case FeedFetchStatus.NotModified: summary.NotModified++; break;
                            case FeedFetchStatus.Suspended: summary.Suspended++; break;
                            default: summary.Failed++; break;
                        }
                        summary.NewItems += status.NewItemsCount;
                        summary.UpdatedItems += status.UpdatedItemsCount;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken)).ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Annulation demandée par l'utilisateur : l'état des flux déjà traités est tout de même enregistré
            }

            // Fusion avec la configuration actuelle : un flux ajouté ou modifié pendant l'actualisation n'est pas écrasé
            ProjectRadarConfigService.UpdateFeedStatuses(feeds);
            cancellationToken.ThrowIfCancellationRequested();

            summary.PurgedItems = await PurgeOldItemsAsync();
            LastSummary = summary;
            AppLogger.Info(nameof(RssFeedService), summary.ToMessage());

            // Retourner l'ensemble des éléments mis en cache depuis la base
            return await GetCachedItemsAsync();
        }

        /// <summary>
        /// جلب خلاصة فردية مع حماية SSRF والمحاولة بتأخير تصاعدي عند الفشل.
        /// Renvoie les éléments lus dans le flux (vide si inchangé, suspendu ou en échec).
        /// </summary>
        public async Task<List<RssItemCache>> FetchSingleFeedWithRetryAsync(
            RssFeedConfig feed,
            IProgress<FeedFetchStatus>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var items = new List<RssItemCache>();
            await FetchFeedAsync(feed, progress, cancellationToken, items);
            return items;
        }

        private async Task<FeedFetchStatus> FetchFeedAsync(
            RssFeedConfig feed,
            IProgress<FeedFetchStatus>? progress,
            CancellationToken cancellationToken,
            List<RssItemCache>? itemsOut = null)
        {
            var status = new FeedFetchStatus { FeedId = feed.Id, FeedName = feed.Name, Status = FeedFetchStatus.Running };
            progress?.Report(status);

            // Flux en échec répété : suspendu 24 h pour ne pas ralentir chaque actualisation
            if (feed.ConsecutiveFailures >= SuspendAfterFailures && feed.LastFetchedAt.HasValue &&
                DateTime.UtcNow - feed.LastFetchedAt.Value < SuspensionDuration)
            {
                status.Status = FeedFetchStatus.Suspended;
                status.ErrorMessage = $"Suspendu après {feed.ConsecutiveFailures} échecs consécutifs (dernière erreur : {feed.LastError}). Nouvel essai automatique après 24 h, ou vérifiez l'adresse.";
                progress?.Report(status);
                return status;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PerFeedTimeout);

            try
            {
                // Requête conditionnelle seulement si le flux a déjà été lu avec succès (sinon le cache serait vide)
                bool conditional = feed.LastSuccessAt.HasValue;
                var response = await _fetcher.FetchWithRetryAsync(feed.Url, conditional ? feed.ETag : null, conditional ? feed.LastModified : null, timeout.Token);

                feed.LastFetchedAt = DateTime.UtcNow;
                if (response.NotModified)
                {
                    MarkSuccess(feed, "À jour (aucun changement depuis la dernière lecture)");
                    status.Status = FeedFetchStatus.NotModified;
                    progress?.Report(status);
                    return status;
                }

                var parsed = RssParser.ParseDocument(response.Body, feed, response.Charset);
                if (!parsed.IsFeed)
                {
                    string message = parsed.IsHtml && parsed.DiscoveredFeedUrls.Count > 0
                        ? $"L'adresse renvoie une page web, pas un flux. Flux annoncé par la page : {parsed.DiscoveredFeedUrls[0]}"
                        : parsed.IsHtml ? "L'adresse renvoie une page web, pas un flux RSS/Atom." : "Contenu illisible (ni RSS ni Atom).";
                    throw new FeedFetchException(message, isTransient: false);
                }

                var (added, updated) = await SaveAndDeduplicateItemsAsync(parsed.Items, cancellationToken);
                itemsOut?.AddRange(parsed.Items);

                feed.ETag = response.ETag;
                feed.LastModified = response.LastModified;
                feed.LastItemCount = parsed.Items.Count;
                MarkSuccess(feed, parsed.Items.Count == 0
                    ? "Succès, mais le flux ne contient aucun élément"
                    : $"Succès : {parsed.Items.Count} élément(s), {added} nouveau(x)" + (updated > 0 ? $", {updated} mis à jour" : ""));

                status.Status = FeedFetchStatus.Succeeded;
                status.NewItemsCount = added;
                status.UpdatedItemsCount = updated;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                status.Status = FeedFetchStatus.Failed;
                status.ErrorMessage = "Annulé";
                progress?.Report(status);
                throw;
            }
            catch (Exception ex) when (ex is SafeFetchException or HttpRequestException or OperationCanceledException or IOException)
            {
                string error = ex is OperationCanceledException ? $"Délai dépassé ({PerFeedTimeout.TotalSeconds:0} s)" : ex.Message;
                feed.LastFetchedAt = DateTime.UtcNow;
                feed.ConsecutiveFailures++;
                feed.LastError = error;
                feed.LastStatus = $"Échec : {error}";
                status.Status = FeedFetchStatus.Failed;
                status.ErrorMessage = error;
                AppLogger.Warn(nameof(RssFeedService), $"Flux « {feed.Name} » ({feed.Url}) : {error}");
            }
            catch (Exception ex)
            {
                // Erreur inattendue (base, analyse…) : n'interrompt pas l'actualisation des autres flux
                AppLogger.Error(nameof(RssFeedService), $"Erreur inattendue sur le flux « {feed.Name} ».", ex);
                feed.LastFetchedAt = DateTime.UtcNow;
                feed.ConsecutiveFailures++;
                feed.LastError = ex.Message;
                feed.LastStatus = $"Échec : {ex.Message}";
                status.Status = FeedFetchStatus.Failed;
                status.ErrorMessage = ex.Message;
            }

            progress?.Report(status);
            return status;
        }

        private static void MarkSuccess(RssFeedConfig feed, string statusText)
        {
            feed.LastStatus = statusText;
            feed.LastSuccessAt = DateTime.UtcNow;
            feed.ConsecutiveFailures = 0;
            feed.LastError = null;
        }

        /// <summary>
        /// Ajoute les nouveaux éléments ; met à jour ceux que la source a modifiés (titre, texte, date),
        /// sans toucher à leur état (lu, masqué, pipeline). Un élément modifié doit être réévalué par l'IA.
        /// </summary>
        private async Task<(int Added, int Updated)> SaveAndDeduplicateItemsAsync(List<RssItemCache> items, CancellationToken cancellationToken)
        {
            if (items.Count == 0) return (0, 0);

            using var db = _dbContextFactory();
            var ids = items.Select(i => i.ExternalId).Distinct().ToList();
            var links = items.Select(i => i.Link).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();

            var existing = await db.RssItemsCache
                .Where(i => ids.Contains(i.ExternalId) || links.Contains(i.Link))
                .ToListAsync(cancellationToken);
            var byId = existing.GroupBy(i => i.ExternalId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var byLink = existing.Where(i => !string.IsNullOrEmpty(i.Link)).GroupBy(i => i.Link, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Liens déjà présents dans le pipeline
            var pipelineUrlSet = new HashSet<string>(
                await db.GrantsPipeline.Where(g => !g.IsDeleted && links.Contains(g.OriginalUrl)).Select(g => g.OriginalUrl).ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);

            var retentionCutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            int added = 0, updated = 0;

            foreach (var item in items)
            {
                var match = byId.GetValueOrDefault(item.ExternalId)
                            ?? (string.IsNullOrEmpty(item.Link) ? null : byLink.GetValueOrDefault(item.Link));

                if (match != null)
                {
                    if (match.Id == 0) continue; // doublon dans le même lot
                    bool textChanged = !string.Equals(match.Title, item.Title, StringComparison.Ordinal) ||
                                       !string.Equals(match.CleanDescription, item.CleanDescription, StringComparison.Ordinal);
                    bool dateChanged = item.PublishDate.HasValue && match.PublishDate != item.PublishDate;
                    if (textChanged || dateChanged)
                    {
                        match.Title = item.Title;
                        match.CleanDescription = item.CleanDescription;
                        if (item.PublishDate.HasValue) match.PublishDate = item.PublishDate;
                        if (textChanged) match.IsScored = false; // contenu modifié (date limite prolongée…) : à réévaluer
                        updated++;
                    }
                    continue;
                }

                // Élément ancien : il serait aussitôt purgé (et réapparaîtrait à chaque actualisation)
                if (item.PublishDate.HasValue && item.PublishDate.Value < retentionCutoff) continue;

                if (!string.IsNullOrEmpty(item.Link) && pipelineUrlSet.Contains(item.Link)) item.IsInPipeline = true;

                db.RssItemsCache.Add(item);
                byId[item.ExternalId] = item;
                if (!string.IsNullOrEmpty(item.Link)) byLink[item.Link] = item;
                added++;
            }

            if (added > 0 || updated > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            return (added, updated);
        }

        /// <summary>
        /// Supprime du cache les opportunités publiées il y a plus de <see cref="RetentionDays"/> jours,
        /// sauf celles suivies dans le pipeline.
        /// </summary>
        public async Task<int> PurgeOldItemsAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            using var db = _dbContextFactory();
            var old = await db.RssItemsCache
                .Where(i => !i.IsInPipeline && (i.PublishDate ?? i.FetchedAt) < cutoff)
                .ToListAsync(cancellationToken);
            if (old.Count == 0) return 0;

            db.RssItemsCache.RemoveRange(old);
            await db.SaveChangesAsync(cancellationToken);
            return old.Count;
        }

        /// <summary>
        /// استرجاع العناصر المخزنة مؤقتاً في القاعدة عند العمل بدون إنترنت.
        /// </summary>
        public async Task<List<RssItemCache>> GetCachedItemsAsync()
        {
            using var db = _dbContextFactory();
            return await db.RssItemsCache
                .Where(i => !i.IsHidden)
                .OrderByDescending(i => i.PublishDate ?? i.FetchedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Teste une adresse avant enregistrement. Si l'adresse est celle d'une page web, les flux qu'elle
        /// annonce sont essayés et l'adresse du premier flux valide est proposée.
        /// </summary>
        public async Task<FeedTestResult> TestFeedAsync(string url, CancellationToken cancellationToken = default)
        {
            url = url.Trim();
            var result = new FeedTestResult { ResolvedUrl = url };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(40));

            try
            {
                var probe = new RssFeedConfig { Id = "test", Name = "Test", Url = url };
                var response = await _fetcher.FetchAsync(url, null, null, timeout.Token);
                var parsed = RssParser.ParseDocument(response.Body, probe, response.Charset);

                if (!parsed.IsFeed && parsed.IsHtml)
                {
                    foreach (var candidate in parsed.DiscoveredFeedUrls.Take(3))
                    {
                        try
                        {
                            var candidateResponse = await _fetcher.FetchAsync(candidate, null, null, timeout.Token);
                            var candidateParsed = RssParser.ParseDocument(candidateResponse.Body, new RssFeedConfig { Id = "test", Name = "Test", Url = candidate }, candidateResponse.Charset);
                            if (!candidateParsed.IsFeed) continue;
                            parsed = candidateParsed;
                            result.ResolvedUrl = candidate;
                            result.UrlChanged = true;
                            break;
                        }
                        catch (Exception ex) when (ex is SafeFetchException or HttpRequestException or IOException)
                        {
                            AppLogger.Warn(nameof(RssFeedService), $"Flux découvert inaccessible : {candidate}", ex);
                        }
                    }
                }

                if (!parsed.IsFeed)
                {
                    result.Message = parsed.IsHtml
                        ? "Cette adresse est une page web qui n'annonce aucun flux RSS/Atom utilisable. Cherchez sur le site un lien « RSS » ou « Flux »."
                        : "Le contenu reçu n'est ni un flux RSS ni un flux Atom.";
                    return result;
                }

                result.Success = true;
                result.ItemCount = parsed.Items.Count;
                result.FeedTitle = parsed.Title;
                result.Message = (result.UrlChanged ? $"Flux détecté automatiquement dans la page : {result.ResolvedUrl}\n" : string.Empty) +
                                 (parsed.Items.Count == 0
                                     ? "Connexion réussie, mais le flux ne contient actuellement aucun élément."
                                     : $"Connexion réussie ! {parsed.Items.Count} élément(s) dans le flux.");
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result.Message = "Délai dépassé : la source ne répond pas.";
                return result;
            }
            catch (Exception ex) when (ex is SafeFetchException or HttpRequestException or IOException)
            {
                result.Message = $"Erreur de connexion : {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// اختبار الاتصال بخلاصة RSS معينة قبل الحفظ.
        /// </summary>
        public async Task<(bool Success, string Message, int ItemCount)> TestFeedConnectionAsync(string url)
        {
            var result = await TestFeedAsync(url);
            return (result.Success, result.Message, result.ItemCount);
        }
    }
}

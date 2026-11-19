using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AFMI_Montage_projet_2.Services.Updates
{
    public class ReleaseAsset
    {
        public string Name { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>Adresse API du fichier (api.github.com/…/releases/assets/{id}) : seule voie de téléchargement d'un dépôt privé.</summary>
        public string ApiUrl { get; set; } = string.Empty;
        public long Size { get; set; }

        /// <summary>Empreinte publiée par GitHub (« sha256:… ») ou lue dans le fichier .sha256 joint à la version.</summary>
        public string? Sha256 { get; set; }
    }

    public class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public Version? Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public DateTime? PublishedAt { get; set; }
        public ReleaseAsset? Installer { get; set; }
        public ReleaseAsset? ChecksumFile { get; set; }
    }

    public class UpdateCheckResult
    {
        public bool IsConfigured { get; set; }
        public bool UpdateAvailable { get; set; }

        /// <summary>Dépôt privé inaccessible depuis ce poste : clé d'accès absente, expirée ou sans droit sur le dépôt (ADR 29).</summary>
        public bool NeedsAccessToken { get; set; }
        public GitHubRelease? Release { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Mises à jour par les « Releases » GitHub (ADR 26) : la dernière version publiée est comparée à la version installée,
    /// l'installateur Inno Setup est téléchargé par l'accès web sécurisé, son empreinte SHA-256 est vérifiée
    /// (refus en cas d'absence ou de différence), puis il est lancé en mode silencieux et l'application se ferme.
    /// Le dépôt (« propriétaire/nom ») vient du réglage Update_GitHubRepository, sinon de Config/update.json
    /// (renseigné automatiquement par la chaîne de publication GitHub Actions).
    /// Dépôt privé : une clé d'accès en lecture seule (jeton GitHub « fine-grained », Contents: Read-only) est saisie
    /// par poste, chiffrée DPAPI dans les réglages locaux (jamais partagés avec l'équipe, ni intégrée à l'installateur),
    /// et n'est envoyée qu'à api.github.com.
    /// </summary>
    public class GitHubUpdateService
    {
        public const string RepositorySetting = "Update_GitHubRepository";
        public const string TokenSetting = "Update_GitHubTokenProtected";
        public const string InstallerPattern = @"^AFMIMontageProjet_Setup_v[\d.]+\.exe$";
        public const long MaxInstallerBytes = 600L * 1024 * 1024;

        private static readonly Regex RepositoryRegex = new(@"^[A-Za-z0-9-]{1,39}/[A-Za-z0-9._-]{1,100}$", RegexOptions.Compiled);
        private readonly SafeHttpFetcher _fetcher;

        public GitHubUpdateService(SafeHttpFetcher? fetcher = null) => _fetcher = fetcher ?? new SafeHttpFetcher { MaxAttempts = 2 };

        public static string CurrentVersion => AutoUpdaterService.CurrentVersion;

        /// <summary>Dépôt configuré (« propriétaire/nom »), ou chaîne vide.</summary>
        public static string Repository
        {
            get
            {
                string fromSettings = string.Empty;
                try { fromSettings = SettingsService.GetSetting(RepositorySetting, ""); }
                catch (Exception ex) { AppLogger.Warn(nameof(GitHubUpdateService), "Réglage du dépôt illisible.", ex); }
                if (IsValidRepository(fromSettings)) return NormalizeRepository(fromSettings);

                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "update.json");
                    if (File.Exists(path))
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(path));
                        if (doc.RootElement.TryGetProperty("GitHubRepository", out var repo) && IsValidRepository(repo.GetString()))
                            return NormalizeRepository(repo.GetString()!);
                    }
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    AppLogger.Warn(nameof(GitHubUpdateService), "Config/update.json illisible.", ex);
                }
                return string.Empty;
            }
        }

        /// <summary>Accepte « propriétaire/nom » ou l'adresse https://github.com/propriétaire/nom.</summary>
        public static string NormalizeRepository(string value)
        {
            string v = value.Trim().TrimEnd('/');
            v = Regex.Replace(v, @"^https?://(www\.)?github\.com/", "", RegexOptions.IgnoreCase);
            if (v.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) v = v[..^4];
            return v;
        }

        public static bool IsValidRepository(string? value) =>
            !string.IsNullOrWhiteSpace(value) && RepositoryRegex.IsMatch(NormalizeRepository(value));

        /// <summary>Clé d'accès GitHub de ce poste (déchiffrée), ou chaîne vide.</summary>
        public static string AccessToken
        {
            get
            {
                try { return Helpers.EncryptionHelper.Unprotect(SettingsService.GetSetting(TokenSetting, "")).Trim(); }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(GitHubUpdateService), "Clé d'accès GitHub illisible.", ex);
                    return string.Empty;
                }
            }
        }

        public static bool HasAccessToken => AccessToken.Length > 0;

        public static void SaveAccessToken(string? token) =>
            SettingsService.SaveSetting(TokenSetting, string.IsNullOrWhiteSpace(token) ? "" : Helpers.EncryptionHelper.Protect(token.Trim()));

        /// <summary>Format des jetons GitHub (classiques ghp_, « fine-grained » github_pat_).</summary>
        public static bool LooksLikeToken(string? token) =>
            !string.IsNullOrWhiteSpace(token) && Regex.IsMatch(token.Trim(), @"^(ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})$");

        private static Dictionary<string, string> ApiHeaders(string accept = "application/vnd.github+json")
        {
            var headers = new Dictionary<string, string>
            {
                ["Accept"] = accept,
                ["X-GitHub-Api-Version"] = "2022-11-28"
            };
            string token = AccessToken;
            if (token.Length > 0) headers["Authorization"] = "Bearer " + token;
            return headers;
        }

        private static bool IsGitHubApiUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);

        public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            var result = new UpdateCheckResult { CurrentVersion = CurrentVersion };
            string repo = Repository;
            if (repo.Length == 0)
            {
                result.Message = "Aucun dépôt GitHub configuré pour les mises à jour (Paramètres › Mises à jour).";
                return result;
            }
            result.IsConfigured = true;

            SafeFetchResponse response;
            try
            {
                response = await _fetcher.FetchWithRetryAsync($"https://api.github.com/repos/{repo}/releases/latest", cancellationToken: cancellationToken, headers: ApiHeaders());
            }
            catch (SafeFetchException ex) when (ex.Message.StartsWith("HTTP 404", StringComparison.Ordinal) || ex.Message.StartsWith("HTTP 401", StringComparison.Ordinal))
            {
                result.NeedsAccessToken = true;
                result.Message = HasAccessToken
                    ? $"Dépôt {repo} introuvable avec la clé d'accès de ce poste, ou aucune version publiée (clé expirée ou sans accès à ce dépôt ?)."
                    : $"Dépôt {repo} privé ou sans version publiée : saisissez la clé d'accès GitHub dans Paramètres › Mises à jour.";
                return result;
            }
            var release = ParseRelease(Encoding.UTF8.GetString(response.Body));
            result.Release = release;

            if (release.Version == null || !Version.TryParse(CurrentVersion, out var current))
            {
                result.Message = $"Version publiée illisible ({release.TagName}).";
                return result;
            }
            if (release.Installer == null)
            {
                result.Message = $"La version {release.TagName} ne contient pas d'installateur AFMIMontageProjet_Setup_v….exe.";
                return result;
            }

            result.UpdateAvailable = release.Version > current;
            result.Message = result.UpdateAvailable
                ? $"Nouvelle version disponible : {release.Version} (installée : {CurrentVersion})."
                : $"Vous utilisez la dernière version ({CurrentVersion}).";
            return result;
        }

        /// <summary>Lit la réponse « releases/latest » de l'API GitHub.</summary>
        public static GitHubRelease ParseRelease(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

            string tag = Str(root, "tag_name");
            var release = new GitHubRelease
            {
                TagName = tag,
                Version = Version.TryParse(Regex.Replace(tag, @"^[vV]", ""), out var v) ? v : null,
                Name = Str(root, "name"),
                Notes = Str(root, "body"),
                HtmlUrl = Str(root, "html_url"),
                PublishedAt = DateTime.TryParse(Str(root, "published_at"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d) ? d : null
            };

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var asset = new ReleaseAsset
                    {
                        Name = Str(a, "name"),
                        DownloadUrl = Str(a, "browser_download_url"),
                        ApiUrl = Str(a, "url"),
                        Size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : 0
                    };
                    string digest = Str(a, "digest");
                    if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) asset.Sha256 = digest[7..].ToLowerInvariant();

                    if (Regex.IsMatch(asset.Name, InstallerPattern, RegexOptions.IgnoreCase)) release.Installer = asset;
                    else if (asset.Name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase)) release.ChecksumFile = asset;
                }
            }
            return release;
        }

        /// <summary>
        /// Télécharge l'installateur dans %TEMP% et vérifie son empreinte SHA-256. Renvoie le chemin du fichier vérifié.
        /// </summary>
        public async Task<string> DownloadVerifiedInstallerAsync(GitHubRelease release, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var installer = release.Installer ?? throw new InvalidOperationException("Aucun installateur dans cette version.");
            var (url, headers) = AssetRequest(installer);

            string expected = installer.Sha256 ?? await ReadChecksumFileAsync(release, cancellationToken)
                ?? throw new InvalidOperationException("Aucune empreinte SHA-256 publiée pour cet installateur : installation refusée par sécurité.");

            string folder = Path.Combine(Path.GetTempPath(), "AFMI_Update");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, Path.GetFileName(installer.Name));
            await _fetcher.DownloadToFileAsync(url, path, MaxInstallerBytes, progress, headers, cancellationToken);

            string actual = ComputeSha256(path);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new InvalidOperationException("L'empreinte de l'installateur téléchargé ne correspond pas à celle publiée : fichier supprimé, installation annulée.");
            }
            AppLogger.Info(nameof(GitHubUpdateService), $"Installateur {installer.Name} téléchargé et vérifié (SHA-256 {actual}).");
            return path;
        }

        private async Task<string?> ReadChecksumFileAsync(GitHubRelease release, CancellationToken cancellationToken)
        {
            if (release.ChecksumFile == null) return null;
            var (url, headers) = AssetRequest(release.ChecksumFile);
            var response = await _fetcher.FetchWithRetryAsync(url, cancellationToken: cancellationToken, headers: headers);
            var match = Regex.Match(Encoding.UTF8.GetString(response.Body), @"\b[a-fA-F0-9]{64}\b");
            return match.Success ? match.Value.ToLowerInvariant() : null;
        }

        /// <summary>
        /// Adresse et en-têtes de téléchargement d'un fichier de version : dépôt privé (clé présente) par l'API
        /// (application/octet-stream, redirection vers le stockage GitHub sans la clé), sinon lien public github.com.
        /// </summary>
        public static (string Url, Dictionary<string, string>? Headers) AssetRequest(ReleaseAsset asset)
        {
            if (HasAccessToken && IsGitHubApiUrl(asset.ApiUrl))
                return (asset.ApiUrl, ApiHeaders("application/octet-stream"));

            if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Adresse de téléchargement inattendue : seules les versions publiées sur GitHub sont acceptées.");
            return (asset.DownloadUrl, null);
        }

        public static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// Lance l'installateur vérifié en mode silencieux (il ferme l'application, met à jour puis la relance) et quitte.
        /// Les projets et réglages (%APPDATA%, %LOCALAPPDATA%) ne sont pas touchés par la mise à jour.
        /// </summary>
        public static void LaunchInstallerAndExit(string installerPath)
        {
            // L'installation à la fermeture (AutoUpdateCoordinator) ne doit pas relancer un second installateur
            AutoUpdateCoordinator.MarkInstallerLaunched();
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });
            System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        }
    }
}

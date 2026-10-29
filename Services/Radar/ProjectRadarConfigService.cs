using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>
    /// خدمة مركزية لإدارة ملفات إعدادات رادار المشاريع:
    /// - مفتاح التفعيل الشامل ProjectRadar.Enabled
    /// - خلاصات rss_feeds.json
    /// - محاور AFMI الاستراتيجية afmi_axes.json
    /// - ملف تعريف AFMI المؤسسي afmi_profile.json
    /// تدعم الكتابة الذرية (Atomic Write) لسلامة البيانات.
    /// </summary>
    public static class ProjectRadarConfigService
    {
        private static readonly object FileLock = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static string AppDataFolder =>
            Models.AppDbContext.DataDirectoryOverride is string overrideDir
                ? Path.Combine(overrideDir, "config")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AFMI_Montage_Projet");

        public static string AppBaseConfigFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        #region Feature Flag (ProjectRadar.Enabled)

        public const string SettingKeyProjectRadarEnabled = "ProjectRadar.Enabled";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    string val = SettingsService.GetSetting(SettingKeyProjectRadarEnabled, "true");
                    return !string.Equals(val, "false", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return true;
                }
            }
        }

        public static void SetEnabled(bool enabled)
        {
            SettingsService.SaveSetting(SettingKeyProjectRadarEnabled, enabled ? "true" : "false");
        }

        #endregion

        #region RSS Feeds Config (rss_feeds.json)

        private static string GetFeedsFilePath()
        {
            EnsureAppDataDirectory();
            string appDataFile = Path.Combine(AppDataFolder, "rss_feeds.json");
            if (File.Exists(appDataFile)) return appDataFile;

            string baseFile = Path.Combine(AppBaseConfigFolder, "rss_feeds.json");
            if (File.Exists(baseFile))
            {
                try
                {
                    File.Copy(baseFile, appDataFile, false);
                    return appDataFile;
                }
                catch
                {
                    return baseFile;
                }
            }

            return appDataFile;
        }

        public static List<RssFeedConfig> LoadFeeds()
        {
            lock (FileLock)
            {
                try
                {
                    string path = GetFeedsFilePath();
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var list = JsonSerializer.Deserialize<List<RssFeedConfig>>(json);
                        if (list != null && list.Count > 0)
                        {
                            if (MigrateDefaultFeeds(list))
                            {
                                EnsureAppDataDirectory();
                                AtomicWriteJson(Path.Combine(AppDataFolder, "rss_feeds.json"), list);
                            }
                            return list;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    AppLogger.Warn(nameof(ProjectRadarConfigService), "Lecture de rss_feeds.json impossible, flux par défaut utilisés.", ex);
                }

                return GetDefaultFeeds();
            }
        }

        /// <summary>
        /// Révision de la liste des flux par défaut. À incrémenter quand une adresse par défaut change :
        /// les copies existantes (rss_feeds.json de l'utilisateur) sont alors corrigées au chargement,
        /// sauf si l'utilisateur a lui-même modifié l'adresse.
        /// </summary>
        public const int DefaultFeedsRevision = 2;

        /// <summary>Anciennes adresses par défaut devenues invalides (révision 1), vérifiées le 29/10/2026.</summary>
        private static readonly Dictionary<string, string> ObsoleteDefaultUrls = new()
        {
            ["feed-gef-sgp"] = "https://www.thegef.org/newsroom/all/rss.xml",
            ["feed-undp-morocco"] = "https://www.undp.org/morocco/rss.xml",
            ["feed-worldbank-climate"] = "https://www.worldbank.org/en/news/all.rss",
            ["feed-fao-forestry"] = "https://www.fao.org/newsroom/rss/en/",
            ["feed-cepf-med"] = "https://www.cepf.net/rss.xml"
        };

        /// <summary>Corrige les flux par défaut d'une ancienne révision et ajoute les nouveaux. Renvoie vrai si la liste a changé.</summary>
        public static bool MigrateDefaultFeeds(List<RssFeedConfig> feeds)
        {
            bool changed = false;
            var defaults = GetDefaultFeeds();

            foreach (var feed in feeds.Where(f => f.IsDefault && f.DefaultsRevision < DefaultFeedsRevision))
            {
                var current = defaults.FirstOrDefault(d => d.Id == feed.Id);
                if (current != null && ObsoleteDefaultUrls.TryGetValue(feed.Id, out var obsoleteUrl) &&
                    string.Equals(feed.Url, obsoleteUrl, StringComparison.OrdinalIgnoreCase))
                {
                    feed.Url = current.Url;
                    feed.Name = current.Name;
                    feed.Enabled = current.Enabled && feed.Enabled;
                    feed.Verified = current.Verified;
                    feed.LastStatus = current.LastStatus;
                    feed.ConsecutiveFailures = 0;
                    feed.LastError = null;
                    feed.ETag = null;
                    feed.LastModified = null;
                }
                feed.DefaultsRevision = DefaultFeedsRevision;
                changed = true;
            }

            foreach (var missing in defaults.Where(d => d.DefaultsRevision > 1 && feeds.All(f => f.Id != d.Id)))
            {
                feeds.Add(missing);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Enregistre l'état de lecture des flux (statut, santé, ETag) dans la configuration actuelle,
        /// sans écraser les flux ajoutés, supprimés ou modifiés entre-temps par l'utilisateur.
        /// </summary>
        public static void UpdateFeedStatuses(IEnumerable<RssFeedConfig> fetched)
        {
            lock (FileLock)
            {
                var feeds = LoadFeeds();
                foreach (var source in fetched)
                {
                    var target = feeds.FirstOrDefault(f => f.Id == source.Id);
                    if (target == null || !string.Equals(target.Url, source.Url, StringComparison.OrdinalIgnoreCase)) continue;
                    target.LastFetchedAt = source.LastFetchedAt;
                    target.LastStatus = source.LastStatus;
                    target.LastItemCount = source.LastItemCount;
                    target.LastSuccessAt = source.LastSuccessAt;
                    target.ConsecutiveFailures = source.ConsecutiveFailures;
                    target.LastError = source.LastError;
                    target.ETag = source.ETag;
                    target.LastModified = source.LastModified;
                }
                SaveFeeds(feeds);
            }
        }

        public static void SaveFeeds(List<RssFeedConfig> feeds)
        {
            lock (FileLock)
            {
                EnsureAppDataDirectory();
                string targetPath = Path.Combine(AppDataFolder, "rss_feeds.json");
                AtomicWriteJson(targetPath, feeds);
            }
        }

        public static void ToggleFeed(string feedId, bool enabled)
        {
            var feeds = LoadFeeds();
            var target = feeds.FirstOrDefault(f => f.Id == feedId);
            if (target != null)
            {
                target.Enabled = enabled;
                SaveFeeds(feeds);
            }
        }

        public static void AddCustomFeed(RssFeedConfig feed)
        {
            var feeds = LoadFeeds();
            feed.IsDefault = false;
            if (string.IsNullOrWhiteSpace(feed.Id)) feed.Id = Guid.NewGuid().ToString("N");
            feeds.Add(feed);
            SaveFeeds(feeds);
        }

        public static void UpdateCustomFeed(RssFeedConfig updated)
        {
            var feeds = LoadFeeds();
            int idx = feeds.FindIndex(f => f.Id == updated.Id);
            if (idx >= 0)
            {
                // Preserver isDefault
                updated.IsDefault = feeds[idx].IsDefault;
                feeds[idx] = updated;
                SaveFeeds(feeds);
            }
        }

        public static bool DeleteCustomFeed(string feedId)
        {
            var feeds = LoadFeeds();
            var target = feeds.FirstOrDefault(f => f.Id == feedId);
            if (target != null && !target.IsDefault)
            {
                feeds.Remove(target);
                SaveFeeds(feeds);
                return true;
            }
            return false; // Default feeds cannot be deleted, only disabled
        }

        #endregion

        #region AFMI Strategic Axes (afmi_axes.json)

        private static string GetAxesFilePath()
        {
            EnsureAppDataDirectory();
            string appDataFile = Path.Combine(AppDataFolder, "afmi_axes.json");
            if (File.Exists(appDataFile)) return appDataFile;

            string baseFile = Path.Combine(AppBaseConfigFolder, "afmi_axes.json");
            if (File.Exists(baseFile))
            {
                try
                {
                    File.Copy(baseFile, appDataFile, false);
                    return appDataFile;
                }
                catch
                {
                    return baseFile;
                }
            }

            return appDataFile;
        }

        public static List<AfmiAxis> LoadAxes()
        {
            lock (FileLock)
            {
                try
                {
                    string path = GetAxesFilePath();
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var list = JsonSerializer.Deserialize<List<AfmiAxis>>(json);
                        if (list != null && list.Count > 0)
                        {
                            // Vérifier si la liste chargée contient uniquement les anciens placeholders "À compléter"
                            bool hasOnlyPlaceholders = list.All(a => a.Title.Contains("À compléter") || a.Title.Contains("يُستكمل"));
                            if (!hasOnlyPlaceholders)
                            {
                                return list;
                            }
                        }
                    }
                }
                catch { }

                var defaultAxes = GetDefaultAfmiAxes();
                try
                {
                    SaveAxes(defaultAxes);
                }
                catch { }
                return defaultAxes;
            }
        }

        public static void SaveAxes(List<AfmiAxis> axes)
        {
            lock (FileLock)
            {
                EnsureAppDataDirectory();
                string targetPath = Path.Combine(AppDataFolder, "afmi_axes.json");
                AtomicWriteJson(targetPath, axes);
            }
        }

        #endregion

        #region AFMI Profile (afmi_profile.json)

        private static string GetProfileFilePath()
        {
            EnsureAppDataDirectory();
            string appDataFile = Path.Combine(AppDataFolder, "afmi_profile.json");
            if (File.Exists(appDataFile)) return appDataFile;

            string baseFile = Path.Combine(AppBaseConfigFolder, "afmi_profile.json");
            if (File.Exists(baseFile))
            {
                try
                {
                    File.Copy(baseFile, appDataFile, false);
                    return appDataFile;
                }
                catch
                {
                    return baseFile;
                }
            }

            return appDataFile;
        }

        public static AfmiProfile LoadProfile()
        {
            lock (FileLock)
            {
                try
                {
                    string path = GetProfileFilePath();
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var profile = JsonSerializer.Deserialize<AfmiProfile>(json);
                        if (profile != null) return profile;
                    }
                }
                catch { }

                return new AfmiProfile();
            }
        }

        public static void SaveProfile(AfmiProfile profile)
        {
            lock (FileLock)
            {
                EnsureAppDataDirectory();
                string targetPath = Path.Combine(AppDataFolder, "afmi_profile.json");
                AtomicWriteJson(targetPath, profile);
            }
        }

        #endregion

        #region Helpers & Fallbacks

        private static void EnsureAppDataDirectory()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        private static void AtomicWriteJson<T>(string targetPath, T data)
        {
            string tempFile = targetPath + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                string json = JsonSerializer.Serialize(data, JsonOptions);
                File.WriteAllText(tempFile, json);
                if (File.Exists(targetPath))
                {
                    File.Replace(tempFile, targetPath, null);
                }
                else
                {
                    File.Move(tempFile, targetPath);
                }
            }
            catch
            {
                // Fallback direct write if atomic replace is not supported
                try
                {
                    string json = JsonSerializer.Serialize(data, JsonOptions);
                    File.WriteAllText(targetPath, json);
                }
                catch { }
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        public static List<AfmiAxis> GetDefaultAfmiAxes()
        {
            return new List<AfmiAxis>
            {
                new()
                {
                    Id = 1,
                    Title = "Axe 1: Renforcement des capacités de gouvernance / المحور 1: تعزيز قدرات الحكامة",
                    Description = "Renforcement du partenariat (organes statutaires, comités techniques, planification concertée) et Gouvernance des acteurs locaux (appui aux associations de gestion sylvo-pastorale AGS, coopératives et ONG locales dans le montage, gestion et conduite de projets en partenariat).",
                    IsCompleted = true
                },
                new()
                {
                    Id = 2,
                    Title = "Axe 2: Gestion des ressources naturelles / المحور 2: تدبير الموارد الطبيعية",
                    Description = "Aménagement et gestion des parcours (projets sylvo-pastoraux, chartes pastorales), gestion forestière adaptative et participative (gestion contractuelle), conservation de la biodiversité et lutte contre la pollution (cèdre, singe magot, sites sensibles), gestion de l'eau et ressources hydrauliques (zones humides, assainissement, efficacité irrigation).",
                    IsCompleted = true
                },
                new()
                {
                    Id = 3,
                    Title = "Axe 3: Énergie et habitat / المحور 3: الطاقة والسكن",
                    Description = "Gestion durable des sources d'énergie (utilisation rationnelle du bois de chauffe, plantations énergétiques, énergies solaires et renouvelables), économies d'énergie et amélioration de l'habitat (isolation thermique du bâti, efficacité énergétique, réduction de la pression sur les forêts).",
                    IsCompleted = true
                },
                new()
                {
                    Id = 4,
                    Title = "Axe 4: Diversification et amélioration des revenus / المحور 4: تنويع وتحسين الدخل",
                    Description = "Organisation des filières et renforcement des acteurs économiques (formation des producteurs, accompagnement des organisations), valorisation et promotion des produits du terroir (safran, produits forestiers non ligneux, labellisation), promotion du tourisme durable (Réseau Cèdre, écotourisme).",
                    IsCompleted = true
                },
                new()
                {
                    Id = 5,
                    Title = "Axe 5: Communication et mise en réseau / المحور 5: التواصل والتشبيك",
                    Description = "Information et sensibilisation locale (campagnes et ateliers scolaires/populations), communication événementielle et externe (Journée de la poésie Amazigh, site web, brochures), vie en réseau (Réseau Méditerranéen RMFM, Réseau Africain RAFM, Réseau International des Forêts Modèles RIFM).",
                    IsCompleted = true
                }
            };
        }

        private static List<RssFeedConfig> GetDefaultFeeds()
        {
            // Adresses vérifiées le 29/10/2026. Sources sans flux public fonctionnel : conservées mais désactivées.
            const string noPublicFeed = "Désactivé : aucun flux public fonctionnel trouvé (vérifié le 29/10/2026). Remplacez l'adresse si la source en publie un.";
            const int rev = DefaultFeedsRevision;
            return new List<RssFeedConfig>
            {
                new() { Id = "feed-gef-sgp", Name = "GEF - Fonds pour l'Environnement Mondial", Url = "https://www.thegef.org/rss.xml", Category = "Environnement & Climat", Topic = "Financements de projets", Enabled = true, IsDefault = true, Verified = true, Language = "en", DefaultsRevision = rev },
                new() { Id = "feed-undp-morocco", Name = "PNUD Maroc & International", Url = "https://www.undp.org/morocco/rss.xml", Category = "Développement Durable", Topic = "Appels à projets", Enabled = false, IsDefault = true, Verified = false, Language = "fr", LastStatus = noPublicFeed, DefaultsRevision = rev },
                new() { Id = "feed-eu-greendeal", Name = "Union Européenne (Commission Européenne & Green Deal)", Url = "https://ec.europa.eu/commission/presscorner/api/rss?language=fr", Category = "Coopération Internationale", Topic = "Pacte Vert & Subventions", Enabled = true, IsDefault = true, Verified = true, Language = "fr", DefaultsRevision = rev },
                new() { Id = "feed-worldbank-climate", Name = "Banque Mondiale (Environnement & Climat)", Url = "https://www.worldbank.org/en/news/all.rss", Category = "Finance Durable", Topic = "Climat & Forêts", Enabled = false, IsDefault = true, Verified = false, Language = "en", LastStatus = noPublicFeed, DefaultsRevision = rev },
                new() { Id = "feed-afd-ffem", Name = "AFD / FFEM (Fonds Français pour l'Environnement Mondial)", Url = "https://www.afd.fr/fr/rss.xml", Category = "Biodiversité & Territoires", Topic = "Subventions de préservation", Enabled = true, IsDefault = true, Verified = true, Language = "fr", DefaultsRevision = rev },
                new() { Id = "feed-iucn-med", Name = "UICN Méditerranée (Conservation de la Nature)", Url = "https://www.iucn.org/rss.xml", Category = "Conservation de la Nature", Topic = "Aires protégées & Espèces", Enabled = true, IsDefault = true, Verified = true, Language = "fr", DefaultsRevision = rev },
                new() { Id = "feed-fao-forestry", Name = "FAO (Forêts et Pastoralisme)", Url = "https://www.fao.org/feeds/fao-newsroom-rss", Category = "Agriculture & Forêts", Topic = "Gestion forestière durable", Enabled = true, IsDefault = true, Verified = true, Language = "en", DefaultsRevision = rev },
                new() { Id = "feed-cepf-med", Name = "CEPF Méditerranée (Critical Ecosystem Partnership Fund)", Url = "https://www.cepf.net/rss.xml", Category = "Points Chauds de Biodiversité", Topic = "Subventions directes aux ONG", Enabled = false, IsDefault = true, Verified = false, Language = "en", LastStatus = noPublicFeed, DefaultsRevision = rev },
                new() { Id = "feed-adaptation-fund", Name = "Fonds pour l'Adaptation (Adaptation Fund)", Url = "https://www.adaptation-fund.org/feed/", Category = "Adaptation au changement climatique", Topic = "Financement de projets d'adaptation (accès direct)", Enabled = true, IsDefault = true, Verified = true, Language = "en", DefaultsRevision = rev },
                new() { Id = "feed-onu-climat", Name = "ONU Info - Climat et environnement", Url = "https://news.un.org/feed/subscribe/fr/news/topic/climate-change/feed/rss.xml", Category = "Veille climat", Topic = "Actualité climat, COP et environnement", Enabled = true, IsDefault = true, Verified = true, Language = "fr", DefaultsRevision = rev }
            };
        }

        #endregion
    }
}

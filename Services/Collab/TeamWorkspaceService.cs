using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public enum RefreshOutcome
    {
        Updated,
        AlreadyUpToDate,
        NoOfficialVersion,
        PendingLocalChanges,
        NotAllowed
    }

    public class RefreshResult
    {
        public RefreshOutcome Outcome { get; set; }
        public string Message { get; set; } = string.Empty;
        public OfficialSnapshotInfo? Info { get; set; }
        public int PendingChangesCount { get; set; }
    }

    /// <summary>
    /// Orchestration de l'espace d'équipe (ADR 23) :
    /// - poste officiel : publication de la version officielle des projets sur le Drive du compte officiel ;
    /// - poste membre : mise à jour depuis la version officielle, détection et envoi des contributions.
    /// La version reçue est conservée (team\official_base.db) comme référence pour détecter les modifications du membre.
    /// </summary>
    public class TeamWorkspaceService
    {
        private static TeamWorkspaceService? _instance;
        public static TeamWorkspaceService Instance => _instance ??= new TeamWorkspaceService();

        public const string MirrorPublishedAtSetting = "Collab_MirrorPublishedAt";
        public const string LastPublishedAtSetting = "Collab_LastPublishedAt";
        public const string LastPublishedHashSetting = "Collab_LastPublishedHash";
        private static readonly TimeSpan AutoPublishInterval = TimeSpan.FromMinutes(10);

        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Une intégration a modifié la version officielle : publication à faire.</summary>
        public bool PublishRequested { get; set; }

        public static string TeamFolder => Path.Combine(Path.GetDirectoryName(AppDbContext.LocalDatabasePath)!, "team");
        public static string BaseSnapshotPath => Path.Combine(TeamFolder, "official_base.db");

        public bool HasBaseSnapshot => File.Exists(BaseSnapshotPath);

        public DateTime? MirrorPublishedAtUtc => ReadDate(MirrorPublishedAtSetting);
        public DateTime? LastPublishedAtUtc => ReadDate(LastPublishedAtSetting);

        private AuthService Auth => AuthService.Instance;

        #region Poste officiel

        /// <summary>
        /// Publie la version officielle (base locale assainie) sur l'espace d'équipe.
        /// Avec <paramref name="onlyIfChanged"/>, rien n'est envoyé si le contenu partagé est identique à la dernière publication.
        /// Renvoie null si rien n'a été envoyé.
        /// </summary>
        public async Task<OfficialSnapshotInfo?> PublishAsync(bool onlyIfChanged = false, CancellationToken cancellationToken = default)
        {
            if (!Auth.IsOfficialStation || Auth.CollabStore == null)
                throw new InvalidOperationException("La publication se fait depuis le poste officiel, connecté avec le compte de l'association.");

            await _gate.WaitAsync(cancellationToken);
            string tempPath = Path.Combine(TeamFolder, $"publish_{Guid.NewGuid():N}.db");
            try
            {
                int projectCount = await Task.Run(() => OfficialSnapshotService.CreateSanitizedSnapshot(AppDbContext.LocalDatabasePath, tempPath), cancellationToken);
                string hash = ComputeFileHash(tempPath);
                if (onlyIfChanged && hash == SettingsService.GetSetting(LastPublishedHashSetting, ""))
                {
                    PublishRequested = false;
                    _lastAutoPublishCheckUtc = DateTime.UtcNow;
                    return null;
                }

                var info = await Auth.CollabStore.UploadOfficialSnapshotAsync(tempPath, projectCount, cancellationToken);
                PublishRequested = false;
                _lastAutoPublishCheckUtc = DateTime.UtcNow;
                SettingsService.SaveSetting(LastPublishedHashSetting, hash);
                SettingsService.SaveSetting(LastPublishedAtSetting, info.PublishedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                AppLogger.Info(nameof(TeamWorkspaceService), $"Version officielle publiée : {projectCount} projet(s).");
                return info;
            }
            finally
            {
                TryDelete(tempPath);
                _gate.Release();
            }
        }

        private DateTime _lastAutoPublishCheckUtc = DateTime.MinValue;

        /// <summary>
        /// Appelé par la synchronisation sur le poste officiel : publie aussitôt si une intégration l'a demandé,
        /// sinon vérifie au plus toutes les 10 minutes si le contenu partagé a changé (modifications directes de l'administrateur).
        /// </summary>
        public async Task<bool> AutoPublishIfNeededAsync(CancellationToken cancellationToken = default)
        {
            if (!Auth.IsOfficialStation) return false;
            if (!PublishRequested && DateTime.UtcNow - _lastAutoPublishCheckUtc < AutoPublishInterval) return false;

            return await PublishAsync(onlyIfChanged: true, cancellationToken) != null;
        }

        private static string ComputeFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }

        #endregion

        #region Poste membre

        /// <summary>Modifications locales non encore envoyées, par rapport à la dernière version officielle reçue.</summary>
        public async Task<List<DetectedChange>> GetUnsentLocalChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!HasBaseSnapshot || Auth.IsOfficialStation) return new List<DetectedChange>();
            var detected = await Task.Run(() => TeamChangeDetector.Detect(BaseSnapshotPath, AppDbContext.LocalDatabasePath), cancellationToken);
            return await ContributionService.Instance.FilterUnsentAsync(detected, cancellationToken);
        }

        /// <summary>Indique si une version officielle plus récente que celle du poste est disponible.</summary>
        public async Task<OfficialSnapshotInfo?> GetNewerOfficialVersionAsync(CancellationToken cancellationToken = default)
        {
            if (Auth.CollabStore == null || Auth.IsOfficialStation) return null;
            var info = await Auth.CollabStore.GetOfficialSnapshotInfoAsync(cancellationToken);
            if (info == null) return null;
            var local = MirrorPublishedAtUtc;
            return local == null || info.PublishedAtUtc > local.Value.AddSeconds(1) ? info : null;
        }

        /// <summary>
        /// Remplace les projets du poste par la version officielle.
        /// Refusé tant que des modifications locales n'ont pas été envoyées, sauf <paramref name="discardLocalChanges"/>.
        /// </summary>
        public async Task<RefreshResult> RefreshFromOfficialAsync(bool discardLocalChanges, bool force = false, CancellationToken cancellationToken = default)
        {
            if (Auth.CollabStore == null)
                return new RefreshResult { Outcome = RefreshOutcome.NotAllowed, Message = "Connectez-vous d'abord à l'espace d'équipe." };
            if (Auth.IsOfficialStation)
                return new RefreshResult { Outcome = RefreshOutcome.NotAllowed, Message = "Le poste officiel détient la version de référence : il ne se met pas à jour depuis Drive." };
            if (AppDbContext.IsCloudDatabaseConfigured)
                return new RefreshResult { Outcome = RefreshOutcome.NotAllowed, Message = "Ce poste utilise une base PostgreSQL : la version officielle ne s'applique qu'à une base locale." };

            if (!discardLocalChanges)
            {
                var pending = await GetUnsentLocalChangesAsync(cancellationToken);
                if (pending.Count > 0)
                {
                    return new RefreshResult
                    {
                        Outcome = RefreshOutcome.PendingLocalChanges,
                        PendingChangesCount = pending.Count,
                        Message = $"{pending.Count} modification(s) locale(s) n'ont pas été envoyées. Envoyez-les d'abord, ou confirmez qu'elles peuvent être abandonnées."
                    };
                }
            }

            await _gate.WaitAsync(cancellationToken);
            string downloadPath = Path.Combine(TeamFolder, "official_download.db");
            try
            {
                var info = await Auth.CollabStore.GetOfficialSnapshotInfoAsync(cancellationToken);
                if (info == null)
                    return new RefreshResult { Outcome = RefreshOutcome.NoOfficialVersion, Message = "Aucune version officielle n'a encore été publiée par le poste officiel." };

                var local = MirrorPublishedAtUtc;
                if (!force && local != null && info.PublishedAtUtc <= local.Value.AddSeconds(1) && HasBaseSnapshot)
                    return new RefreshResult { Outcome = RefreshOutcome.AlreadyUpToDate, Info = info, Message = "Vos projets sont déjà à jour." };

                info = await Auth.CollabStore.DownloadOfficialSnapshotAsync(downloadPath, cancellationToken);
                if (info == null)
                    return new RefreshResult { Outcome = RefreshOutcome.NoOfficialVersion, Message = "Version officielle indisponible." };

                var applied = await Task.Run(() => OfficialSnapshotService.ApplySnapshotToLocal(downloadPath, AppDbContext.LocalDatabasePath), cancellationToken);
                File.Copy(downloadPath, BaseSnapshotPath, overwrite: true);
                SettingsService.SaveSetting(MirrorPublishedAtSetting, info.PublishedAtUtc.ToString("O", CultureInfo.InvariantCulture));

                // Le schéma local peut être plus récent que celui du poste officiel : compléter les colonnes
                using (var db = new AppDbContext()) db.SeedDefaultData();

                return new RefreshResult
                {
                    Outcome = RefreshOutcome.Updated,
                    Info = info,
                    Message = $"Projets mis à jour depuis la version officielle du {info.PublishedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} ({applied.ProjectCount} projet(s)). Sauvegarde de vos données précédentes : {Path.GetFileName(applied.BackupPath)}."
                };
            }
            finally
            {
                TryDelete(downloadPath);
                _gate.Release();
            }
        }

        #endregion

        private static DateTime? ReadDate(string key)
        {
            var raw = SettingsService.GetSetting(key, "");
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : null;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException ex) { AppLogger.Warn(nameof(TeamWorkspaceService), $"Fichier temporaire non supprimé : {path}", ex); }
        }
    }
}

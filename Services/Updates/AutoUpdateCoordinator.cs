using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AFMI_Montage_projet_2.Services.Updates
{
    /// <summary>
    /// Mise à jour automatique (ADR 29) : au démarrage, la nouvelle version est téléchargée et vérifiée (SHA-256)
    /// en arrière-plan, puis installée en silence à la fermeture du logiciel, sans interrompre le travail en cours.
    /// L'utilisateur peut aussi l'installer tout de suite. Réglage « Update_AutoInstall » : tant qu'il n'est pas
    /// choisi, la question est posée la première fois qu'une nouvelle version est détectée (ADR 30).
    /// </summary>
    public static class AutoUpdateCoordinator
    {
        public const string AutoInstallSetting = "Update_AutoInstall";
        public const string TokenPromptDisabledSetting = "Update_TokenPromptDisabled";

        /// <summary>Arguments Inno Setup à la fermeture : l'utilisateur a quitté, l'application n'est pas relancée.</summary>
        public const string InstallOnExitArguments = "/SILENT /SP- /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /NORESTARTAPPLICATIONS";

        private static readonly object Gate = new();

        /// <summary>Vrai seulement si l'utilisateur a activé la mise à jour automatique (non choisi = faux).</summary>
        public static bool AutoInstallEnabled
        {
            get => string.Equals(SettingsService.GetSetting(AutoInstallSetting, ""), "true", StringComparison.OrdinalIgnoreCase);
            set => SettingsService.SaveSetting(AutoInstallSetting, value ? "true" : "false");
        }

        /// <summary>Faux tant que l'utilisateur n'a pas répondu à la question (ni coché/décoché le réglage).</summary>
        public static bool AutoInstallDecided =>
            !string.IsNullOrWhiteSpace(SettingsService.GetSetting(AutoInstallSetting, ""));

        /// <summary>Phrase d'état affichée dans Paramètres › Mises à jour.</summary>
        public static string DescribeAutoInstall() =>
            !AutoInstallDecided
                ? "État : pas encore choisi. La question vous sera posée à la prochaine nouvelle version."
                : AutoInstallEnabled
                    ? "État : ✅ activée. Les nouvelles versions s'installent toutes seules à la fermeture du logiciel."
                    : "État : désactivée. Chaque nouvelle version vous sera proposée au démarrage.";

        /// <summary>Faux si l'utilisateur a demandé à ne plus être invité à saisir la clé d'accès sur ce poste.</summary>
        public static bool TokenPromptEnabled
        {
            get => !string.Equals(SettingsService.GetSetting(TokenPromptDisabledSetting, "false"), "true", StringComparison.OrdinalIgnoreCase);
            set => SettingsService.SaveSetting(TokenPromptDisabledSetting, value ? "false" : "true");
        }

        /// <summary>Installateur vérifié en attente d'installation à la fermeture.</summary>
        public static string? PendingInstallerPath { get; private set; }
        public static string? PendingSha256 { get; private set; }
        public static Version? PendingVersion { get; private set; }

        /// <summary>Vrai dès qu'un installateur a été lancé (évite un second lancement à la fermeture).</summary>
        public static bool InstallerLaunched { get; private set; }

        public static bool HasPendingUpdate => PendingInstallerPath != null && File.Exists(PendingInstallerPath);

        /// <summary>Télécharge et vérifie l'installateur, puis le garde en attente. Renvoie faux en cas d'échec (journalisé).</summary>
        public static async Task<bool> PrepareAsync(GitHubRelease release, GitHubUpdateService? service = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var svc = service ?? new GitHubUpdateService();
                string path = await svc.DownloadVerifiedInstallerAsync(release, null, cancellationToken);
                SetPending(path, GitHubUpdateService.ComputeSha256(path), release.Version);
                AppLogger.Info(nameof(AutoUpdateCoordinator), $"Version {release.Version} prête : installation à la fermeture du logiciel.");
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLogger.Warn(nameof(AutoUpdateCoordinator), $"Préparation de la mise à jour {release.Version} impossible (nouvel essai au prochain démarrage).", ex);
                return false;
            }
        }

        /// <summary>Enregistre un installateur déjà vérifié comme mise à jour en attente.</summary>
        public static void SetPending(string path, string sha256, Version? version)
        {
            lock (Gate)
            {
                PendingInstallerPath = path;
                PendingSha256 = sha256;
                PendingVersion = version;
            }
        }

        /// <summary>Signale qu'un installateur a été lancé par un autre chemin (installation immédiate).</summary>
        public static void MarkInstallerLaunched()
        {
            lock (Gate) InstallerLaunched = true;
        }

        /// <summary>
        /// À appeler à la fermeture, après libération du mutex d'instance unique : lance l'installateur en attente
        /// s'il est toujours intact (empreinte recontrôlée, le fichier ayant pu être modifié dans %TEMP%).
        /// </summary>
        public static bool InstallPendingOnExit(Func<string, string, bool>? launcher = null)
        {
            string? path, expected;
            lock (Gate)
            {
                if (InstallerLaunched || PendingInstallerPath == null || PendingSha256 == null) return false;
                path = PendingInstallerPath;
                expected = PendingSha256;
                InstallerLaunched = true;
            }

            try
            {
                if (!File.Exists(path))
                {
                    AppLogger.Warn(nameof(AutoUpdateCoordinator), "Installateur en attente introuvable : mise à jour reportée au prochain démarrage.");
                    return false;
                }
                if (!string.Equals(GitHubUpdateService.ComputeSha256(path), expected, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                    AppLogger.Warn(nameof(AutoUpdateCoordinator), "Installateur en attente modifié depuis sa vérification : supprimé, mise à jour annulée.");
                    return false;
                }

                bool started = (launcher ?? StartInstaller)(path, InstallOnExitArguments);
                AppLogger.Info(nameof(AutoUpdateCoordinator), started
                    ? $"Installation de la version {PendingVersion} lancée à la fermeture."
                    : "Le lancement de l'installateur a échoué.");
                return started;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AutoUpdateCoordinator), "Installation à la fermeture impossible.", ex);
                return false;
            }
        }

        private static bool StartInstaller(string path, string arguments) =>
            Process.Start(new ProcessStartInfo { FileName = path, Arguments = arguments, UseShellExecute = true }) != null;

        /// <summary>Remise à zéro (tests).</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                PendingInstallerPath = null;
                PendingSha256 = null;
                PendingVersion = null;
                InstallerLaunched = false;
            }
        }
    }
}

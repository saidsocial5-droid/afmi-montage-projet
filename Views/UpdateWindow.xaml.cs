using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Updates;

namespace AFMI_Montage_projet_2.Views
{
    /// <summary>Proposition de mise à jour depuis GitHub : notes de version, téléchargement vérifié, installation (ADR 26).</summary>
    public partial class UpdateWindow : Window
    {
        private readonly UpdateCheckResult _check;
        private readonly GitHubUpdateService _service = new();
        private CancellationTokenSource? _cts;

        public UpdateWindow(UpdateCheckResult check)
        {
            InitializeComponent();
            _check = check;
            var release = check.Release!;
            TxtTitle.Text = $"AFMI Montage Projet {release.Version} est disponible";
            TxtVersions.Text = $"Version installée : {check.CurrentVersion} — publiée le {release.PublishedAt?.ToLocalTime():dd/MM/yyyy}" +
                               (release.Installer != null ? $" — {release.Installer.Size / (1024.0 * 1024):0} Mo" : "");
            TxtNotes.Text = string.IsNullOrWhiteSpace(release.Notes) ? "(pas de notes de version)" : release.Notes;
            BtnGitHub.Visibility = string.IsNullOrEmpty(release.HtmlUrl) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Affiche la proposition de mise à jour (appelée au démarrage ou depuis les paramètres).</summary>
        public static Task OfferAsync(UpdateCheckResult check, Window? owner)
        {
            var window = new UpdateWindow(check);
            if (owner != null && owner.IsLoaded) window.Owner = owner;
            window.ShowDialog();
            return Task.CompletedTask;
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            BtnInstall.IsEnabled = false;
            BtnLater.Content = "Annuler";
            Progress.Visibility = Visibility.Visible;
            TxtStatus.Text = "Téléchargement de l'installateur…";
            _cts = new CancellationTokenSource();
            try
            {
                var progress = new Progress<double>(p =>
                {
                    Progress.Value = p;
                    TxtStatus.Text = $"Téléchargement… {p:P0}";
                });
                string path = await _service.DownloadVerifiedInstallerAsync(_check.Release!, progress, _cts.Token);
                TxtStatus.Text = "Installateur vérifié (empreinte SHA-256 conforme). L'application va se fermer pour la mise à jour, puis redémarrer.";
                await Task.Delay(1200);
                GitHubUpdateService.LaunchInstallerAndExit(path);
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Téléchargement annulé.";
                ResetButtons();
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(UpdateWindow), "Mise à jour impossible.", ex);
                TxtStatus.Text = $"Mise à jour impossible : {ex.Message}";
                ResetButtons();
            }
        }

        private void ResetButtons()
        {
            BtnInstall.IsEnabled = true;
            BtnLater.Content = "Plus tard";
            Progress.Visibility = Visibility.Collapsed;
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !BtnInstall.IsEnabled)
            {
                _cts.Cancel();
                return;
            }
            Close();
        }

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
        {
            string url = _check.Release?.HtmlUrl ?? string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                AppLogger.Warn(nameof(UpdateWindow), "Ouverture de la page GitHub impossible.", ex);
            }
        }
    }
}

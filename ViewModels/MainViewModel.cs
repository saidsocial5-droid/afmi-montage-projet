using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        [ObservableProperty]
        private Project? _selectedProject;

        public bool IsProjectLoaded => SelectedProject != null;

        public MainViewModel()
        {
            NavigateToProjectList();
            _ = CheckForUpdatesAsync();
            _ = RestoreTeamSessionAsync();
        }

        /// <summary>Reconnexion silencieuse à l'espace d'équipe si l'utilisateur s'y était connecté (ADR 23).</summary>
        private static async Task RestoreTeamSessionAsync()
        {
            try
            {
                await Services.Collab.AuthService.Instance.TryRestoreSessionAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(MainViewModel), "Reconnexion automatique à l'espace d'équipe impossible.", ex);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            // 1. Releases GitHub (canal principal, ADR 26 ; mise à jour automatique, ADR 29)
            try
            {
                var github = new Services.Updates.GitHubUpdateService();
                var check = await github.CheckAsync();

                // Dépôt privé sans clé valide sur ce poste : inviter à l'activer, puis enchaîner sur la vérification
                if (check.NeedsAccessToken)
                {
                    AppLogger.Info(nameof(MainViewModel), check.Message);
                    var validated = await Views.GitHubTokenWindow.PromptAsync(Application.Current?.MainWindow);
                    if (validated == null) return;
                    check = validated;
                }

                if (check.UpdateAvailable && check.Release != null)
                {
                    await ApplyGitHubUpdateAsync(check, github);
                    return;
                }
                if (check.IsConfigured) return; // dépôt configuré et application à jour : pas de canal secondaire
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(MainViewModel), "Vérification des mises à jour GitHub impossible.", ex);
            }

            // 2. Espace Google Drive de l'équipe (canal historique, publications d'administrateur uniquement)
            try
            {
                var workspaceId = SettingsService.GetSetting("Google_WorkspaceId", "");
                if (string.IsNullOrEmpty(workspaceId)) return;

                var driveService = SessionManager.CurrentDriveService;
                if (driveService == null) return;

                var updater = new AutoUpdaterService(driveService);
                var updateInfo = await updater.CheckForUpdatesAsync(workspaceId);

                if (updateInfo == null) return;

                // Appelé depuis le constructeur (thread UI) : la continuation reprend sur le thread UI,
                // inutile de passer par Dispatcher.Invoke (qui transformait la lambda en async void).
                var result = MessageBox.Show(
                    $"Une nouvelle mise à jour (v{updateInfo.Version}) est disponible !\n\nVersion installée : v{AutoUpdaterService.CurrentVersion}\n\nNouveautés:\n{updateInfo.ReleaseNotes}\n\nVoulez-vous la télécharger et l'installer maintenant ?",
                    "Mise à jour disponible",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result != MessageBoxResult.Yes) return;

                var win = new Window
                {
                    Title = "Mise à jour en cours...",
                    Width = 300,
                    Height = 100,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new System.Windows.Controls.TextBlock { Text = "Téléchargement en cours, veuillez patienter...", Margin = new Thickness(20) }
                };
                win.Show();
                try
                {
                    await updater.DownloadAndApplyUpdateAsync(updateInfo, (progress) => { });
                }
                finally
                {
                    win.Close();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(MainViewModel), "Erreur lors de la vérification automatique des mises à jour.", ex);
            }
        }

        /// <summary>
        /// Mise à jour automatique activée : téléchargement et vérification en arrière-plan, installation à la fermeture,
        /// avec la possibilité d'installer tout de suite. Sinon (ou si le téléchargement échoue) : fenêtre de proposition.
        /// </summary>
        public static async Task ApplyGitHubUpdateAsync(Services.Updates.UpdateCheckResult check, Services.Updates.GitHubUpdateService github)
        {
            var release = check.Release!;

            // Première nouvelle version détectée sur ce poste : demander si la mise à jour doit devenir automatique (ADR 30)
            if (!Services.Updates.AutoUpdateCoordinator.AutoInstallDecided)
            {
                var mainWindow = Application.Current?.MainWindow;
                string question = $"Une nouvelle version d'AFMI Montage Projet est disponible : {release.Version}.\n\n" +
                                  "Voulez-vous activer la mise à jour automatique ?\n\n" +
                                  "• Oui : cette version et les suivantes sont téléchargées et vérifiées en arrière-plan, puis installées " +
                                  "toutes seules à la fermeture du logiciel (vous pourrez aussi les installer tout de suite).\n" +
                                  "• Non : chaque nouvelle version vous sera proposée au démarrage.\n\n" +
                                  "Ce choix se modifie à tout moment dans Paramètres › Mises à jour.";
                var choice = mainWindow != null && mainWindow.IsLoaded
                    ? MessageBox.Show(mainWindow, question, "Mise à jour automatique", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes)
                    : MessageBox.Show(question, "Mise à jour automatique", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                Services.Updates.AutoUpdateCoordinator.AutoInstallEnabled = choice == MessageBoxResult.Yes;
                AppLogger.Info(nameof(MainViewModel), $"Mise à jour automatique {(choice == MessageBoxResult.Yes ? "activée" : "refusée")} par l'utilisateur.");
            }

            if (!Services.Updates.AutoUpdateCoordinator.AutoInstallEnabled ||
                !await Services.Updates.AutoUpdateCoordinator.PrepareAsync(release, github))
            {
                await Views.UpdateWindow.OfferAsync(check, Application.Current?.MainWindow);
                return;
            }

            var owner = Application.Current?.MainWindow;
            string text = $"La version {release.Version} d'AFMI Montage Projet a été téléchargée et vérifiée.\n\n" +
                          "Elle s'installera automatiquement à la fermeture du logiciel : vos projets ne sont pas touchés.\n\n" +
                          "Voulez-vous l'installer maintenant ? (le logiciel se fermera puis redémarrera)";
            var answer = owner != null && owner.IsLoaded
                ? MessageBox.Show(owner, text, "Mise à jour prête", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No)
                : MessageBox.Show(text, "Mise à jour prête", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
            if (answer == MessageBoxResult.Yes && Services.Updates.AutoUpdateCoordinator.PendingInstallerPath is string path)
                Services.Updates.GitHubUpdateService.LaunchInstallerAndExit(path);
        }

        [RelayCommand]
        public void NavigateToProjectList()
        {
            CurrentViewModel = new ProjectListViewModel(this);
            OnPropertyChanged(nameof(IsProjectLoaded));
        }

        public void LoadProject(Project project)
        {
            SelectedProject = project;
            OnPropertyChanged(nameof(IsProjectLoaded));
            CurrentViewModel = new ProjectWorkspaceViewModel(project, this);
        }

        public void CloseProject()
        {
            SelectedProject = null;
            OnPropertyChanged(nameof(IsProjectLoaded));
            NavigateToProjectList();
        }
    }
}


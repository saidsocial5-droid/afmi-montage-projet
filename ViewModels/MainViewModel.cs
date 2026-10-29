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
            // 1. Releases GitHub (canal principal, ADR 26)
            try
            {
                var github = new Services.Updates.GitHubUpdateService();
                var check = await github.CheckAsync();
                if (check.UpdateAvailable && check.Release != null)
                {
                    await Views.UpdateWindow.OfferAsync(check, Application.Current?.MainWindow);
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


using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ProjectListViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private Project? _selectedProjectInList;

        [ObservableProperty]
        private string _newProjectTitle = string.Empty;

        public bool IsSystemAdmin => Services.SessionManager.CurrentUser?.IsSystemAdmin == true;
        
        public bool IsAdmin => AFMI_Montage_projet_2.Security.SecurityConstants.IsSuperAdmin(Services.SessionManager.CurrentUser?.Email);
        
        public bool IsProjectRadarEnabled => Services.Radar.ProjectRadarConfigService.IsEnabled;

        [RelayCommand]
        private void OpenProjectRadar()
        {
            var win = new Views.Radar.ProjectRadarWindow((proj) => _mainViewModel.LoadProject(proj));
            win.Owner = Application.Current?.MainWindow;
            win.Closed += (s, e) => LoadProjectsFromDb();
            win.Show();
        }

        [RelayCommand]
        private void OpenTeamWorkspace()
        {
            var win = new Views.TeamWorkspaceWindow(() => Application.Current?.Dispatcher.Invoke(LoadProjectsFromDb));
            win.Owner = Application.Current?.MainWindow;
            win.Closed += (s, e) => LoadProjectsFromDb();
            win.Show();
        }

        [RelayCommand]
        private void OpenOrganizationProfile()
        {
            var win = new Views.OrganizationProfileWindow();
            win.Owner = Application.Current?.MainWindow;
            win.ShowDialog();
        }

        public ProjectListViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            LoadProjectsFromDb();
        }

        private void LoadProjectsFromDb()
        {
            using var db = new AppDbContext();
            db.SeedDefaultData(); // S'assurer que les tables et catégories par défaut existent
            
            var list = db.Projects
                .OrderByDescending(p => p.UpdatedAt)
                .ToList();
                
            Projects = new ObservableCollection<Project>(list);
        }

        [RelayCommand]
        private void OpenMyAccount()
        {
            var win = new Views.UserAccountWindow();
            win.ShowDialog();
        }

        [RelayCommand]
        private void OpenUserManagement()
        {
            if (!IsAdmin)
            {
                AFMI_Montage_projet_2.Security.AuditLogger.Log("Accès refusé", "Tentative d'accès à la gestion des utilisateurs.");
                System.Windows.MessageBox.Show("Seul le Super Admin peut gérer les membres.", "Accès refusé", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            var win = new Views.UserManagementWindow();
            win.ShowDialog();
        }

        [RelayCommand]
        private void Logout()
        {
            Services.SessionManager.Logout();
            var login = new Views.LoginWindow();
            login.Show();
            // Close main window
            foreach (var window in System.Windows.Application.Current.Windows)
            {
                if (window is System.Windows.Window w && w != login)
                    w.Close();
            }
        }

        [RelayCommand]
        public void OpenInnovationLab()
        {
            var win = new AFMI_Montage_projet_2.Views.IdeationInnovationWindow();
            win.ShowDialog();
            LoadProjectsFromDb(); // Recharger au cas où le projet a été modifié
        }

        [RelayCommand]
        public void CreateProject()
        {
            if (string.IsNullOrWhiteSpace(NewProjectTitle))
                return;

            using var db = new AppDbContext();
            var newProject = new Project
            {
                Title = NewProjectTitle.Trim(),
                LeadOrganization = "Association Forêt Modèle Ifrane (AFMI)",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Projects.Add(newProject);
            db.SaveChanges();

            Projects.Insert(0, newProject);
            NewProjectTitle = string.Empty;
            
            // Charger immédiatement le projet créé
            OpenProject(newProject);
        }

        [RelayCommand]
        public void OpenProject(Project? project)
        {
            var target = project ?? SelectedProjectInList;
            if (target != null)
            {
                _mainViewModel.LoadProject(target);
            }
        }

        [RelayCommand]
        public void DeleteProject(Project? project)
        {
            var target = project ?? SelectedProjectInList;
            if (target != null)
            {
                using var db = new AppDbContext();
                db.Projects.Remove(target);
                db.SaveChanges();
                
                Projects.Remove(target);
                if (SelectedProjectInList == target)
                {
                    SelectedProjectInList = null;
                }
            }
        }

        [ObservableProperty]
        private bool _isSyncing = false;

        [RelayCommand]
        private async Task SyncToCloudAsync()
        {
            var driveService = Services.SessionManager.CurrentDriveService;
            if (driveService == null)
            {
                System.Windows.MessageBox.Show("Non connecté à Google Drive. Veuillez vérifier vos paramètres.", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var workspaceId = Services.SettingsService.GetSetting("Google_WorkspaceId", "");
            if (string.IsNullOrEmpty(workspaceId))
            {
                try
                {
                    var manager = new Services.Drive.GoogleDriveManager(driveService);
                    workspaceId = await manager.InitializeWorkspaceAsync();
                    Services.SettingsService.SaveSetting("Google_WorkspaceId", workspaceId);
                }
                catch { }

                if (string.IsNullOrEmpty(workspaceId))
                {
                    System.Windows.MessageBox.Show("Veuillez d'abord configurer l'espace de travail Cloud dans les paramètres.", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            IsSyncing = true;
            var engine = new Services.Drive.SyncEngineService(driveService, workspaceId);
            var result = await engine.ExportProjectsToCloudAsync();
            var imported = await engine.ImportContributionsFromCloudAsync();
            IsSyncing = false;

            if (result)
            {
                var msg = "Synchronisation vers le Cloud réussie.";
                if (imported > 0) msg += $"\n{imported} contribution(s) importée(s).";
                System.Windows.MessageBox.Show(msg, "Succès", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Erreur lors de la synchronisation.", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
                }
    }
}






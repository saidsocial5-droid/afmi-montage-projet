using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.ViewModels.Strategic;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ProjectWorkspaceViewModel : ObservableObject
    {
        private readonly MainViewModel _parentMainViewModel;
        
        [ObservableProperty]
        private Project _project;

        [ObservableProperty]
        private ObservableObject? _currentSubViewModel;

        [ObservableProperty]
        private string _activeTabName = "StrategicPlanning";

        public ProjectStepperViewModel Stepper { get; }

        [ObservableProperty]
        private bool _isAiPanelVisible = false;

        public AiAssistantPanelViewModel AiAssistantPanel { get; } = new();

        [ObservableProperty]
        private bool _isNotificationsOpen;

        [ObservableProperty]
        private ObservableCollection<Notification> _notifications = new();

        public bool HasUnreadNotifications => Notifications.Any(n => !n.IsRead);
        public int UnreadNotificationsCount => Notifications.Count(n => !n.IsRead);

        public bool CanEdit => SessionManager.CurrentUser?.Role != "Reader";
        public bool IsManagerOrAdmin => SessionManager.CurrentUser?.Role == "SuperAdmin" || SessionManager.CurrentUser?.Role == "Manager";
        public bool IsSuperAdmin => SessionManager.CurrentUser?.Role == "SuperAdmin";
        public bool IsProjectRadarEnabled => AFMI_Montage_projet_2.Services.Radar.ProjectRadarConfigService.IsEnabled;

        public ProjectWorkspaceViewModel(Project project, MainViewModel parentMainViewModel)
        {
            _project = project;
            _parentMainViewModel = parentMainViewModel;
            
            SessionManager.CurrentProjectId = project.Id;
            SessionManager.CurrentProject = project;

            Stepper = new ProjectStepperViewModel(project.Id, NavigateByTabName);

            // Charger la sous-vue par défaut (Tendance RBM: Planification Stratégique d'abord)
            NavigateToStrategicPlanning();

            LoadNotifications();
        }

        public void NavigateByTabName(string tabKey)
        {
            switch (tabKey)
            {
                case "StrategicPlanning":
                    NavigateToStrategicPlanning();
                    break;
                case "Cadrage":
                    NavigateToCadrage();
                    break;
                case "ArbreProbleme":
                    NavigateToArbreProbleme();
                    break;
                case "CadreLogique":
                    NavigateToCadreLogique();
                    break;
                case "Chronogramme":
                    NavigateToChronogramme();
                    break;
                case "Budget":
                    NavigateToBudget();
                    break;
                case "Canvas":
                case "FormulaireDynamique":
                    NavigateToFormulaireDynamique();
                    break;
                case "Collaboration":
                    NavigateToCollaboration();
                    break;
                case "Export":
                    NavigateToExport();
                    break;
                case "ProjectRadar":
                    NavigateToProjectRadar();
                    break;
                case "OrganizationProfile":
                    NavigateToOrganizationProfile();
                    break;
                default:
                    NavigateToStrategicPlanning();
                    break;
            }
        }

        private void LoadNotifications()
        {
            if (SessionManager.CurrentUser == null) return;
            try
            {
                using var db = new AppDbContext();
                var userNotifs = db.Notifications
                    .Where(n => n.UserId == SessionManager.CurrentUser.Id)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20) // Prendre les 20 dernières
                    .ToList();
                
                Notifications = new ObservableCollection<Notification>(userNotifs);
                OnPropertyChanged(nameof(HasUnreadNotifications));
                OnPropertyChanged(nameof(UnreadNotificationsCount));
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(ProjectWorkspaceViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        [RelayCommand]
        private void ShowNotifications()
        {
            LoadNotifications();
            IsNotificationsOpen = true;
        }

        [RelayCommand]
        private void MarkAllNotificationsRead()
        {
            if (SessionManager.CurrentUser == null) return;
            try
            {
                using var db = new AppDbContext();
                var unread = db.Notifications.Where(n => n.UserId == SessionManager.CurrentUser.Id && !n.IsRead).ToList();
                foreach (var n in unread) n.IsRead = true;
                db.SaveChanges();
                LoadNotifications();
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(ProjectWorkspaceViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        [RelayCommand]
        public void ToggleAiPanel()
        {
            IsAiPanelVisible = !IsAiPanelVisible;
            if (IsAiPanelVisible)
            {
                AiAssistantPanel.UpdateEngineIndicator();
            }
        }

        [RelayCommand]
        public void NavigateToStrategicPlanning()
        {
            ActiveTabName = "StrategicPlanning";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new StrategicPlanningViewModel(Project.Id, NavigateByTabName);
        }

        [RelayCommand]
        public void NavigateToCadrage()
        {
            ActiveTabName = "Cadrage";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new CadrageViewModel(Project, _parentMainViewModel);
        }

        [RelayCommand]
        public void NavigateToCadreLogique()
        {
            ActiveTabName = "CadreLogique";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new CadreLogiqueViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToChronogramme()
        {
            ActiveTabName = "Chronogramme";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new ChronogrammeViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToBudget()
        {
            ActiveTabName = "Budget";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new BudgetViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToExport()
        {
            ActiveTabName = "Export";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new ExportViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToSettings()
        {
            ActiveTabName = "Settings";
            CurrentSubViewModel = new SettingsViewModel();
        }

        [RelayCommand]
        public void NavigateToAudit()
        {
            ActiveTabName = "Audit";
            CurrentSubViewModel = new AuditViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToActivityReports()
        {
            ActiveTabName = "ActivityReports";
            CurrentSubViewModel = new ActivityReportsViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToDashboard()
        {
            ActiveTabName = "Dashboard";
            CurrentSubViewModel = new DashboardViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToIndicateurs()
        {
            ActiveTabName = "Indicateurs";
            CurrentSubViewModel = new IndicateursViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToFormulaireDynamique()
        {
            ActiveTabName = "FormulaireDynamique";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new FormulaireDynamiqueViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToRisques()
        {
            ActiveTabName = "Risques";
            CurrentSubViewModel = new RisquesViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToSeMilestones()
        {
            ActiveTabName = "SeMilestones";
            CurrentSubViewModel = new SeMilestonesViewModel(Project);
        }

        [RelayCommand]
        public void NavigateToCollaboration()
        {
            ActiveTabName = "Collaboration";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new CollaborationViewModel(Project.Id);
        }

        [RelayCommand]
        public void NavigateToArbreProbleme()
        {
            ActiveTabName = "ArbreProbleme";
            Stepper.SetActiveTab(ActiveTabName);
            _ = Stepper.RefreshStatusesAsync();
            CurrentSubViewModel = new ArbreProblemeViewModel(Project.Id);
        }

        [RelayCommand]
        public void NavigateToAnalyseDocuments()
        {
            ActiveTabName = "AnalyseDocuments";
            CurrentSubViewModel = new AnalyseDocumentsViewModel(Project.Id);
        }

        [RelayCommand]
        public void NavigateToApprovalInbox()
        {
            CurrentSubViewModel = new ApprovalInboxViewModel();
            ActiveTabName = "ApprovalInbox";
        }
        [RelayCommand]
        public void NavigateToCloudSync()
        {
            ActiveTabName = "CloudSync";
            CurrentSubViewModel = new CloudSyncViewModel();
        }

        [RelayCommand]
        public void NavigateToProjectRadar()
        {
            ActiveTabName = "ProjectRadar";
            CurrentSubViewModel = new AFMI_Montage_projet_2.ViewModels.Radar.ProjectRadarViewModel(
                onOpenProjectRequested: (proj) => _parentMainViewModel.LoadProject(proj));
        }

        [RelayCommand]
        public void NavigateToOrganizationProfile()
        {
            ActiveTabName = "OrganizationProfile";
            CurrentSubViewModel = new OrganizationProfileViewModel(Project);
        }

        [RelayCommand]
        public void CloseWorkspace()
        {
            _parentMainViewModel.CloseProject();
        }
    }
}









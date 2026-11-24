using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ActivityReportsViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<Activity> _activities = new();

        [ObservableProperty]
        private Activity? _selectedActivity;

        [ObservableProperty]
        private ObservableCollection<ActivityReport> _reports = new();

        [ObservableProperty]
        private ActivityReport? _selectedReport;

        // Form fields
        [ObservableProperty]
        private string _newNarrative = string.Empty;

        [ObservableProperty]
        private decimal _newExecutionRate = 0;

        public ActivityReportsViewModel(Project project)
        {
            _project = project;
            LoadActivities();
        }

        private void LoadActivities()
        {
            using var db = new AppDbContext();
            var acts = db.Activities
                .Include(a => a.Result)
                .ThenInclude(r => r!.Objective)
                .Where(a => a.Result!.Objective!.ProjectId == _project.Id)
                .OrderBy(a => a.Code)
                .ToList();
            
            Activities = new ObservableCollection<Activity>(acts);
            if (Activities.Any())
            {
                SelectedActivity = Activities.First();
            }
        }

        partial void OnSelectedActivityChanged(Activity? value)
        {
            LoadReports();
        }

        private void LoadReports()
        {
            if (SelectedActivity == null)
            {
                Reports.Clear();
                return;
            }

            using var db = new AppDbContext();
            var actReports = db.ActivityReports
                .Include(ar => ar.ReportedByUser)
                .Where(ar => ar.ActivityId == SelectedActivity.Id)
                .OrderByDescending(ar => ar.ReportDate)
                .ToList();

            Reports = new ObservableCollection<ActivityReport>(actReports);
        }

        [RelayCommand]
        private void SubmitReport()
        {
            if (SelectedActivity == null) return;
            if (string.IsNullOrWhiteSpace(NewNarrative))
            {
                MessageBox.Show("Veuillez saisir un compte rendu.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var currentUser = SessionManager.CurrentUser;
            bool isAdmin = false;
            using (var checkDb = new AppDbContext())
            {
                // Sans utilisateur connecté, personne n'est administrateur (évitait une NullReferenceException).
                int? currentUserId = currentUser?.Id;
                isAdmin = currentUserId != null &&
                          (checkDb.ProjectRoles.Any(r => r.ProjectId == _project.Id && r.UserId == currentUserId && r.RoleName == "Admin")
                           || (currentUser?.IsSystemAdmin ?? false));
            }

            var report = new ActivityReport
            {
                ActivityId = SelectedActivity.Id,
                ReportedByUserId = currentUser?.Id,
                ReportDate = DateTime.UtcNow,
                Narrative = NewNarrative.Trim(),
                ExecutionRate = NewExecutionRate,
                Status = isAdmin ? "Approved" : "Pending"
            };

            using var db = new AppDbContext();
            db.ActivityReports.Add(report);
            db.SaveChanges();

            if (!isAdmin)
            {
                NotificationService.NotifyAdminsOfProject(_project.Id, "Nouveau Compte Rendu soumis",
                    $"Un compte rendu d'activité a été soumis par @{currentUser?.Username} pour l'activité {SelectedActivity.Code}.");
            }

            // Reset form
            NewNarrative = string.Empty;
            NewExecutionRate = 0;

            LoadReports();
            MessageBox.Show("Compte rendu soumis avec succès !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ApproveReport(ActivityReport report)
        {
            if (report == null) return;

            using var db = new AppDbContext();
            var dbReport = db.ActivityReports.Find(report.Id);
            if (dbReport != null)
            {
                dbReport.Status = "Approved";
                db.SaveChanges();
                LoadReports();
            }
        }
        
        [RelayCommand]
        private void DeleteReport(ActivityReport report)
        {
            if (report == null) return;

            if (MessageBox.Show("Voulez-vous vraiment supprimer ce compte rendu ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                using var db = new AppDbContext();
                db.ActivityReports.Remove(report);
                db.SaveChanges();
                LoadReports();
            }
        }
    }
}


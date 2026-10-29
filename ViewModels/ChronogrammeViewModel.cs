using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ChronogrammeViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<ActivityRowViewModel> _activities = new();

        [ObservableProperty]
        private ObservableCollection<CalendarEventViewModel> _calendarEvents = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _timelineHeaders = new();

        public ChronogrammeViewModel(Project project)
        {
            _project = project;
            LoadTimeline();
        }

        private void LoadTimeline()
        {
            using var db = new AppDbContext();
            
            // Récupérer toutes les activités liées au projet
            var dbActivities = db.Activities
                .Include(a => a.Result)
                    .ThenInclude(r => r!.Objective)
                .Where(a => a.Result!.Objective!.ProjectId == _project.Id)
                .OrderBy(a => a.Code)
                .ToList();

            // Générer les en-têtes de trimestres en fonction de la durée du projet
            TimelineHeaders.Clear();
            int durationYears = 1;
            if (_project.StartDate.HasValue && _project.EndDate.HasValue)
            {
                int months = ((_project.EndDate.Value.Year - _project.StartDate.Value.Year) * 12) + _project.EndDate.Value.Month - _project.StartDate.Value.Month;
                durationYears = System.Math.Max(1, (int)System.Math.Ceiling((double)months / 12));
            }

            for (int y = 1; y <= durationYears; y++)
            {
                TimelineHeaders.Add($"An {y} - T1");
                TimelineHeaders.Add($"An {y} - T2");
                TimelineHeaders.Add($"An {y} - T3");
                TimelineHeaders.Add($"An {y} - T4");
            }

            // Instancier les lignes de ViewModel
            var rows = dbActivities.Select(a => new ActivityRowViewModel(a, _project.StartDate, TimelineHeaders.Count, this)).ToList();
            Activities = new ObservableCollection<ActivityRowViewModel>(rows);

            // Charger les événements de calendrier unifiés
            var calendarList = new System.Collections.Generic.List<CalendarEventViewModel>();

            // Ajouter les activités ayant des dates définies
            foreach (var act in dbActivities)
            {
                if (act.StartDate.HasValue)
                {
                    calendarList.Add(new CalendarEventViewModel
                    {
                        DateString = act.StartDate.Value.ToString("dd/MM/yyyy"),
                        Title = $"Début Activité : [{act.Code}] {act.Description}",
                        Type = "Activité",
                        ColorBrush = "#16A34A", // Vert
                        Responsible = "Équipe Projet",
                        Status = act.EndDate.HasValue ? $"Jusqu'au {act.EndDate.Value:dd/MM/yyyy}" : "Ponctuelle"
                    });
                }
            }

            // Ajouter les Jalons S&E
            var milestones = db.SeMilestones.Where(m => m.ProjectId == _project.Id).OrderBy(m => m.TargetDate).ToList();
            foreach (var m in milestones)
            {
                calendarList.Add(new CalendarEventViewModel
                {
                    DateString = m.TargetDate.ToString("dd/MM/yyyy"),
                    Title = $"Jalon S&E : {m.Title} ({m.ExpectedDeliverable})",
                    Type = "Suivi & Évaluation",
                    ColorBrush = "#8B5CF6", // Violet
                    Responsible = m.ResponsibleRole,
                    Status = m.Status
                });
            }

            CalendarEvents = new ObservableCollection<CalendarEventViewModel>(calendarList);
        }

        [RelayCommand]
        public void SaveTimeline()
        {
            try
            {
                using var db = new AppDbContext();
                foreach (var row in Activities)
                {
                    var dbActivity = db.Activities.FirstOrDefault(a => a.Id == row.ActivityId);
                    if (dbActivity != null)
                    {
                        dbActivity.StartDate = row.StartDate;
                        dbActivity.EndDate = row.EndDate;
                    }
                }
                db.SaveChanges();
                StatusMessage = "Planning enregistré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'enregistrement : {ex.Message}";
            }
        }
    }

    public partial class ActivityRowViewModel : ObservableObject
    {
        private readonly ChronogrammeViewModel _parent;
        private readonly DateTime? _projectStart;
        private readonly int _totalQuarters;

        public int ActivityId { get; }
        public string Code { get; }
        public string Description { get; }

        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private DateTime? _endDate;

        [ObservableProperty]
        private ObservableCollection<bool> _quarterSelections = new();

        public ActivityRowViewModel(Activity activity, DateTime? projectStart, int totalQuarters, ChronogrammeViewModel parent)
        {
            _parent = parent;
            _projectStart = projectStart;
            _totalQuarters = totalQuarters;

            ActivityId = activity.Id;
            Code = activity.Code;
            Description = activity.Description;
            _startDate = activity.StartDate;
            _endDate = activity.EndDate;

            CalculateQuarterSelections();
        }

        // Déclencher le recalcul lorsque les dates changent
        partial void OnStartDateChanged(DateTime? value)
        {
            CalculateQuarterSelections();
        }

        partial void OnEndDateChanged(DateTime? value)
        {
            CalculateQuarterSelections();
        }

        private void CalculateQuarterSelections()
        {
            QuarterSelections.Clear();
            for (int i = 0; i < _totalQuarters; i++)
            {
                QuarterSelections.Add(false);
            }

            if (!StartDate.HasValue || !EndDate.HasValue || !_projectStart.HasValue)
                return;

            DateTime projectStart = _projectStart.Value;
            
            // Calculer pour chaque trimestre s'il intersecte la plage de l'activité
            for (int q = 0; q < _totalQuarters; q++)
            {
                // Un trimestre q commence à projectStart + q * 3 mois et dure 3 mois
                DateTime quarterStart = projectStart.AddMonths(q * 3);
                DateTime quarterEnd = quarterStart.AddMonths(3).AddDays(-1);

                // L'activité intersecte le trimestre si (StartDate <= quarterEnd) et (EndDate >= quarterStart)
                if (StartDate.Value.Date <= quarterEnd.Date && EndDate.Value.Date >= quarterStart.Date)
                {
                    QuarterSelections[q] = true;
                }
            }
        }
    }

    public class CalendarEventViewModel
    {
        public string DateString { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Activité" ou "Suivi S&E"
        public string ColorBrush { get; set; } = "#475569";
        public string Responsible { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}


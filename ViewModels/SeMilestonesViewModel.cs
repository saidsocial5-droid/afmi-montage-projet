using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class SeMilestonesViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<SeMilestone> _milestones = new();

        [ObservableProperty]
        private SeMilestone? _selectedMilestone;

        // Propriétés du formulaire
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private DateTime _targetDate = DateTime.Today;

        [ObservableProperty]
        private string _frequency = "Ponctuelle";

        [ObservableProperty]
        private string _responsibleRole = string.Empty;

        [ObservableProperty]
        private string _expectedDeliverable = string.Empty;

        [ObservableProperty]
        private string _status = "A faire";

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<string> Frequencies { get; } = new()
        {
            "Ponctuelle", "Mensuelle", "Trimestrielle", "Semestrielle", "Annuelle"
        };

        public ObservableCollection<string> Statuses { get; } = new()
        {
            "A faire", "En cours", "Réalisé", "En retard"
        };

        public SeMilestonesViewModel(Project project)
        {
            _project = project;
            LoadMilestones();
        }

        private void LoadMilestones()
        {
            try
            {
                using var db = new AppDbContext();
                var list = db.SeMilestones.Where(m => m.ProjectId == _project.Id).OrderBy(m => m.TargetDate).ToList();
                Milestones = new ObservableCollection<SeMilestone>(list);
                StatusMessage = "Plan de Suivi & Évaluation chargé.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors du chargement : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SaveMilestone()
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                StatusMessage = "Veuillez saisir un titre de jalon.";
                return;
            }

            try
            {
                using var db = new AppDbContext();

                if (SelectedMilestone == null)
                {
                    var newMilestone = new SeMilestone
                    {
                        ProjectId = _project.Id,
                        Title = Title.Trim(),
                        Description = Description.Trim(),
                        TargetDate = TargetDate,
                        Frequency = Frequency,
                        ResponsibleRole = ResponsibleRole.Trim(),
                        ExpectedDeliverable = ExpectedDeliverable.Trim(),
                        Status = Status
                    };

                    db.SeMilestones.Add(newMilestone);
                    db.SaveChanges();
                    Milestones.Add(newMilestone);
                    StatusMessage = "Jalon de suivi ajouté avec succès !";
                }
                else
                {
                    var dbMilestone = db.SeMilestones.FirstOrDefault(m => m.Id == SelectedMilestone.Id);
                    if (dbMilestone != null)
                    {
                        dbMilestone.Title = Title.Trim();
                        dbMilestone.Description = Description.Trim();
                        dbMilestone.TargetDate = TargetDate;
                        dbMilestone.Frequency = Frequency;
                        dbMilestone.ResponsibleRole = ResponsibleRole.Trim();
                        dbMilestone.ExpectedDeliverable = ExpectedDeliverable.Trim();
                        dbMilestone.Status = Status;

                        db.SaveChanges();

                        // Mettre à jour l'instance locale
                        SelectedMilestone.Title = Title;
                        SelectedMilestone.Description = Description;
                        SelectedMilestone.TargetDate = TargetDate;
                        SelectedMilestone.Frequency = Frequency;
                        SelectedMilestone.ResponsibleRole = ResponsibleRole;
                        SelectedMilestone.ExpectedDeliverable = ExpectedDeliverable;
                        SelectedMilestone.Status = Status;

                        var index = Milestones.IndexOf(SelectedMilestone);
                        if (index >= 0)
                        {
                            var temp = SelectedMilestone;
                            Milestones.RemoveAt(index);
                            Milestones.Insert(index, temp);
                            SelectedMilestone = temp;
                        }

                        StatusMessage = "Jalon de suivi mis à jour !";
                    }
                }

                ClearForm();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la sauvegarde : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteMilestone()
        {
            if (SelectedMilestone == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbMilestone = db.SeMilestones.FirstOrDefault(m => m.Id == SelectedMilestone.Id);
                if (dbMilestone != null)
                {
                    db.SeMilestones.Remove(dbMilestone);
                    db.SaveChanges();
                    Milestones.Remove(SelectedMilestone);
                    StatusMessage = "Jalon supprimé.";
                }

                ClearForm();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la suppression : {ex.Message}";
            }
        }

        [RelayCommand]
        public void AutoGeneratePlan()
        {
            try
            {
                using var db = new AppDbContext();

                // Supprimer les anciens jalons automatiques existants
                var oldAuto = db.SeMilestones.Where(m => m.ProjectId == _project.Id && m.Description.Contains("[Génération Automatique]"));
                db.SeMilestones.RemoveRange(oldAuto);

                // Générer des jalons trimestriels
                if (!_project.StartDate.HasValue || !_project.EndDate.HasValue)
                {
                    StatusMessage = "Veuillez configurer les dates de début et de fin du projet au préalable.";
                    return;
                }

                var start = _project.StartDate.Value;
                var end = _project.EndDate.Value;
                int monthsDiff = ((end.Year - start.Year) * 12) + end.Month - start.Month;

                // Créer un jalon initial
                var initMilestone = new SeMilestone
                {
                    ProjectId = _project.Id,
                    Title = "Rapport de démarrage",
                    Description = "Lancement officiel et validation du plan de travail. [Génération Automatique]",
                    TargetDate = start.AddDays(15),
                    Frequency = "Ponctuelle",
                    ResponsibleRole = "Chef de projet",
                    ExpectedDeliverable = "Rapport initial d'éligibilité",
                    Status = "A faire"
                };
                db.SeMilestones.Add(initMilestone);

                // Jalons intermédiaires (tous les 3 mois)
                int index = 1;
                for (int m = 3; m < monthsDiff; m += 3)
                {
                    var midMilestone = new SeMilestone
                    {
                        ProjectId = _project.Id,
                        Title = $"Rapport d'avancement Trimestre {index}",
                        Description = $"Suivi de progression et audit technique intermédiaire. [Génération Automatique]",
                        TargetDate = start.AddMonths(m),
                        Frequency = "Trimestrielle",
                        ResponsibleRole = "Responsable S&E",
                        ExpectedDeliverable = "Rapport technique trimestriel",
                        Status = "A faire"
                    };
                    db.SeMilestones.Add(midMilestone);
                    index++;
                }

                // Créer un jalon final
                var endMilestone = new SeMilestone
                {
                    ProjectId = _project.Id,
                    Title = "Rapport d'évaluation finale",
                    Description = "Mesure finale des indicateurs et clôture. [Génération Automatique]",
                    TargetDate = end.AddDays(-5),
                    Frequency = "Ponctuelle",
                    ResponsibleRole = "Évaluateur externe",
                    ExpectedDeliverable = "Rapport d'évaluation final",
                    Status = "A faire"
                };
                db.SeMilestones.Add(endMilestone);

                db.SaveChanges();
                LoadMilestones();
                StatusMessage = "Plan de suivi et évaluation (jalons) généré automatiquement !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la génération automatique : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SelectMilestone(SeMilestone? milestone)
        {
            SelectedMilestone = milestone;
            if (milestone != null)
            {
                Title = milestone.Title;
                Description = milestone.Description;
                TargetDate = milestone.TargetDate;
                Frequency = milestone.Frequency;
                ResponsibleRole = milestone.ResponsibleRole;
                ExpectedDeliverable = milestone.ExpectedDeliverable;
                Status = milestone.Status;
            }
            else
            {
                ClearForm();
            }
        }

        [RelayCommand]
        public void ClearForm()
        {
            SelectedMilestone = null;
            Title = string.Empty;
            Description = string.Empty;
            TargetDate = DateTime.Today;
            Frequency = "Ponctuelle";
            ResponsibleRole = string.Empty;
            ExpectedDeliverable = string.Empty;
            Status = "A faire";
        }
    }
}


using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Strategic;
using AFMI_Montage_projet_2.Security;

namespace AFMI_Montage_projet_2.ViewModels.Strategic
{
    public partial class StepItem : ObservableObject
    {
        public int StepNumber { get; set; }
        public LifecycleStage Stage { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string TabName { get; set; } = string.Empty;

        [ObservableProperty] private StageStatus _status = StageStatus.NotStarted;
        [ObservableProperty] private bool _isActive = false;
        [ObservableProperty] private bool _isPassed = false;
        [ObservableProperty] private bool _isOverridden = false;
        [ObservableProperty] private string _statusLabel = "Non démarré";
        [ObservableProperty] private string _statusBadgeColor = "#94A3B8"; // Slate
    }

    public partial class ProjectStepperViewModel : ObservableObject
    {
        private readonly int _projectId;
        private readonly Action<string> _onNavigate;
        private readonly ReadinessGateService _gateService;

        [ObservableProperty]
        private ObservableCollection<StepItem> _steps = new();

        [ObservableProperty]
        private StepItem? _currentStep;

        public ProjectStepperViewModel(int projectId, Action<string> onNavigate)
        {
            _projectId = projectId;
            _onNavigate = onNavigate ?? throw new ArgumentNullException(nameof(onNavigate));
            _gateService = ReadinessGateService.Instance;

            InitializeSteps();
            _ = RefreshStatusesAsync();
        }

        private void InitializeSteps()
        {
            Steps = new ObservableCollection<StepItem>
            {
                new() { StepNumber = 1, Stage = LifecycleStage.StrategicPlanning, Title = "1. Planification Stratégique", Icon = "🧭", TabName = "StrategicPlanning", IsActive = true },
                new() { StepNumber = 2, Stage = LifecycleStage.Cadrage, Title = "2. Cadrage", Icon = "📋", TabName = "Cadrage" },
                new() { StepNumber = 3, Stage = LifecycleStage.ProblemTree, Title = "3. Arbre à Problèmes", Icon = "🌳", TabName = "ArbreProbleme" },
                new() { StepNumber = 4, Stage = LifecycleStage.CadreLogique, Title = "4. Cadre Logique", Icon = "📊", TabName = "CadreLogique" },
                new() { StepNumber = 5, Stage = LifecycleStage.Chronogramme, Title = "5. Chronogramme", Icon = "📅", TabName = "Chronogramme" },
                new() { StepNumber = 6, Stage = LifecycleStage.Budget, Title = "6. Budget Prévisionnel", Icon = "💰", TabName = "Budget" },
                new() { StepNumber = 7, Stage = LifecycleStage.Canvas, Title = "7. Canevas & Financement", Icon = "📝", TabName = "FormulaireDynamique" },
                new() { StepNumber = 8, Stage = LifecycleStage.Collaboration, Title = "8. Espace Collaboratif", Icon = "🤝", TabName = "Collaboration" },
                new() { StepNumber = 9, Stage = LifecycleStage.Export, Title = "9. Exports & Rapports", Icon = "📤", TabName = "Export" },
            };

            CurrentStep = Steps.First();
        }

        [RelayCommand]
        public async Task RefreshStatusesAsync()
        {
            try
            {
                foreach (var step in Steps)
                {
                    var eval = await _gateService.EvaluateStageAsync(_projectId, step.Stage);
                    step.Status = eval.Status;
                    step.IsPassed = eval.IsPassed;
                    step.IsOverridden = eval.IsOverridden;

                    switch (step.Status)
                    {
                        case StageStatus.Completed:
                            step.StatusLabel = "Terminé";
                            step.StatusBadgeColor = "#10B981"; // Emerald
                            break;
                        case StageStatus.InProgress:
                            step.StatusLabel = "En cours";
                            step.StatusBadgeColor = "#0284C7"; // Sky
                            break;
                        case StageStatus.NeedsReview:
                            step.StatusLabel = "À réviser (Stale)";
                            step.StatusBadgeColor = "#F59E0B"; // Amber
                            break;
                        case StageStatus.NotStarted:
                        default:
                            step.StatusLabel = "Non démarré";
                            step.StatusBadgeColor = "#94A3B8"; // Slate
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(ProjectStepperViewModel), "Actualisation des statuts d'étapes impossible.", ex);
            }
        }

        [RelayCommand]
        public async Task SelectStep(StepItem? targetStep)
        {
            if (targetStep == null) return;

            // Vérification des portes de maturité de TOUTES les étapes précédentes (la première non validée bloque) :
            // auparavant seule l'étape immédiatement précédente était contrôlée, ce qui permettait de sauter des étapes.
            if (targetStep.StepNumber > 1)
            {
                StepItem? prevStep = null;
                GateEvaluationResult? eval = null;
                foreach (var candidate in Steps.Where(s => s.StepNumber < targetStep.StepNumber && s.Stage != LifecycleStage.Collaboration).OrderBy(s => s.StepNumber))
                {
                    var candidateEval = await _gateService.EvaluateStageAsync(_projectId, candidate.Stage);
                    if (!candidateEval.IsPassed && !candidateEval.IsOverridden)
                    {
                        prevStep = candidate;
                        eval = candidateEval;
                        break;
                    }
                }

                if (prevStep != null && eval != null)
                {
                    {
                        string gapsText = string.Join("\n• ", eval.BlockingGaps);
                        var response = MessageBox.Show(
                            $"⚠️ La porte de maturité de l'étape '{prevStep.Title}' n'est pas validée !\n\nPoints bloquants :\n• {gapsText}\n\nSouhaitez-vous enregistrer une dérogation administrative justifiée pour continuer ?",
                            "Porte de maturité bloquante",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (response == MessageBoxResult.Yes)
                        {
                            string reason = Microsoft.VisualBasic.Interaction.InputBox(
                                "Saisissez le motif de la dérogation administrative (sera consigné dans le journal d'audit) :",
                                "Demande de dérogation",
                                "Session de travail exceptionnelle avec le bailleur");

                            if (!string.IsNullOrWhiteSpace(reason))
                            {
                                await _gateService.RecordGateOverrideAsync(_projectId, prevStep.Stage, reason.Trim(), SessionManager.CurrentUser?.Email ?? "Directeur");
                                await RefreshStatusesAsync();
                            }
                            else
                            {
                                MessageBox.Show("Navigation annulée : aucun motif de dérogation fourni.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }

            foreach (var s in Steps) s.IsActive = (s == targetStep);
            CurrentStep = targetStep;
            _onNavigate(targetStep.TabName);
        }

        public void SetActiveTab(string tabName)
        {
            var match = Steps.FirstOrDefault(s => s.TabName.Equals(tabName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                foreach (var s in Steps) s.IsActive = (s == match);
                CurrentStep = match;
            }
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.ViewModels.Radar
{
    /// <summary>
    /// Base de veille (ADR 28) : notation /25 de l'appel sélectionné, statuts, contrôle des échéances,
    /// couverture par axe, appels à venir et appels vus non retenus.
    /// </summary>
    public partial class ProjectRadarViewModel
    {
        private readonly VeilleBaseService _veilleService;
        private bool _veilleBasePrepared;

        public int[] NoteOptions { get; } = { 0, 1, 2, 3, 4, 5 };
        public string[] ConfirmationOptions { get; } = VeilleConfirmation.All;
        public string[] StatusOptions { get; } = VeilleStatus.All;

        public ObservableCollection<AxisCoverage> AxisCoverages { get; } = new();
        public ObservableCollection<UpcomingCall> UpcomingCalls { get; } = new();
        public ObservableCollection<RejectedCall> RejectedCalls { get; } = new();

        [ObservableProperty] private string _veilleSummaryMessage = string.Empty;
        [ObservableProperty] private string _deadlineCheckMessage = string.Empty;

        // Appel sélectionné et éditeur de notation
        [ObservableProperty] private GrantOpportunity? _selectedPipelineOpportunity;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditTotalPreview))] private int _editPertinence;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditTotalPreview))] private int _editEligibilite;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditTotalPreview))] private int _editFinancier;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditTotalPreview))] private int _editFaisabilite;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditTotalPreview))] private int _editStrategique;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(EditTotalPreview))] private string _editConfirmation = VeilleConfirmation.NonConfirme;
        [ObservableProperty] private string _editJustification = string.Empty;
        [ObservableProperty] private string _editStatus = VeilleStatus.Nouveau;
        [ObservableProperty] private string _editStatusReason = string.Empty;

        // Saisie d'un appel « À venir »
        [ObservableProperty] private string _newUpcomingDonor = string.Empty;
        [ObservableProperty] private string _newUpcomingProgramme = string.Empty;
        [ObservableProperty] private string _newUpcomingOpening = string.Empty;
        [ObservableProperty] private DateTime? _newUpcomingDeadline;
        [ObservableProperty] private string _newUpcomingLink = string.Empty;

        public bool HasSelectedPipelineOpportunity => SelectedPipelineOpportunity != null;

        /// <summary>Total et priorité recalculés en direct (plafond de 14 si l'appel n'est pas confirmé).</summary>
        public string EditTotalPreview
        {
            get
            {
                int raw = EditPertinence + EditEligibilite + EditFinancier + EditFaisabilite + EditStrategique;
                int total = VeilleScoringService.CappedTotal(raw, EditConfirmation);
                string cap = total < raw ? $" (plafonné, brut {raw})" : string.Empty;
                return $"{total}/25 — priorité {VeilleScoringService.PriorityOf(total)}{cap}";
            }
        }

        partial void OnSelectedPipelineOpportunityChanged(GrantOpportunity? value)
        {
            OnPropertyChanged(nameof(HasSelectedPipelineOpportunity));
            if (value == null) return;

            EditPertinence = value.ScorePertinence;
            EditEligibilite = value.ScoreEligibilite;
            EditFinancier = value.ScoreFinancier;
            EditFaisabilite = value.ScoreFaisabilite;
            EditStrategique = value.ScoreStrategique;
            EditConfirmation = VeilleConfirmation.All.Contains(value.Confirmation) ? value.Confirmation : VeilleConfirmation.NonConfirme;
            EditJustification = value.ScoreJustification;
            EditStatus = VeilleStatus.All.Contains(value.PipelineStatus) ? value.PipelineStatus : VeilleStatus.Nouveau;
            EditStatusReason = string.Empty;
        }

        /// <summary>Une fois par session : identifiants et notation des anciens appels, puis contrôle des échéances.</summary>
        private async Task PrepareVeilleBaseAsync()
        {
            if (_veilleBasePrepared) return;
            _veilleBasePrepared = true;
            try
            {
                await _veilleService.BackfillAsync(DateTime.Today);
                var changes = await _veilleService.RunDeadlineCheckAsync(DateTime.Today);
                DeadlineCheckMessage = DescribeChanges(changes);
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(ProjectRadarViewModel), "Préparation de la base de veille impossible.", ex);
            }
        }

        private async Task LoadVeilleTablesAsync()
        {
            try
            {
                var coverage = await _veilleService.ComputeAxisCoverageAsync();
                AxisCoverages.Clear();
                foreach (var c in coverage) AxisCoverages.Add(c);

                UpcomingCalls.Clear();
                foreach (var u in await _veilleService.GetUpcomingAsync()) UpcomingCalls.Add(u);

                RejectedCalls.Clear();
                foreach (var r in await _veilleService.GetRejectedAsync()) RejectedCalls.Add(r);

                var active = PipelineOpportunities.Where(o => VeilleStatus.IsActive(o.PipelineStatus)).ToList();
                var least = coverage.FirstOrDefault(c => c.IsLeastCovered);
                VeilleSummaryMessage = $"{active.Count} appel(s) suivi(s) : {active.Count(o => o.Priority == "A")} A, "
                                     + $"{active.Count(o => o.Priority == "B")} B, {active.Count(o => o.Priority == "C")} C"
                                     + (least != null ? $" — axe le moins couvert : {least.Code} {least.Label}" : string.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(ProjectRadarViewModel), "Chargement de la base de veille impossible.", ex);
            }
        }

        private async Task ReloadPipelineKeepingSelectionAsync(int? selectedId)
        {
            await LoadPipelineAsync();
            SelectedPipelineOpportunity = PipelineOpportunities.FirstOrDefault(o => o.Id == selectedId);
        }

        [RelayCommand]
        public async Task SaveVeilleScoreAsync()
        {
            var opp = SelectedPipelineOpportunity;
            if (opp == null) return;

            await _veilleService.SaveScoreAsync(opp.Id, EditPertinence, EditEligibilite, EditFinancier, EditFaisabilite,
                EditStrategique, EditConfirmation, EditJustification, DateTime.Today);
            await ReloadPipelineKeepingSelectionAsync(opp.Id);
        }

        [RelayCommand]
        public async Task ApplyVeilleStatusAsync()
        {
            var opp = SelectedPipelineOpportunity;
            if (opp == null) return;

            if (EditStatus == VeilleStatus.Ecarte && string.IsNullOrWhiteSpace(EditStatusReason))
            {
                MessageBox.Show("Indiquez le motif : un appel n'est jamais écarté sans motif.", "Base de veille", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _veilleService.SetStatusAsync(opp.Id, EditStatus, EditStatusReason, DateTime.Today);
            await ReloadPipelineKeepingSelectionAsync(opp.Id);
        }

        [RelayCommand]
        public async Task RunDeadlineCheckAsync()
        {
            var changes = await _veilleService.RunDeadlineCheckAsync(DateTime.Today);
            DeadlineCheckMessage = DescribeChanges(changes);
            await ReloadPipelineKeepingSelectionAsync(SelectedPipelineOpportunity?.Id);
        }

        [RelayCommand]
        public async Task AddUpcomingCallAsync()
        {
            if (string.IsNullOrWhiteSpace(NewUpcomingDonor) || string.IsNullOrWhiteSpace(NewUpcomingProgramme))
            {
                MessageBox.Show("Renseignez au moins le bailleur et le programme.", "Appels à venir", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _veilleService.AddUpcomingAsync(NewUpcomingDonor.Trim(), NewUpcomingProgramme.Trim(), NewUpcomingOpening.Trim(),
                NewUpcomingDeadline, string.Empty, NewUpcomingLink.Trim(), $"Saisi le {DateTime.Today:dd/MM/yyyy}.");
            NewUpcomingDonor = NewUpcomingProgramme = NewUpcomingOpening = NewUpcomingLink = string.Empty;
            NewUpcomingDeadline = null;
            await LoadVeilleTablesAsync();
        }

        [RelayCommand]
        public async Task PromoteUpcomingCallAsync(UpcomingCall? upcoming)
        {
            if (upcoming == null || upcoming.PromotedGrantId != null) return;

            var opp = await _veilleService.PromoteUpcomingAsync(upcoming.Id, DateTime.Today);
            if (opp != null)
            {
                await ReloadPipelineKeepingSelectionAsync(opp.Id);
                SelectedTabIndex = 1;
            }
        }

        private static string DescribeChanges(System.Collections.Generic.List<VeilleStatusChange> changes) =>
            changes.Count == 0
                ? $"Contrôle des échéances du {DateTime.Today:dd/MM/yyyy} : aucun changement."
                : $"Contrôle des échéances du {DateTime.Today:dd/MM/yyyy} : {changes.Count} changement(s).\n" + string.Join("\n", changes);
    }
}

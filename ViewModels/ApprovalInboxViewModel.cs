using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Collab;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ProposalItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private Contribution _contribution;

        [ObservableProperty]
        private List<DiffPiece> _diffPieces = new();

        /// <summary>Commentaire de l'administrateur (motif du rejet ou note d'approbation).</summary>
        [ObservableProperty]
        private string _commentInput = string.Empty;

        public ProposalItemViewModel(Contribution contribution)
        {
            _contribution = contribution;
            _diffPieces = IsCreation ? new List<DiffPiece>() : TextDiffHelper.CalculateWordDiff(contribution.BaseValue, contribution.ProposedValue);
        }

        public bool IsCreation => Contribution.FieldName == ProposalFields.Create;
        public bool IsNote => Contribution.FieldName == ProposalFields.Note;
        public bool IsFieldChange => !IsCreation && !IsNote;

        public string Title => string.IsNullOrEmpty(Contribution.EntityLabel)
            ? TeamDataModel.EntityTypeLabel(Contribution.EntityType)
            : Contribution.EntityLabel!;

        public string KindLabel => IsCreation ? "Ajout d'un élément" : IsNote ? "Suggestion" : $"Modification du champ « {Contribution.FieldName} »";
        public string StatusLabel => ContributionStatuses.Label(Contribution.Status);
        public string AuthorText => $"{Contribution.AuthorName} ({Contribution.AuthorEmail}) — {Contribution.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}";
        public bool HasReviewNotes => !string.IsNullOrWhiteSpace(Contribution.ReviewNotes);

        /// <summary>Contenu d'un ajout, lisible (colonnes renseignées).</summary>
        public string CreationDetails
        {
            get
            {
                if (!IsCreation) return string.Empty;
                try
                {
                    var payload = JsonSerializer.Deserialize<CreationPayload>(Contribution.ProposedValue ?? "{}");
                    var sb = new StringBuilder();
                    foreach (var (name, value) in payload?.Columns ?? new Dictionary<string, string?>())
                    {
                        if (string.IsNullOrWhiteSpace(value) || value == "0") continue;
                        var text = value.Length > 300 ? value[..297] + "…" : value;
                        sb.AppendLine($"{name} : {text}");
                    }
                    return sb.Length == 0 ? "(aucun champ renseigné)" : sb.ToString().TrimEnd();
                }
                catch (JsonException)
                {
                    return "(contenu illisible)";
                }
            }
        }
    }

    /// <summary>
    /// File de validation des propositions de l'équipe. Règle de l'association : décision de l'administrateur seul (ADR 23).
    /// </summary>
    public partial class ApprovalInboxViewModel : ObservableObject,
        IRecipient<ProposalCreatedMessage>,
        IRecipient<ProposalUpdatedMessage>
    {
        private readonly ContributionService _contributionService;
        private readonly AuthService _authService;

        [ObservableProperty]
        private ObservableCollection<ProposalItemViewModel> _proposals = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private int _pendingCount;

        public bool CanDecide => _authService.CanDecide();

        public string RoleHint => CanDecide
            ? (_authService.IsOfficialStation
                ? "Poste officiel : une proposition approuvée est intégrée immédiatement puis publiée pour l'équipe."
                : "Une proposition approuvée sera intégrée par le poste officiel lors de sa prochaine synchronisation.")
            : "Lecture seule : seuls les administrateurs de l'association approuvent ou rejettent les propositions.";

        public ApprovalInboxViewModel()
        {
            _contributionService = ContributionService.Instance;
            _authService = AuthService.Instance;

            // تسجيل الاشتراك في الرسائل التلقائية لتحديث الصندوق شبه لحظياً
            WeakReferenceMessenger.Default.Register<ProposalCreatedMessage>(this);
            WeakReferenceMessenger.Default.Register<ProposalUpdatedMessage>(this);

            LoadProposalsCommand.Execute(null);
        }

        [RelayCommand]
        public async Task LoadProposalsAsync()
        {
            IsLoading = true;
            try
            {
                var list = await _contributionService.GetPendingProposalsAsync();

                Proposals.Clear();
                foreach (var contrib in list)
                {
                    Proposals.Add(new ProposalItemViewModel(contrib));
                }

                PendingCount = Proposals.Count;
                StatusMessage = PendingCount == 0
                    ? "Aucune proposition en attente."
                    : $"{PendingCount} proposition(s) en attente de décision.";
                OnPropertyChanged(nameof(CanDecide));
                OnPropertyChanged(nameof(RoleHint));
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(ApprovalInboxViewModel), "Chargement des propositions impossible.", ex);
                StatusMessage = $"Erreur de chargement des propositions : {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task ApproveAsync(ProposalItemViewModel item)
        {
            if (item == null) return;
            await DecideAsync(item, async () =>
            {
                var result = await MergeService.Instance.ApproveAsync(item.Contribution.Id, item.CommentInput);
                if (result.ConflictDetected)
                {
                    var confirm = MessageBox.Show(result.Message + "\n\nAppliquer la proposition quand même ?",
                        "Valeur officielle modifiée", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm == MessageBoxResult.Yes)
                        result = await MergeService.Instance.ApproveAsync(item.Contribution.Id, item.CommentInput, force: true);
                }
                if (result.OfficialDataChanged) await PublishAfterIntegrationAsync();
                return result;
            });
        }

        [RelayCommand]
        public async Task RejectAsync(ProposalItemViewModel item)
        {
            if (item == null) return;
            await DecideAsync(item, () => MergeService.Instance.RejectAsync(item.Contribution.Id, item.CommentInput));
        }

        private async Task DecideAsync(ProposalItemViewModel item, Func<Task<MergeResult>> decision)
        {
            IsLoading = true;
            try
            {
                var result = await decision();
                StatusMessage = result.Message;
                if (result.Success)
                {
                    Proposals.Remove(item);
                    PendingCount = Proposals.Count;
                }
                else
                {
                    await LoadProposalsAsync();
                    StatusMessage = result.Message;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusMessage = ex.Message;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(ApprovalInboxViewModel), "Décision impossible.", ex);
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>Poste officiel : la version officielle est republiée aussitôt après une intégration.</summary>
        private static async Task PublishAfterIntegrationAsync()
        {
            if (!AuthService.Instance.IsOfficialStation) return;
            try
            {
                await TeamWorkspaceService.Instance.PublishAsync(onlyIfChanged: true);
            }
            catch (Exception ex)
            {
                TeamWorkspaceService.Instance.PublishRequested = true; // nouvelle tentative à la prochaine synchronisation
                AppLogger.Warn(nameof(ApprovalInboxViewModel), "Publication après intégration différée.", ex);
            }
        }

        // استقبال الرسائل التلقائية لتحديث الواجهة فور ورود مقترحات أو أصوات جديدة
        public void Receive(ProposalCreatedMessage message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (!ContributionStatuses.IsOpen(message.Value.Status) || message.Value.Status == ContributionStatuses.Accepted) return;
                if (Proposals.Any(p => p.Contribution.Id == message.Value.Id)) return;
                Proposals.Insert(0, new ProposalItemViewModel(message.Value));
                PendingCount = Proposals.Count;
                StatusMessage = $"Nouvelle proposition de {message.Value.AuthorName}.";
            });
        }

        public void Receive(ProposalUpdatedMessage message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var existing = Proposals.FirstOrDefault(p => p.Contribution.Id == message.Value.Id);
                if (existing == null) return;
                if (message.Value.Status is ContributionStatuses.Approved or ContributionStatuses.Rejected or ContributionStatuses.Accepted)
                {
                    Proposals.Remove(existing);
                }
                else
                {
                    existing.Contribution = message.Value;
                }
                PendingCount = Proposals.Count;
            });
        }
    }
}

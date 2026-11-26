using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Radar;
using AFMI_Montage_projet_2.Views;
using AFMI_Montage_projet_2.Views.Radar;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels.Radar
{
    public partial class ProjectRadarViewModel : ObservableObject
    {
        private readonly RssFeedService _rssService;
        private readonly OpportunityScoringService _scoringService;
        private readonly PipelineService _pipelineService;
        private CancellationTokenSource? _refreshCts;

        // 0 = Opportunités, 1 = Pipeline, 2 = Base de veille, 3 = Sources RSS
        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedThematicChip = string.Empty;

        public ObservableCollection<string> ThematicChips { get; } = new()
        {
            "🌲 Cèdre & Forêts",
            "💧 Eau & Zones humides",
            "🦅 Biodiversité",
            "🏕️ Écotourisme",
            "🌍 Climat & Résilience",
            "👥 Jeunesse",
            "🤝 Coopératives & Terroir",
            "🌿 Sylvopastoralisme"
        };

        [ObservableProperty]
        private string _selectedDonorFilter = "Tous";

        public ObservableCollection<string> AvailableDonors { get; } = new()
        {
            "Tous",
            "GEF",
            "PNUD",
            "Union Européenne",
            "Banque Mondiale",
            "AFD",
            "UICN",
            "FAO",
            "CEPF"
        };

        [ObservableProperty]
        private string _selectedAxisFilter = "Tous";

        public ObservableCollection<string> AvailableAxes { get; } = new() { "Tous" };

        [ObservableProperty]
        private string _selectedScoreFilter = "Tous";

        public ObservableCollection<string> AvailableScoreFilters { get; } = new()
        {
            "Tous",
            "≥ 5/10 (Moyenne)",
            "≥ 7/10 (Bonne)",
            "≥ 8/10 (Très forte)",
            "≥ 9/10 (Excellente)"
        };

        [ObservableProperty]
        private string _selectedDeadlineFilter = "En cours (non expirées)";

        public ObservableCollection<string> AvailableDeadlineFilters { get; } = new()
        {
            "En cours (non expirées)",
            "Échéance proche (< 30 jours)",
            "Échéance dans 60 jours",
            "Toutes (y compris expirées)",
            "Expirées uniquement"
        };

        [ObservableProperty]
        private string _selectedPublishDateFilter = "Toutes les dates";

        public ObservableCollection<string> AvailablePublishDateFilters { get; } = new()
        {
            "Toutes les dates",
            "Derniers 7 jours",
            "Derniers 30 jours",
            "Derniers 3 mois",
            "Cette année",
            "Période personnalisée"
        };

        [ObservableProperty]
        private DateTime? _customPublishDateMin;

        [ObservableProperty]
        private DateTime? _customPublishDateMax;

        [ObservableProperty]
        private DateTime? _customDeadlineMax;

        [ObservableProperty]
        private string _selectedSortOption = "Date de publication (plus récent)";

        public ObservableCollection<string> AvailableSortOptions { get; } = new()
        {
            "Date de publication (plus récent)",
            "Date limite (échéance la plus proche)",
            "Meilleure note IA",
            "Budget estimé (décroissant)"
        };

        // Collections principales
        public ObservableCollection<OpportunityItemViewModel> AllOpportunities { get; } = new();
        public ObservableCollection<OpportunityItemViewModel> FilteredOpportunities { get; } = new();
        public ObservableCollection<GrantOpportunity> PipelineOpportunities { get; } = new();
        public ObservableCollection<RssFeedConfig> FeedConfigs { get; } = new();

        // Statut d'actualisation en cours
        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private int _refreshProgress;

        [ObservableProperty]
        private string _refreshStatusMessage = string.Empty;

        public ObservableCollection<FeedFetchStatus> ActiveFeedStatuses { get; } = new();

        // Direction d'affichage et langue
        [ObservableProperty]
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;

        [ObservableProperty]
        private string _currentLanguage = "fr";

        public bool IsProjectRadarEnabled => ProjectRadarConfigService.IsEnabled;

        private readonly Action<Project>? _onOpenProjectRequested;

        public ProjectRadarViewModel() : this(null, null, null, null)
        {
        }

        public ProjectRadarViewModel(
            RssFeedService? rssService = null,
            OpportunityScoringService? scoringService = null,
            PipelineService? pipelineService = null,
            Action<Project>? onOpenProjectRequested = null)
        {
            _rssService = rssService ?? new RssFeedService();
            _scoringService = scoringService ?? new OpportunityScoringService();
            _pipelineService = pipelineService ?? new PipelineService();
            _veilleService = new VeilleBaseService(_pipelineService.DbContextFactory);
            _onOpenProjectRequested = onOpenProjectRequested;

            LoadAxesOptions();
            LoadSavedFilterPreferences();
            LoadInitialData();
        }

        private void LoadAxesOptions()
        {
            var axes = ProjectRadarConfigService.LoadAxes();
            AvailableAxes.Clear();
            AvailableAxes.Add("Tous");
            foreach (var a in axes)
            {
                AvailableAxes.Add(a.Title);
            }
        }

        private void LoadSavedFilterPreferences()
        {
            try
            {
                string savedDonor = SettingsService.GetSetting("Radar_Filter_Donor", "Tous");
                if (AvailableDonors.Contains(savedDonor)) SelectedDonorFilter = savedDonor;

                string savedScore = SettingsService.GetSetting("Radar_Filter_Score", "Tous");
                if (AvailableScoreFilters.Contains(savedScore)) SelectedScoreFilter = savedScore;

                string savedSort = SettingsService.GetSetting("Radar_Filter_Sort", "Plus récent");
                if (AvailableSortOptions.Contains(savedSort)) SelectedSortOption = savedSort;
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(ProjectRadarViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        private void SaveFilterPreferences()
        {
            try
            {
                SettingsService.SaveSetting("Radar_Filter_Donor", SelectedDonorFilter);
                SettingsService.SaveSetting("Radar_Filter_Score", SelectedScoreFilter);
                SettingsService.SaveSetting("Radar_Filter_Sort", SelectedSortOption);
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(ProjectRadarViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        public async void LoadInitialData()
        {
            await LoadFeedsAndCacheAsync();
            await LoadPipelineAsync();
        }

        public async Task LoadFeedsAndCacheAsync()
        {
            // 1. Charger les flux
            var feeds = ProjectRadarConfigService.LoadFeeds();
            FeedConfigs.Clear();
            foreach (var f in feeds) FeedConfigs.Add(f);

            // 2. Charger les éléments en cache
            var cached = await _rssService.GetCachedItemsAsync();
            AllOpportunities.Clear();
            foreach (var item in cached)
            {
                AllOpportunities.Add(new OpportunityItemViewModel(item));
            }

            ApplyFilters();
        }

        public async Task LoadPipelineAsync()
        {
            await PrepareVeilleBaseAsync();

            var pipeline = await _pipelineService.GetPipelineAsync();
            PipelineOpportunities.Clear();
            foreach (var opp in pipeline)
            {
                PipelineOpportunities.Add(opp);
            }

            await LoadVeilleTablesAsync();
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilters();
        partial void OnSelectedDonorFilterChanged(string value) { SaveFilterPreferences(); ApplyFilters(); }
        partial void OnSelectedAxisFilterChanged(string value) => ApplyFilters();
        partial void OnSelectedScoreFilterChanged(string value) { SaveFilterPreferences(); ApplyFilters(); }
        partial void OnSelectedDeadlineFilterChanged(string value) => ApplyFilters();
        partial void OnSelectedPublishDateFilterChanged(string value)
        {
            OnPropertyChanged(nameof(IsCustomPublishDateRange));
            ApplyFilters();
        }

        public bool IsCustomPublishDateRange => SelectedPublishDateFilter == "Période personnalisée";
        partial void OnCustomPublishDateMinChanged(DateTime? value) => ApplyFilters();
        partial void OnCustomPublishDateMaxChanged(DateTime? value) => ApplyFilters();
        partial void OnCustomDeadlineMaxChanged(DateTime? value) => ApplyFilters();
        partial void OnSelectedSortOptionChanged(string value) { SaveFilterPreferences(); ApplyFilters(); }

        [RelayCommand]
        public void SelectThematicChip(string chip)
        {
            if (SelectedThematicChip == chip)
            {
                // Désélectionner
                SelectedThematicChip = string.Empty;
                SearchQuery = string.Empty;
            }
            else
            {
                SelectedThematicChip = chip;
                // Extraire le mot-clé principal après l'icône
                string cleanWord = chip.Split('&')[0].Replace("🌲", "").Replace("💧", "").Replace("🦅", "").Replace("🏕️", "").Replace("🌍", "").Replace("👥", "").Replace("🤝", "").Replace("🌿", "").Trim();
                SearchQuery = cleanWord;
            }
        }

        [RelayCommand]
        public void ClearFilters()
        {
            SearchQuery = string.Empty;
            SelectedThematicChip = string.Empty;
            SelectedDonorFilter = "Tous";
            SelectedAxisFilter = "Tous";
            SelectedScoreFilter = "Tous";
            SelectedDeadlineFilter = "En cours (non expirées)";
            SelectedPublishDateFilter = "Toutes les dates";
            CustomPublishDateMin = null;
            CustomPublishDateMax = null;
            CustomDeadlineMax = null;
            SelectedSortOption = "Date de publication (plus récent)";
            ApplyFilters();
        }

        public void ApplyFilters()
        {
            var query = AllOpportunities.AsEnumerable();

            // 1. Recherche plein texte avec normalisation bilingue (ar/fr)
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string normQuery = TextNormalizer.Normalize(SearchQuery);
                query = query.Where(o =>
                    TextNormalizer.Normalize(o.Title).Contains(normQuery) ||
                    TextNormalizer.Normalize(o.CleanDescription).Contains(normQuery) ||
                    TextNormalizer.Normalize(o.Donor).Contains(normQuery) ||
                    TextNormalizer.Normalize(o.AiSummaryAr).Contains(normQuery) ||
                    TextNormalizer.Normalize(o.AiSummaryFr).Contains(normQuery)
                );
            }

            // 2. Filtre par donateur
            if (SelectedDonorFilter != "Tous")
            {
                query = query.Where(o => o.Donor.Contains(SelectedDonorFilter, StringComparison.OrdinalIgnoreCase) ||
                                         o.FeedName.Contains(SelectedDonorFilter, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Filtre par axe stratégique
            if (SelectedAxisFilter != "Tous")
            {
                string normAxis = TextNormalizer.Normalize(SelectedAxisFilter);
                query = query.Where(o => !string.IsNullOrEmpty(o.AiMatchedAxes) && TextNormalizer.Normalize(o.AiMatchedAxes).Contains(normAxis));
            }

            // 4. Filtre par note de pertinence IA
            if (SelectedScoreFilter != "Tous")
            {
                double minScore = SelectedScoreFilter switch
                {
                    var s when s.Contains("≥ 9/10") => 9.0,
                    var s when s.Contains("≥ 8/10") => 8.0,
                    var s when s.Contains("≥ 7/10") => 7.0,
                    var s when s.Contains("≥ 5/10") => 5.0,
                    _ => 0.0
                };
                query = query.Where(o => o.AiScore.HasValue && o.AiScore.Value >= minScore);
            }

            // 5. Filtre par Échéance / Date limite (Deadline)
            if (SelectedDeadlineFilter == "En cours (non expirées)")
            {
                query = query.Where(o => !o.IsExpired);
            }
            else if (SelectedDeadlineFilter == "Échéance proche (< 30 jours)")
            {
                query = query.Where(o => o.AiDeadline.HasValue && !o.IsExpired && o.DaysRemaining <= 30);
            }
            else if (SelectedDeadlineFilter == "Échéance dans 60 jours")
            {
                query = query.Where(o => o.AiDeadline.HasValue && !o.IsExpired && o.DaysRemaining <= 60);
            }
            else if (SelectedDeadlineFilter == "Expirées uniquement")
            {
                query = query.Where(o => o.IsExpired);
            }

            if (CustomDeadlineMax.HasValue)
            {
                query = query.Where(o => o.AiDeadline.HasValue && o.AiDeadline.Value.Date <= CustomDeadlineMax.Value.Date);
            }

            // 6. Filtre par Date de publication
            if (SelectedPublishDateFilter == "Derniers 7 jours")
            {
                var minDate = DateTime.Today.AddDays(-7);
                query = query.Where(o => o.PublishDate.HasValue && o.PublishDate.Value.Date >= minDate);
            }
            else if (SelectedPublishDateFilter == "Derniers 30 jours")
            {
                var minDate = DateTime.Today.AddDays(-30);
                query = query.Where(o => o.PublishDate.HasValue && o.PublishDate.Value.Date >= minDate);
            }
            else if (SelectedPublishDateFilter == "Derniers 3 mois")
            {
                var minDate = DateTime.Today.AddMonths(-3);
                query = query.Where(o => o.PublishDate.HasValue && o.PublishDate.Value.Date >= minDate);
            }
            else if (SelectedPublishDateFilter == "Cette année")
            {
                var year = DateTime.Today.Year;
                query = query.Where(o => o.PublishDate.HasValue && o.PublishDate.Value.Year == year);
            }
            else if (SelectedPublishDateFilter == "Période personnalisée")
            {
                if (CustomPublishDateMin.HasValue)
                    query = query.Where(o => o.PublishDate.HasValue && o.PublishDate.Value.Date >= CustomPublishDateMin.Value.Date);
                if (CustomPublishDateMax.HasValue)
                    query = query.Where(o => o.PublishDate.HasValue && o.PublishDate.Value.Date <= CustomPublishDateMax.Value.Date);
            }

            // 7. Tri
            query = SelectedSortOption switch
            {
                "Meilleure note IA" => query.OrderByDescending(o => o.AiScore ?? -1).ThenByDescending(o => o.PublishDate),
                "Date limite (échéance la plus proche)" => query.OrderBy(o => o.AiDeadline.HasValue ? 0 : 1).ThenBy(o => o.AiDeadline).ThenByDescending(o => o.PublishDate),
                "Budget estimé (décroissant)" => query.OrderByDescending(o => o.AiEstimatedBudget ?? -1).ThenByDescending(o => o.PublishDate),
                _ => query.OrderByDescending(o => o.PublishDate ?? o.FetchedAt)
            };

            FilteredOpportunities.Clear();
            foreach (var item in query)
            {
                FilteredOpportunities.Add(item);
            }
        }

        [RelayCommand]
        public async Task RefreshAllFeedsAsync()
        {
            if (IsRefreshing) return;

            IsRefreshing = true;
            RefreshProgress = 0;
            RefreshStatusMessage = "Interrogation des flux institutionnels en parallèle...";
            ActiveFeedStatuses.Clear();
            _refreshCts = new CancellationTokenSource();

            var progress = new Progress<FeedFetchStatus>(status =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var existing = ActiveFeedStatuses.FirstOrDefault(s => s.FeedId == status.FeedId);
                    if (existing != null)
                    {
                        if (!ReferenceEquals(existing, status))
                        {
                            existing.Status = status.Status;
                            existing.NewItemsCount = status.NewItemsCount;
                            existing.UpdatedItemsCount = status.UpdatedItemsCount;
                            existing.ErrorMessage = status.ErrorMessage;
                        }
                    }
                    else
                    {
                        ActiveFeedStatuses.Add(status);
                    }

                    int done = ActiveFeedStatuses.Count(s => s.IsDone);
                    int total = FeedConfigs.Count(f => f.Enabled);
                    if (total > 0)
                    {
                        RefreshProgress = (int)((done / (double)total) * 100);
                        RefreshStatusMessage = $"Actualisation : {done}/{total} flux traités...";
                    }
                });
            });

            try
            {
                var cached = await _rssService.RefreshAllFeedsAsync(
                    maxConcurrency: 4,
                    progress: progress,
                    cancellationToken: _refreshCts.Token);

                RefreshStatusMessage = _rssService.LastSummary is { } summary
                    ? "Actualisation terminée : " + summary.ToMessage()
                    : "Actualisation terminée.";

                // Recharger les éléments
                await LoadFeedsAndCacheAsync();
            }
            catch (OperationCanceledException)
            {
                RefreshStatusMessage = "Actualisation annulée par l'utilisateur.";
            }
            catch (Exception ex)
            {
                RefreshStatusMessage = $"Erreur lors de l'actualisation : {ex.Message}";
            }
            finally
            {
                IsRefreshing = false;
                _refreshCts = null;
            }
        }

        [RelayCommand]
        public void CancelRefresh()
        {
            _refreshCts?.Cancel();
        }

        [RelayCommand]
        public async Task ScoreItemAsync(OpportunityItemViewModel item)
        {
            if (item.IsScoringInProgress) return;
            item.IsScoringInProgress = true;

            try
            {
                var result = await _scoringService.ScoreOpportunityAsync(item.Model, forceRefresh: true);
                item.ApplyScoringResult(result);

                // Sauvegarder dans la base locale
                using var db = new AppDbContext();
                var entity = await db.RssItemsCache.FirstOrDefaultAsync(i => i.ExternalId == item.ExternalId);
                if (entity != null)
                {
                    entity.AiScore = item.AiScore;
                    entity.AiScoreRationale = item.AiScoreRationale;
                    entity.AiMatchedAxes = item.AiMatchedAxes;
                    entity.AiSummaryAr = item.AiSummaryAr;
                    entity.AiSummaryFr = item.AiSummaryFr;
                    entity.AiEstimatedBudget = item.AiEstimatedBudget;
                    entity.AiCurrency = item.AiCurrency;
                    entity.AiDeadline = item.AiDeadline;
                    entity.IsScored = true;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'évaluation : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                item.IsScoringInProgress = false;
            }
        }

        [RelayCommand]
        public async Task AddToPipelineAsync(OpportunityItemViewModel item)
        {
            var (success, message, opp) = await _pipelineService.AddToPipelineAsync(item.Model);
            if (success)
            {
                item.IsInPipeline = true;
                if (opp != null) PipelineOpportunities.Insert(0, opp);
                MessageBox.Show(message, "Pipeline des Projets", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        public async Task DraftProposalAsync(OpportunityItemViewModel item)
        {
            if (item == null) return;

            try
            {
                var formulationService = new AiProjectFormulationService();
                RefreshStatusMessage = "Formulation du projet par l'IA et initialisation de toutes les étapes...";

                var project = await formulationService.FormulateProjectFromOpportunityAsync(item.Model, (msg) =>
                {
                    RefreshStatusMessage = msg;
                });

                // Marquer comme étant dans le pipeline
                item.IsInPipeline = true;
                await LoadPipelineAsync();

                MessageBox.Show(
                    $"Le projet a été créé et formulé avec succès par l'IA :\n\n« {project.Title} »\n\nToutes les composantes méthodologiques ont été générées automatiquement (Fiche de cadrage, Arbre de problèmes, Cadre Logique, Indicateurs SMART, Gestion des risques, et Budget prévisionnel).\n\nL'espace de travail du projet va maintenant s'ouvrir pour votre révision et ajustements.",
                    "Formulation de projet par l'IA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Navigation vers le projet dans l'espace de travail
                if (_onOpenProjectRequested != null)
                {
                    _onOpenProjectRequested(project);
                }
                else if (Application.Current?.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.LoadProject(project);
                }

                // Fermer la fenêtre pop-up ProjectRadarWindow si elle est ouverte
                foreach (Window win in Application.Current?.Windows ?? new WindowCollection())
                {
                    if (win is ProjectRadarWindow radarWin)
                    {
                        radarWin.Close();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la formulation du projet : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void OpenLink(string? url)
        {
            PipelineService.OpenOriginalUrlSafely(url);
        }

        [RelayCommand]
        public void ToggleSummaryLanguage(OpportunityItemViewModel item)
        {
            item.ToggleSummaryLanguage();
        }

        [RelayCommand]
        public void ToggleFeed(RssFeedConfig feed)
        {
            feed.Enabled = !feed.Enabled;
            ProjectRadarConfigService.ToggleFeed(feed.Id, feed.Enabled);
        }

        [RelayCommand]
        public async Task RemoveFromPipelineAsync(GrantOpportunity opp)
        {
            if (MessageBox.Show($"Voulez-vous retirer \"{opp.Title}\" du pipeline ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _pipelineService.RemoveFromPipelineAsync(opp.Id);
                PipelineOpportunities.Remove(opp);

                var match = AllOpportunities.FirstOrDefault(o => o.ExternalId == opp.ExternalId || o.Link == opp.OriginalUrl);
                if (match != null) match.IsInPipeline = false;
            }
        }

        [RelayCommand]
        public async Task UpdatePipelineStatusAsync(Tuple<GrantOpportunity, string> param)
        {
            if (param == null) return;
            var (opp, newStatus) = (param.Item1, param.Item2);
            await _pipelineService.UpdatePipelineStatusAsync(opp.Id, newStatus);
            opp.PipelineStatus = newStatus;
        }

        [RelayCommand]
        public void OpenAddFeedDialog()
        {
            var dlg = new Views.Radar.AddEditFeedDialog();
            if (dlg.ShowDialog() == true && dlg.CreatedFeed != null)
            {
                FeedConfigs.Add(dlg.CreatedFeed);
            }
        }

        [RelayCommand]
        public void DeleteFeed(RssFeedConfig feed)
        {
            if (feed == null) return;
            if (feed.IsDefault)
            {
                MessageBox.Show("Les flux institutionnels de base ne peuvent pas être supprimés. Vous pouvez les désactiver.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Supprimer la source '{feed.Name}' ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ProjectRadarConfigService.DeleteCustomFeed(feed.Id);
                FeedConfigs.Remove(feed);
            }
        }

        [RelayCommand]
        public void OpenConfigureAxesDialog()
        {
            var dlg = new Views.Radar.AfmiAxesConfigDialog();
            if (dlg.ShowDialog() == true)
            {
                LoadAxesOptions();
            }
        }

        [RelayCommand]
        public void ToggleRtlDirection()
        {
            if (FlowDirection == FlowDirection.LeftToRight)
            {
                FlowDirection = FlowDirection.RightToLeft;
                CurrentLanguage = "ar";
            }
            else
            {
                FlowDirection = FlowDirection.LeftToRight;
                CurrentLanguage = "fr";
            }
        }
    }
}

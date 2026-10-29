using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Strategic;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Strategic;
using AFMI_Montage_projet_2.Security;

namespace AFMI_Montage_projet_2.ViewModels.Strategic
{
    public partial class StrategicPlanningViewModel : ObservableObject
    {
        private readonly int _projectId;
        public int ProjectId => _projectId;
        private readonly StrategicFoundationService _foundationService;
        private readonly ReadinessGateService _gateService;
        private readonly Action<string>? _onNavigateTab;

        [ObservableProperty]
        private StrategicFoundation _foundation = new();

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        // Collections de données pour affichage dans les onglets
        [ObservableProperty] private ObservableCollection<DataSource> _dataSources = new();
        [ObservableProperty] private ObservableCollection<ContextIndicator> _contextIndicators = new();
        [ObservableProperty] private ObservableCollection<StakeholderEntry> _stakeholders = new();
        [ObservableProperty] private ObservableCollection<SwotItem> _swotItems = new();
        [ObservableProperty] private ObservableCollection<PestelItem> _pestelItems = new();
        [ObservableProperty] private ObservableCollection<NeedAssessment> _needsAssessments = new();
        [ObservableProperty] private ObservableCollection<StrategicOption> _strategicOptions = new();
        [ObservableProperty] private ObservableCollection<TheoryOfChangeNode> _theoryOfChangeNodes = new();
        [ObservableProperty] private ObservableCollection<StrategicObjective> _strategicObjectives = new();
        [ObservableProperty] private ObservableCollection<RiskEntry> _risks = new();
        [ObservableProperty] private ObservableCollection<AssumptionEntry> _assumptions = new();

        // Évaluation de la porte de maturité
        [ObservableProperty] private GateEvaluationResult _readinessResult = new();
        [ObservableProperty] private string _overrideReasonInput = string.Empty;

        // Champs de saisie rapide pour les nouveaux éléments
        [ObservableProperty] private string _newDataSourceTitle = string.Empty;
        [ObservableProperty] private string _newDataSourceType = "Document";
        [ObservableProperty] private string _newDataSourceReliability = "High";

        [ObservableProperty] private string _newIndicatorName = string.Empty;
        [ObservableProperty] private string _newIndicatorBaseline = string.Empty;
        [ObservableProperty] private string _newIndicatorUnit = string.Empty;

        [ObservableProperty] private string _newStakeholderName = string.Empty;
        [ObservableProperty] private string _newStakeholderCategory = "Bénéficiaire";
        [ObservableProperty] private string _newStakeholderRole = string.Empty;
        [ObservableProperty] private int _newStakeholderPower = 3;
        [ObservableProperty] private int _newStakeholderInterest = 3;

        [ObservableProperty] private string _newSwotCategory = "Strength";
        [ObservableProperty] private string _newSwotStatement = string.Empty;

        [ObservableProperty] private string _newPestelDimension = "Political";
        [ObservableProperty] private string _newPestelDescription = string.Empty;

        [ObservableProperty] private string _newNeedDescription = string.Empty;
        [ObservableProperty] private string _newNeedTargetGroup = string.Empty;
        [ObservableProperty] private int _newNeedUrgency = 3;
        [ObservableProperty] private int _newNeedFeasibility = 3;
        [ObservableProperty] private int _newNeedImpact = 3;

        [ObservableProperty] private string _newOptionTitle = string.Empty;
        [ObservableProperty] private string _newOptionDescription = string.Empty;

        [ObservableProperty] private string _newTocType = "Activity";
        [ObservableProperty] private string _newTocTitle = string.Empty;
        [ObservableProperty] private string _newTocDescription = string.Empty;

        [ObservableProperty] private string _newObjectiveCode = "SO1";
        [ObservableProperty] private string _newObjectiveTitle = string.Empty;
        [ObservableProperty] private string _newObjectiveDescription = string.Empty;

        [ObservableProperty] private string _newRiskCode = "R01";
        [ObservableProperty] private string _newRiskCategory = "Opérationnel";
        [ObservableProperty] private string _newRiskDescription = string.Empty;
        [ObservableProperty] private string _newRiskMitigation = string.Empty;
        [ObservableProperty] private int _newRiskProbability = 3;
        [ObservableProperty] private int _newRiskImpact = 3;

        [ObservableProperty] private string _newAssumptionStatement = string.Empty;
        [ObservableProperty] private string _newAssumptionLevel = "ActivityToOutput";

        public StrategicPlanningViewModel(int projectId, Action<string>? onNavigateTab = null)
        {
            _projectId = projectId;
            _onNavigateTab = onNavigateTab;
            _foundationService = StrategicFoundationService.Instance;
            _gateService = ReadinessGateService.Instance;
            Territory = new TerritoryViewModel(projectId, OnTerritorySavedAsync);

            _ = LoadFoundationDataAsync();
        }

        /// <summary>Onglet « Territoire &amp; bénéficiaires » (ADR 25).</summary>
        public TerritoryViewModel Territory { get; }

        /// <summary>Index de l'onglet Territoire dans la vue (accès depuis la fiche).</summary>
        public const int TerritoryTabIndex = 1;

        [RelayCommand]
        private void OpenTerritoryTab() => SelectedTabIndex = TerritoryTabIndex;

        /// <summary>
        /// Après l'enregistrement du territoire : les modifications en cours du socle sont enregistrées,
        /// puis tout est rechargé (zone, public cible, parties prenantes bénéficiaires et porte de maturité à jour).
        /// </summary>
        private async Task OnTerritorySavedAsync()
        {
            await SaveFoundationAsync();
            await LoadFoundationDataAsync();
        }

        [RelayCommand]
        public async Task LoadFoundationDataAsync()
        {
            IsBusy = true;
            try
            {
                Foundation = await _foundationService.GetOrCreateFoundationAsync(_projectId, SessionManager.CurrentUser?.Email ?? "Système");

                DataSources = new ObservableCollection<DataSource>(Foundation.DataSources.Where(d => !d.IsDeleted));
                ContextIndicators = new ObservableCollection<ContextIndicator>(Foundation.ContextIndicators.Where(c => !c.IsDeleted));
                Stakeholders = new ObservableCollection<StakeholderEntry>(Foundation.Stakeholders.Where(s => !s.IsDeleted));
                SwotItems = new ObservableCollection<SwotItem>(Foundation.SwotItems.Where(s => !s.IsDeleted));
                PestelItems = new ObservableCollection<PestelItem>(Foundation.PestelItems.Where(p => !p.IsDeleted));
                NeedsAssessments = new ObservableCollection<NeedAssessment>(Foundation.NeedsAssessments.Where(n => !n.IsDeleted).OrderByDescending(n => n.WeightedScore));
                StrategicOptions = new ObservableCollection<StrategicOption>(Foundation.StrategicOptions.Where(o => !o.IsDeleted));
                TheoryOfChangeNodes = new ObservableCollection<TheoryOfChangeNode>(Foundation.TheoryOfChangeNodes.Where(t => !t.IsDeleted).OrderBy(t => t.OrderIndex));
                StrategicObjectives = new ObservableCollection<StrategicObjective>(Foundation.StrategicObjectives.Where(o => !o.IsDeleted));
                Risks = new ObservableCollection<RiskEntry>(Foundation.Risks.Where(r => !r.IsDeleted));
                Assumptions = new ObservableCollection<AssumptionEntry>(Foundation.Assumptions.Where(a => !a.IsDeleted));

                await RefreshPropagationLinksAsync();
                await RefreshReadinessGateAsync();
                StatusMessage = "Données stratégiques chargées avec succès.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur de chargement: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task SaveFoundationAsync()
        {
            IsBusy = true;
            try
            {
                using var db = new AppDbContext();
                var dbFoundation = await db.StrategicFoundations.FirstOrDefaultAsync(sf => sf.Id == Foundation.Id);
                if (dbFoundation != null)
                {
                    dbFoundation.IdeaTitle = Foundation.IdeaTitle;
                    dbFoundation.TargetZone = Foundation.TargetZone;
                    dbFoundation.TargetBeneficiaries = Foundation.TargetBeneficiaries;
                    dbFoundation.Sector = Foundation.Sector;
                    dbFoundation.EstimatedDurationMonths = Foundation.EstimatedDurationMonths;
                    dbFoundation.EstimatedBudget = Foundation.EstimatedBudget;
                    dbFoundation.Currency = Foundation.Currency;
                    dbFoundation.PotentialDonor = Foundation.PotentialDonor;
                    dbFoundation.DonorPriorities = Foundation.DonorPriorities;
                    dbFoundation.CoreProblemStatement = Foundation.CoreProblemStatement;
                    dbFoundation.ProblemEvidenceData = Foundation.ProblemEvidenceData;
                    dbFoundation.SelectedStrategicOptionRationale = Foundation.SelectedStrategicOptionRationale;
                    dbFoundation.GeneralStrategicObjective = Foundation.GeneralStrategicObjective;
                    dbFoundation.CompletenessScore = _foundationService.CalculateCompleteness(Foundation);
                    dbFoundation.LastModifiedAt = DateTime.UtcNow;
                    dbFoundation.Version++;

                    await db.SaveChangesAsync();
                    Foundation.CompletenessScore = dbFoundation.CompletenessScore;

                    // Zone et public cible dérivent du territoire et des bénéficiaires (source unique, ADR 25)
                    await Services.Research.TerritoryService.SynchronizeAsync(db, _projectId, SessionManager.CurrentUser?.Email ?? "Système");
                    Foundation.TargetZone = dbFoundation.TargetZone;
                    Foundation.TargetBeneficiaries = dbFoundation.TargetBeneficiaries;
                    OnPropertyChanged(nameof(Foundation));
                }

                // Les éléments dérivés (cadre logique, arbre) deviennent « à revoir » si leur source a changé
                await RefreshPropagationLinksAsync();
                await RefreshReadinessGateAsync();
                StatusMessage = StaleLinkCount > 0
                    ? $"Modifications enregistrées. {StaleLinkCount} élément(s) dérivé(s) à revoir : utilisez « Propager vers les étapes suivantes »."
                    : "Modifications enregistrées avec succès !";
                AuditLogger.Log("Sauvegarde socle stratégique", $"Modifications enregistrées pour le projet {_projectId}", SessionManager.CurrentUser?.Email);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'enregistrement: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // --- Ajout et suppression des éléments ---

        [RelayCommand]
        public async Task AddDataSource()
        {
            if (string.IsNullOrWhiteSpace(NewDataSourceTitle)) return;
            using var db = new AppDbContext();
            var item = new DataSource
            {
                StrategicFoundationId = Foundation.Id,
                Title = NewDataSourceTitle.Trim(),
                SourceType = NewDataSourceType,
                ReliabilityLevel = NewDataSourceReliability,
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.DataSources.Add(item);
            await db.SaveChangesAsync();
            DataSources.Add(item);
            NewDataSourceTitle = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteDataSource(DataSource? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<DataSource>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            DataSources.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddContextIndicator()
        {
            if (string.IsNullOrWhiteSpace(NewIndicatorName)) return;
            using var db = new AppDbContext();
            var item = new ContextIndicator
            {
                StrategicFoundationId = Foundation.Id,
                Name = NewIndicatorName.Trim(),
                BaselineValue = NewIndicatorBaseline.Trim(),
                Unit = NewIndicatorUnit.Trim(),
                Year = DateTime.UtcNow.Year,
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.ContextIndicators.Add(item);
            await db.SaveChangesAsync();
            ContextIndicators.Add(item);
            NewIndicatorName = string.Empty;
            NewIndicatorBaseline = string.Empty;
            NewIndicatorUnit = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteContextIndicator(ContextIndicator? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<ContextIndicator>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            ContextIndicators.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddStakeholder()
        {
            if (string.IsNullOrWhiteSpace(NewStakeholderName)) return;
            using var db = new AppDbContext();
            var item = new StakeholderEntry
            {
                StrategicFoundationId = Foundation.Id,
                Name = NewStakeholderName.Trim(),
                Category = NewStakeholderCategory,
                Role = NewStakeholderRole.Trim(),
                PowerLevel = Math.Clamp(NewStakeholderPower, 1, 5),
                InterestLevel = Math.Clamp(NewStakeholderInterest, 1, 5),
                EngagementStrategy = ResolveStrategy(NewStakeholderPower, NewStakeholderInterest),
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.Stakeholders.Add(item);
            await db.SaveChangesAsync();
            Stakeholders.Add(item);
            NewStakeholderName = string.Empty;
            NewStakeholderRole = string.Empty;
            await SaveFoundationAsync();
        }

        private string ResolveStrategy(int power, int interest)
        {
            if (power >= 3 && interest >= 3) return "Collaborer étroitement (Manage Closely)";
            if (power >= 3 && interest < 3) return "Maintenir satisfait (Keep Satisfied)";
            if (power < 3 && interest >= 3) return "Tenir informé (Keep Informed)";
            return "Surveiller avec un minimum d'effort (Monitor)";
        }

        [RelayCommand]
        public async Task DeleteStakeholder(StakeholderEntry? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<StakeholderEntry>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            Stakeholders.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddSwotItem()
        {
            if (string.IsNullOrWhiteSpace(NewSwotStatement)) return;
            using var db = new AppDbContext();
            var item = new SwotItem
            {
                StrategicFoundationId = Foundation.Id,
                Category = NewSwotCategory,
                Statement = NewSwotStatement.Trim(),
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.SwotItems.Add(item);
            await db.SaveChangesAsync();
            SwotItems.Add(item);
            NewSwotStatement = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteSwotItem(SwotItem? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<SwotItem>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            SwotItems.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddPestelItem()
        {
            if (string.IsNullOrWhiteSpace(NewPestelDescription)) return;
            using var db = new AppDbContext();
            var item = new PestelItem
            {
                StrategicFoundationId = Foundation.Id,
                Dimension = NewPestelDimension,
                Description = NewPestelDescription.Trim(),
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.PestelItems.Add(item);
            await db.SaveChangesAsync();
            PestelItems.Add(item);
            NewPestelDescription = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeletePestelItem(PestelItem? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<PestelItem>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            PestelItems.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddNeedAssessment()
        {
            if (string.IsNullOrWhiteSpace(NewNeedDescription)) return;
            using var db = new AppDbContext();
            var item = new NeedAssessment
            {
                StrategicFoundationId = Foundation.Id,
                NeedDescription = NewNeedDescription.Trim(),
                TargetGroupAffected = NewNeedTargetGroup.Trim(),
                UrgencyScore = Math.Clamp(NewNeedUrgency, 1, 5),
                FeasibilityScore = Math.Clamp(NewNeedFeasibility, 1, 5),
                ImpactScore = Math.Clamp(NewNeedImpact, 1, 5),
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.NeedsAssessments.Add(item);
            await db.SaveChangesAsync();
            NeedsAssessments.Add(item);
            NewNeedDescription = string.Empty;
            NewNeedTargetGroup = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteNeedAssessment(NeedAssessment? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<NeedAssessment>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            NeedsAssessments.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddStrategicOption()
        {
            if (string.IsNullOrWhiteSpace(NewOptionTitle)) return;
            using var db = new AppDbContext();
            var item = new StrategicOption
            {
                StrategicFoundationId = Foundation.Id,
                OptionTitle = NewOptionTitle.Trim(),
                Description = NewOptionDescription.Trim(),
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.StrategicOptions.Add(item);
            await db.SaveChangesAsync();
            StrategicOptions.Add(item);
            NewOptionTitle = string.Empty;
            NewOptionDescription = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task SelectStrategicOption(StrategicOption? item)
        {
            if (item == null) return;
            using var db = new AppDbContext();
            var all = await db.StrategicOptions.Where(o => o.StrategicFoundationId == Foundation.Id).ToListAsync();
            foreach (var opt in all)
            {
                opt.IsSelected = (opt.Id == item.Id);
            }
            await db.SaveChangesAsync();
            foreach (var opt in StrategicOptions)
            {
                opt.IsSelected = (opt.Id == item.Id);
            }
            Foundation.SelectedStrategicOptionRationale = $"Option retenue : {item.OptionTitle}. {item.Description}";
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteStrategicOption(StrategicOption? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<StrategicOption>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            StrategicOptions.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddTheoryOfChangeNode()
        {
            if (string.IsNullOrWhiteSpace(NewTocTitle)) return;
            using var db = new AppDbContext();
            var item = new TheoryOfChangeNode
            {
                StrategicFoundationId = Foundation.Id,
                NodeType = NewTocType,
                Title = NewTocTitle.Trim(),
                Description = NewTocDescription.Trim(),
                OrderIndex = TheoryOfChangeNodes.Count + 1,
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.TheoryOfChangeNodes.Add(item);
            await db.SaveChangesAsync();
            TheoryOfChangeNodes.Add(item);
            NewTocTitle = string.Empty;
            NewTocDescription = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteTheoryOfChangeNode(TheoryOfChangeNode? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<TheoryOfChangeNode>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            TheoryOfChangeNodes.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddStrategicObjective()
        {
            if (string.IsNullOrWhiteSpace(NewObjectiveTitle)) return;
            using var db = new AppDbContext();
            var item = new StrategicObjective
            {
                StrategicFoundationId = Foundation.Id,
                Code = $"SO{StrategicObjectives.Count + 1}",
                Title = NewObjectiveTitle.Trim(),
                Description = NewObjectiveDescription.Trim(),
                IsSpecificObjective = true,
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.StrategicObjectives.Add(item);
            await db.SaveChangesAsync();
            StrategicObjectives.Add(item);
            NewObjectiveTitle = string.Empty;
            NewObjectiveDescription = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteStrategicObjective(StrategicObjective? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<StrategicObjective>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            StrategicObjectives.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddRisk()
        {
            if (string.IsNullOrWhiteSpace(NewRiskDescription)) return;
            using var db = new AppDbContext();
            var item = new RiskEntry
            {
                StrategicFoundationId = Foundation.Id,
                Code = $"R{Risks.Count + 1:00}",
                Category = NewRiskCategory,
                Description = NewRiskDescription.Trim(),
                MitigationStrategy = NewRiskMitigation.Trim(),
                Probability = Math.Clamp(NewRiskProbability, 1, 5),
                Impact = Math.Clamp(NewRiskImpact, 1, 5),
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.StrategicRisks.Add(item);
            await db.SaveChangesAsync();
            Risks.Add(item);
            NewRiskDescription = string.Empty;
            NewRiskMitigation = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteRisk(RiskEntry? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<RiskEntry>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            Risks.Remove(item);
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task AddAssumption()
        {
            if (string.IsNullOrWhiteSpace(NewAssumptionStatement)) return;
            using var db = new AppDbContext();
            var item = new AssumptionEntry
            {
                StrategicFoundationId = Foundation.Id,
                Statement = NewAssumptionStatement.Trim(),
                AssociatedLevel = NewAssumptionLevel,
                CreatedBy = SessionManager.CurrentUser?.Email ?? "Système"
            };
            db.StrategicAssumptions.Add(item);
            await db.SaveChangesAsync();
            Assumptions.Add(item);
            NewAssumptionStatement = string.Empty;
            await SaveFoundationAsync();
        }

        [RelayCommand]
        public async Task DeleteAssumption(AssumptionEntry? item)
        {
            if (item == null) return;
            await _foundationService.SoftDeleteAsync<AssumptionEntry>(item.Id, SessionManager.CurrentUser?.Email ?? "Système");
            Assumptions.Remove(item);
            await SaveFoundationAsync();
        }

        // --- Porte de maturité et dérogation ---

        [RelayCommand]
        public async Task RefreshReadinessGateAsync()
        {
            ReadinessResult = await _gateService.EvaluateStageAsync(_projectId, LifecycleStage.StrategicPlanning);
        }

        [RelayCommand]
        public async Task OverrideReadinessGateAsync()
        {
            if (string.IsNullOrWhiteSpace(OverrideReasonInput))
            {
                MessageBox.Show("Veuillez saisir le motif justificatif obligatoire pour la dérogation administrative.", "Motif requis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _gateService.RecordGateOverrideAsync(_projectId, LifecycleStage.StrategicPlanning, OverrideReasonInput.Trim(), SessionManager.CurrentUser?.Email ?? "Directeur");
            OverrideReasonInput = string.Empty;
            await RefreshReadinessGateAsync();
            MessageBox.Show("Dérogation administrative validée et enregistrée dans le journal d'audit !", "Dérogation accordée", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- Propagation vers les étapes aval (ADR 4 : validation humaine obligatoire) ---

        [ObservableProperty] private ObservableCollection<ProposedPropagationItem> _propagationProposals = new();
        [ObservableProperty] private string _propagationStatus = "Choisissez l'étape à alimenter à partir du socle stratégique.";
        [ObservableProperty] private int _staleLinkCount;
        private LifecycleStage? _propagationTarget;

        public bool HasPropagationProposals => PropagationProposals.Count > 0;

        partial void OnPropagationProposalsChanged(ObservableCollection<ProposedPropagationItem> value) =>
            OnPropertyChanged(nameof(HasPropagationProposals));

        private async Task RefreshPropagationLinksAsync()
        {
            try
            {
                var refresh = await PropagationEngineService.Instance.RefreshLinkStatusesAsync(_projectId);
                StaleLinkCount = refresh.StaleCount;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(StrategicPlanningViewModel), "Recalcul des liens de propagation impossible.", ex);
            }
        }

        /// <param name="stageKey">"Cadrage", "ProblemTree" ou "CadreLogique".</param>
        [RelayCommand]
        public async Task PreparePropagationAsync(string? stageKey)
        {
            if (!Enum.TryParse(stageKey, out LifecycleStage stage)) return;
            IsBusy = true;
            try
            {
                await SaveFoundationAsync();
                _propagationTarget = stage;
                var proposals = await PropagationEngineService.Instance.GenerateProposalsAsync(_projectId, stage);
                PropagationProposals = new ObservableCollection<ProposedPropagationItem>(proposals);
                PropagationStatus = proposals.Count == 0
                    ? "Rien à propager : l'étape est déjà alignée sur le socle stratégique (ou le socle ne contient pas encore les éléments sources)."
                    : $"{proposals.Count} proposition(s). Vérifiez, décochez ce que vous ne souhaitez pas appliquer, puis validez.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(StrategicPlanningViewModel), "Échec de la préparation de la propagation.", ex);
                PropagationStatus = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ApplyPropagationAsync()
        {
            if (!PropagationProposals.Any(p => p.IsSelected))
            {
                PropagationStatus = "Aucune proposition sélectionnée.";
                return;
            }

            IsBusy = true;
            try
            {
                var summary = await PropagationEngineService.Instance.ApplyProposalsAsync(
                    _projectId, PropagationProposals.ToList(), SessionManager.CurrentUser?.Email ?? "Système");

                PropagationProposals = new ObservableCollection<ProposedPropagationItem>();
                await RefreshPropagationLinksAsync();
                await RefreshReadinessGateAsync();

                PropagationStatus = $"✅ {summary.CreatedCount} création(s), {summary.UpdatedCount} mise(s) à jour, {summary.SkippedCount} ignorée(s)."
                    + (summary.Messages.Any() ? Environment.NewLine + string.Join(Environment.NewLine, summary.Messages) : string.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(StrategicPlanningViewModel), "Échec de l'application de la propagation.", ex);
                PropagationStatus = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void CancelPropagation()
        {
            PropagationProposals = new ObservableCollection<ProposedPropagationItem>();
            PropagationStatus = "Propagation annulée.";
        }

        [RelayCommand]
        public void OpenPropagatedStage()
        {
            string? tab = _propagationTarget switch
            {
                LifecycleStage.Cadrage => "Cadrage",
                LifecycleStage.ProblemTree => "ArbreProbleme",
                LifecycleStage.CadreLogique => "CadreLogique",
                _ => null
            };
            if (tab != null) _onNavigateTab?.Invoke(tab);
        }

        [RelayCommand]
        public void ProceedToCadrage()
        {
            if (!ReadinessResult.IsPassed && !ReadinessResult.IsOverridden)
            {
                MessageBox.Show("Veuillez combler les lacunes bloquantes ou saisir une dérogation administrative pour continuer.", "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _onNavigateTab?.Invoke("Cadrage");
        }
    }
}

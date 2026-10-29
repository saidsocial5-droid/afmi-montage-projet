using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Research;
using AFMI_Montage_projet_2.Services.Strategic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFMI_Montage_projet_2.ViewModels.Strategic
{
    /// <summary>
    /// Onglet « Territoire &amp; bénéficiaires » de la Planification stratégique (ADR 25) :
    /// diagnostic de la zone d'intervention et groupes de bénéficiaires directs/indirects,
    /// avec assistance IA et alimentation de l'analyse des besoins.
    /// </summary>
    public partial class TerritoryViewModel : ObservableObject
    {
        private readonly int _projectId;
        private readonly Func<Task>? _onSaved;
        private readonly TerritoryService _service = new();

        [ObservableProperty] private TerritoryProfile _profile = new();
        [ObservableProperty] private BeneficiaryGroup? _selectedBeneficiary;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _totalsText = string.Empty;
        [ObservableProperty] private string _zoneLabel = string.Empty;
        [ObservableProperty] private bool _hasUnsavedChanges;

        public ObservableCollection<BeneficiaryGroup> Beneficiaries { get; } = new();

        public string[] BeneficiaryTypeOptions { get; } = { BeneficiaryTypes.Direct, BeneficiaryTypes.Indirect };

        public string[] CategoryOptions { get; } =
        {
            "Femmes", "Jeunes", "Éleveurs", "Agriculteurs", "Coopératives", "Associations (AGS, ONG locales)", "Ménages",
            "Élèves / écoles", "Artisans", "Opérateurs touristiques", "Institutions / services publics", "Population générale"
        };

        public string[] CountUnitOptions { get; } = { "personnes", "ménages", "organisations", "exploitations", "écoles" };

        public TerritoryViewModel(int projectId, Func<Task>? onSaved = null)
        {
            _projectId = projectId;
            _onSaved = onSaved;
            Beneficiaries.CollectionChanged += (_, _) => RefreshTotals();
            _ = LoadAsync();
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                Profile = await _service.GetOrCreateAsync(_projectId);
                Beneficiaries.Clear();
                foreach (var g in await _service.GetBeneficiariesAsync(_projectId)) Beneficiaries.Add(g);
                ZoneLabel = Profile.ZoneLabel;
                RefreshTotals();
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TerritoryViewModel), "Chargement du territoire impossible.", ex);
                StatusMessage = $"Erreur de chargement : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            if (!Profile.HasLocation)
            {
                StatusMessage = "Indiquez au moins la région, la province ou les communes concernées : c'est la zone d'intervention de tout le projet.";
                return;
            }

            IsBusy = true;
            try
            {
                await _service.SaveAsync(_projectId, Profile, Beneficiaries.ToList(), SessionManager.CurrentUser?.Email ?? "Système");
                await LoadAsync();
                StatusMessage = $"Territoire et bénéficiaires enregistrés. Zone d'intervention du projet : « {ZoneLabel} » (cadrage et socle mis à jour, bénéficiaires directs reportés dans les parties prenantes).";
                if (_onSaved != null) await _onSaved();
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TerritoryViewModel), "Enregistrement du territoire impossible.", ex);
                StatusMessage = $"Erreur d'enregistrement : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddBeneficiary(string? type)
        {
            var group = new BeneficiaryGroup
            {
                ProjectId = _projectId,
                Name = type == BeneficiaryTypes.Indirect ? "Nouveau groupe indirect" : "Nouveau groupe direct",
                BeneficiaryType = type == BeneficiaryTypes.Indirect ? BeneficiaryTypes.Indirect : BeneficiaryTypes.Direct
            };
            Beneficiaries.Add(group);
            SelectedBeneficiary = group;
            HasUnsavedChanges = true;
        }

        [RelayCommand]
        private void RemoveBeneficiary(BeneficiaryGroup? group)
        {
            if (group == null) return;
            Beneficiaries.Remove(group);
            HasUnsavedChanges = true;
            StatusMessage = $"« {group.Name} » sera retiré à l'enregistrement.";
        }

        public void RefreshTotals()
        {
            var totals = TerritoryService.ComputeTotals(Beneficiaries);
            TotalsText = Beneficiaries.Count == 0 ? "Aucun groupe de bénéficiaires." : totals.Summary;
        }

        [RelayCommand]
        private async Task SuggestWithAiAsync()
        {
            IsBusy = true;
            StatusMessage = AiAssistantService.CloudProviderSupportsSearch()
                ? "Diagnostic territorial en cours (IA avec recherche Internet)…"
                : "Diagnostic territorial en cours (IA, sans recherche Internet)…";
            try
            {
                var suggestion = await new TerritoryAiService().SuggestAsync(_projectId, Profile, Beneficiaries.ToList());
                var working = Clone(Profile);
                var result = TerritoryAiService.Apply(suggestion, working, Beneficiaries.ToList());
                Profile = working;
                foreach (var g in result.NewGroups) Beneficiaries.Add(g);
                HasUnsavedChanges = result.FilledFields > 0 || result.NewGroups.Count > 0;
                StatusMessage = "🤖 " + result.Message;
            }
            catch (StructuredAiException ex)
            {
                AppLogger.Warn(nameof(TerritoryViewModel), "Proposition IA du territoire inexploitable.", ex);
                StatusMessage = "L'IA n'a pas renvoyé de proposition exploitable (moteur local arrêté ou réponse invalide). Vérifiez la configuration IA dans les paramètres.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TerritoryViewModel), "Proposition IA du territoire impossible.", ex);
                StatusMessage = $"Erreur IA : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Étape suivante : les besoins des groupes et les problèmes de la zone alimentent l'analyse des besoins.</summary>
        [RelayCommand]
        private async Task FeedNeedsAsync()
        {
            if (HasUnsavedChanges)
            {
                StatusMessage = "Enregistrez d'abord le territoire et les bénéficiaires.";
                return;
            }
            IsBusy = true;
            try
            {
                int added = await _service.FeedNeedsAssessmentAsync(_projectId);
                StatusMessage = added == 0
                    ? "Aucun nouveau besoin à ajouter (déjà présents, ou rubriques « Besoins » et « Problèmes » vides)."
                    : $"{added} besoin(s) ajouté(s) à l'onglet « Diagnostic stratégique » (analyse des besoins), à prioriser.";
                if (added > 0 && _onSaved != null) await _onSaved();
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TerritoryViewModel), "Alimentation des besoins impossible.", ex);
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static TerritoryProfile Clone(TerritoryProfile p) => new()
        {
            Id = p.Id, ProjectId = p.ProjectId, GlobalId = p.GlobalId, Country = p.Country, Region = p.Region, Province = p.Province,
            Communes = p.Communes, Localities = p.Localities, AreaType = p.AreaType, Surface = p.Surface, Altitude = p.Altitude,
            Climate = p.Climate, Ecosystems = p.Ecosystems, NaturalResources = p.NaturalResources, WaterResources = p.WaterResources,
            Population = p.Population, Households = p.Households, PovertyRate = p.PovertyRate, SocialProfile = p.SocialProfile,
            MainLivelihoods = p.MainLivelihoods, InfrastructureAccess = p.InfrastructureAccess, EnvironmentalIssues = p.EnvironmentalIssues,
            SocialIssues = p.SocialIssues, EconomicIssues = p.EconomicIssues, ClimateVulnerabilities = p.ClimateVulnerabilities,
            InstitutionalActors = p.InstitutionalActors, Opportunities = p.Opportunities, Sources = p.Sources, Synthesis = p.Synthesis
        };
    }
}

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Research;
using AFMI_Montage_projet_2.Services.Strategic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFMI_Montage_projet_2.ViewModels
{
    /// <summary>
    /// Projets similaires et idées (ADR 25) : recherche dans les bases Banque mondiale et IATI (d-portal)
    /// et, si disponible, recherche web IA ; extraction d'idées puis intégration sélective au projet.
    /// </summary>
    public partial class SimilarProjectsViewModel : ObservableObject
    {
        private readonly int _projectId;
        private readonly SimilarProjectsService _service = new();
        private bool _queryInitialized;

        [ObservableProperty] private string _keywords = string.Empty;
        [ObservableProperty] private bool _moroccoOnly = true;
        [ObservableProperty] private bool _useAiWebSearch;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Les mots-clés sont proposés à partir du projet (en anglais, langue des bases internationales). Ajustez-les si besoin puis lancez la recherche.";
        [ObservableProperty] private string _sourcesSummary = string.Empty;

        public bool AiWebSearchAvailable { get; } = AiAssistantService.CloudProviderSupportsSearch();

        public ObservableCollection<SimilarProject> Results { get; } = new();
        public ObservableCollection<IntegrationProposal> Proposals { get; } = new();

        public bool HasProposals => Proposals.Count > 0;

        public SimilarProjectsViewModel(int projectId)
        {
            _projectId = projectId;
            UseAiWebSearch = AiWebSearchAvailable;
            Proposals.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProposals));
            _ = InitializeQueryAsync();
        }

        private async Task InitializeQueryAsync()
        {
            try
            {
                var query = await _service.BuildDefaultQueryAsync(_projectId);
                Keywords = query.Keywords;
                MoroccoOnly = query.CountryCode == "MA";
                _queryInitialized = true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(SimilarProjectsViewModel), "Mots-clés par défaut indisponibles.", ex);
                Keywords = "forest";
            }
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(Keywords))
            {
                StatusMessage = "Saisissez au moins un mot-clé (ex. forest rangeland women).";
                return;
            }
            if (!_queryInitialized && Keywords == string.Empty) await InitializeQueryAsync();

            IsBusy = true;
            StatusMessage = "Recherche en cours dans les bases de projets…";
            Results.Clear();
            Proposals.Clear();
            try
            {
                var outcome = await _service.SearchAsync(_projectId, new SimilarProjectsQuery
                {
                    Keywords = Keywords.Trim(),
                    CountryCode = MoroccoOnly ? "MA" : string.Empty,
                    UseAiWebSearch = UseAiWebSearch && AiWebSearchAvailable
                });

                foreach (var p in outcome.Projects) Results.Add(p);
                foreach (var p in Results.Take(5).Where(p => p.Relevance >= 0.25)) p.IsSelected = true;
                SourcesSummary = string.Join(" • ", outcome.Messages);
                StatusMessage = Results.Count == 0
                    ? (MoroccoOnly ? "Aucun projet trouvé au Maroc : décochez « Maroc uniquement » ou élargissez les mots-clés." : "Aucun projet trouvé : essayez d'autres mots-clés.")
                    : $"{Results.Count} projet(s) trouvé(s), classés par pertinence. Les plus pertinents sont présélectionnés : ajustez puis « Extraire des idées ».";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(SimilarProjectsViewModel), "Recherche de projets similaires impossible.", ex);
                StatusMessage = $"Recherche impossible : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ExtractIdeasAsync()
        {
            var selected = Results.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "Cochez au moins un projet similaire.";
                return;
            }

            IsBusy = true;
            StatusMessage = $"Analyse de {selected.Count} projet(s) par l'IA : leçons, activités, risques, partenaires…";
            Proposals.Clear();
            try
            {
                var ideas = await _service.ExtractIdeasAsync(_projectId, selected);
                foreach (var p in SimilarProjectsService.ToProposals(ideas, selected)) Proposals.Add(p);
                StatusMessage = $"{ideas.Activities.Count} activité(s), {ideas.Risks.Count} risque(s), {ideas.Partners.Count} partenaire(s), {ideas.Approaches.Count} approche(s) et {ideas.Lessons.Count + ideas.GoodPractices.Count} enseignement(s) proposés. Cochez ce que vous retenez puis « Intégrer au projet ».";
            }
            catch (StructuredAiException ex)
            {
                AppLogger.Warn(nameof(SimilarProjectsViewModel), "Extraction d'idées inexploitable.", ex);
                foreach (var p in SimilarProjectsService.ToProposals(new ProjectIdeas(), selected)) Proposals.Add(p);
                StatusMessage = "L'IA n'a pas renvoyé d'idées exploitables (moteur arrêté ou réponse invalide). Les projets retenus peuvent tout de même être enregistrés comme sources.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(SimilarProjectsViewModel), "Extraction d'idées impossible.", ex);
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task IntegrateAsync()
        {
            if (!Proposals.Any(p => p.IsSelected))
            {
                StatusMessage = "Cochez au moins une proposition.";
                return;
            }
            IsBusy = true;
            try
            {
                var result = await new ProjectIntegrationService().ApplyAsync(_projectId, Proposals, SessionManager.CurrentUser?.Email ?? "Système");
                StatusMessage = $"✔ {result.Summary} Les activités sont ajoutées à la théorie du changement (puis propageables vers le cadre logique), les risques et options au socle stratégique, les partenaires aux parties prenantes.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(SimilarProjectsViewModel), "Intégration des idées impossible.", ex);
                StatusMessage = $"Intégration impossible : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void OpenProject(SimilarProject? project)
        {
            if (project == null || !Uri.TryCreate(project.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)) return;
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                AppLogger.Warn(nameof(SimilarProjectsViewModel), $"Ouverture du lien impossible : {uri}", ex);
                StatusMessage = "Impossible d'ouvrir le navigateur.";
            }
        }
    }
}

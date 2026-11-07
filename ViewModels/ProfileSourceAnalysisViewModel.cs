using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ProfileSourceAnalysisViewModel : ObservableObject
    {
        private readonly OrganizationProfileViewModel _parentViewModel;
        private readonly ProfileSourceAnalysisService _analysisService;

        #region Sources (Documents & Liens)

        [ObservableProperty]
        private ObservableCollection<AnalysisSourceItem> _documents = new();

        [ObservableProperty]
        private ObservableCollection<AnalysisSourceItem> _links = new();

        [ObservableProperty]
        private string _newUrlInput = string.Empty;

        [ObservableProperty]
        private string _pastedText = string.Empty;

        [ObservableProperty]
        private string _userFocus = string.Empty;

        #endregion

        #region État d'Analyse & Prévisualisation

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string _statusMessage = "Sélectionnez vos documents, ajoutez des liens web/réseaux sociaux ou lancez une analyse individuelle/globale.";

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        // Prévisualisation du texte brut extrait
        [ObservableProperty]
        private bool _isPreviewOpen;

        [ObservableProperty]
        private string _previewTitle = string.Empty;

        [ObservableProperty]
        private string _previewContent = string.Empty;

        #endregion

        #region Propositions d'Enrichissement & Filtres

        [ObservableProperty]
        private ObservableCollection<ProfileEnrichmentProposal> _allProposals = new();

        [ObservableProperty]
        private ObservableCollection<ProfileEnrichmentProposal> _filteredProposals = new();

        [ObservableProperty]
        private ObservableCollection<string> _sourceFilterOptions = new();

        [ObservableProperty]
        private string _selectedSourceFilter = "Toutes les sources";

        [ObservableProperty]
        private bool _hasProposals;

        #endregion

        public event Action? RequestClose;

        public ProfileSourceAnalysisViewModel(OrganizationProfileViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
            _analysisService = new ProfileSourceAnalysisService();

            InitializeDefaultLinks();
            UpdateSourceFilters();
        }

        private void InitializeDefaultLinks()
        {
            if (!string.IsNullOrWhiteSpace(_parentViewModel.Website))
            {
                AddLinkItem(_parentViewModel.Website, SourceType.WebLink, "Site officiel AFMI");
            }
            if (!string.IsNullOrWhiteSpace(_parentViewModel.FacebookUrl))
            {
                AddLinkItem(_parentViewModel.FacebookUrl, SourceType.SocialMedia, "Page Facebook officielle");
            }
            if (!string.IsNullOrWhiteSpace(_parentViewModel.LinkedInUrl))
            {
                AddLinkItem(_parentViewModel.LinkedInUrl, SourceType.SocialMedia, "Profil LinkedIn institutionnel");
            }
            if (!string.IsNullOrWhiteSpace(_parentViewModel.TwitterUrl))
            {
                AddLinkItem(_parentViewModel.TwitterUrl, SourceType.SocialMedia, "Compte X / Twitter");
            }
            if (!string.IsNullOrWhiteSpace(_parentViewModel.YouTubeUrl))
            {
                AddLinkItem(_parentViewModel.YouTubeUrl, SourceType.SocialMedia, "Chaîne YouTube");
            }
        }

        private void AddLinkItem(string url, SourceType type, string? customTitle = null)
        {
            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            if (Links.Any(l => string.Equals(l.PathOrUrl, url, StringComparison.OrdinalIgnoreCase)))
                return;

            string title = customTitle ?? CleanUrlTitle(url);

            Links.Add(new AnalysisSourceItem
            {
                Title = title,
                PathOrUrl = url,
                Type = type,
                Status = SourceAnalysisStatus.Pending,
                StatusMessage = "Prêt pour analyse"
            });
        }

        private static string CleanUrlTitle(string url)
        {
            try
            {
                var uri = new Uri(url);
                string host = uri.Host.Replace("www.", "");
                string path = uri.AbsolutePath.Trim('/');
                if (!string.IsNullOrEmpty(path) && path.Length < 30)
                    return $"{host}/{path}";
                return host;
            }
            catch
            {
                return url;
            }
        }

        #region Gestion des Fichiers

        [RelayCommand]
        private void BrowseFiles()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Sélectionner des documents institutionnels (Rapports, Statuts, Audits, Projets...)",
                Filter = "Documents supportés (*.pdf;*.docx;*.xlsx;*.txt;*.md)|*.pdf;*.docx;*.xlsx;*.txt;*.md|Fichiers PDF (*.pdf)|*.pdf|Documents Word (*.docx)|*.docx|Tableurs Excel (*.xlsx)|*.xlsx|Fichiers Texte (*.txt;*.md)|*.txt;*.md",
                Multiselect = true,
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                int countAdded = 0;
                foreach (var file in ofd.FileNames)
                {
                    if (Documents.Any(d => string.Equals(d.PathOrUrl, file, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    string sizeText = string.Empty;
                    try
                    {
                        var fi = new FileInfo(file);
                        sizeText = fi.Length >= 1024 * 1024
                            ? $"{(fi.Length / (1024.0 * 1024.0)):F1} Mo"
                            : $"{(fi.Length / 1024.0):F0} Ko";
                    }
                    catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(ProfileSourceAnalysisViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }

                    Documents.Add(new AnalysisSourceItem
                    {
                        Title = Path.GetFileName(file),
                        PathOrUrl = file,
                        Type = SourceType.Document,
                        FileSizeText = sizeText,
                        Status = SourceAnalysisStatus.Pending,
                        StatusMessage = "Prêt pour analyse"
                    });
                    countAdded++;
                }

                if (countAdded > 0)
                {
                    StatusMessage = $"{countAdded} document(s) ajouté(s). Vous pouvez analyser chaque fichier individuellement ou tous ensemble.";
                }
            }
        }

        [RelayCommand]
        private void RemoveDocument(AnalysisSourceItem? item)
        {
            if (item != null && Documents.Contains(item))
            {
                Documents.Remove(item);
                RemoveProposalsForSource(item.Title);
            }
        }

        #endregion

        #region Gestion des Liens Web & Réseaux Sociaux

        [RelayCommand]
        private void AddWebUrl()
        {
            if (string.IsNullOrWhiteSpace(NewUrlInput)) return;

            string url = NewUrlInput.Trim();
            AddLinkItem(url, SourceType.WebLink);
            NewUrlInput = string.Empty;
            StatusMessage = "Lien ajouté avec succès. Cliquez sur '⚡ Analyser' pour lancer son extraction individuelle.";
        }

        [RelayCommand]
        private void RemoveLink(AnalysisSourceItem? item)
        {
            if (item != null && Links.Contains(item))
            {
                Links.Remove(item);
                RemoveProposalsForSource(item.Title);
            }
        }

        [RelayCommand]
        private void AddQuickPreset(string preset)
        {
            string url = preset switch
            {
                "Facebook" => string.IsNullOrWhiteSpace(_parentViewModel.FacebookUrl) ? "https://www.facebook.com/foretmodeleifrane" : _parentViewModel.FacebookUrl,
                "LinkedIn" => string.IsNullOrWhiteSpace(_parentViewModel.LinkedInUrl) ? "https://www.linkedin.com/company/afmi-ifrane" : _parentViewModel.LinkedInUrl,
                "Twitter" => string.IsNullOrWhiteSpace(_parentViewModel.TwitterUrl) ? "https://x.com/afmi_ifrane" : _parentViewModel.TwitterUrl,
                "Website" => string.IsNullOrWhiteSpace(_parentViewModel.Website) ? "https://www.foretmodele-ifrane.ma" : _parentViewModel.Website,
                "YouTube" => string.IsNullOrWhiteSpace(_parentViewModel.YouTubeUrl) ? "https://www.youtube.com/@foretmodeleifrane" : _parentViewModel.YouTubeUrl,
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(url))
            {
                AddLinkItem(url, SourceType.SocialMedia);
                StatusMessage = $"Lien preset ajouté : {url}";
            }
        }

        [RelayCommand]
        private void OpenLinkInBrowser(AnalysisSourceItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.PathOrUrl)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.PathOrUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le lien : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Analyse Individuelle (Document par Document / Lien par Lien)

        [RelayCommand]
        public async Task AnalyzeSingleDocumentAsync(AnalysisSourceItem? item)
        {
            if (item == null) return;

            item.IsAnalyzing = true;
            item.Status = SourceAnalysisStatus.Analyzing;
            item.StatusMessage = "Extraction du texte...";
            IsAnalyzing = true;
            StatusMessage = $"Analyse individuelle en cours : {item.Title}...";

            try
            {
                // 1. Extraction du texte
                if (string.IsNullOrWhiteSpace(item.ExtractedText))
                {
                    var (success, content, error) = _analysisService.ExtractTextFromFile(item.PathOrUrl);
                    if (!success || string.IsNullOrWhiteSpace(content))
                    {
                        item.Status = SourceAnalysisStatus.Error;
                        item.StatusMessage = $"Échec extraction : {error}";
                        StatusMessage = $"Erreur lors de la lecture de {item.Title} : {error}";
                        return;
                    }
                    item.ExtractedText = content;
                }

                // 2. Analyse IA de ce document seul
                item.StatusMessage = "Interrogation de l'IA...";
                var singleCorpus = new List<(string SourceTitle, string Content)>
                {
                    (item.Title, item.ExtractedText)
                };

                var currentProfile = _parentViewModel.BuildProfileFromProperties();
                var proposals = await _analysisService.AnalyzeCorpusAsync(singleCorpus, currentProfile, UserFocus);

                // 3. Intégration des propositions
                RemoveProposalsForSource(item.Title);
                foreach (var p in proposals)
                {
                    p.SourceOrigin = item.Title;
                    AllProposals.Add(p);
                }

                item.Status = SourceAnalysisStatus.Success;
                item.ProposalsCount = proposals.Count;
                item.AnalyzedAt = DateTime.Now;
                item.StatusMessage = $"{proposals.Count} proposition(s) extraite(s)";

                UpdateSourceFilters();
                ApplyFilter();

                StatusMessage = $"✅ Analyse terminée pour '{item.Title}' : {proposals.Count} proposition(s) trouvée(s).";
                if (proposals.Count > 0)
                {
                    SelectedTabIndex = 1; // Basculer sur les résultats
                }
            }
            catch (Exception ex)
            {
                item.Status = SourceAnalysisStatus.Error;
                item.StatusMessage = $"Erreur : {ex.Message}";
                StatusMessage = $"Erreur d'analyse pour {item.Title} : {ex.Message}";
            }
            finally
            {
                item.IsAnalyzing = false;
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        public async Task AnalyzeSingleLinkAsync(AnalysisSourceItem? item)
        {
            if (item == null) return;

            item.IsAnalyzing = true;
            item.Status = SourceAnalysisStatus.Analyzing;
            item.StatusMessage = "Téléchargement de la page web...";
            IsAnalyzing = true;
            StatusMessage = $"Récupération du contenu en ligne : {item.PathOrUrl}...";

            try
            {
                // 1. Télécharger et extraire
                if (string.IsNullOrWhiteSpace(item.ExtractedText))
                {
                    var (success, content, error) = await _analysisService.FetchUrlContentAsync(item.PathOrUrl);
                    if (!success || string.IsNullOrWhiteSpace(content))
                    {
                        item.Status = SourceAnalysisStatus.Error;
                        item.StatusMessage = $"Échec web : {error}";
                        StatusMessage = $"Erreur pour {item.PathOrUrl} : {error}";
                        return;
                    }
                    item.ExtractedText = content;
                }

                // 2. Analyse IA de ce lien seul
                item.StatusMessage = "Analyse IA du contenu web...";
                var singleCorpus = new List<(string SourceTitle, string Content)>
                {
                    (item.Title, item.ExtractedText)
                };

                var currentProfile = _parentViewModel.BuildProfileFromProperties();
                var proposals = await _analysisService.AnalyzeCorpusAsync(singleCorpus, currentProfile, UserFocus);

                // 3. Intégration
                RemoveProposalsForSource(item.Title);
                foreach (var p in proposals)
                {
                    p.SourceOrigin = item.Title;
                    AllProposals.Add(p);
                }

                item.Status = SourceAnalysisStatus.Success;
                item.ProposalsCount = proposals.Count;
                item.AnalyzedAt = DateTime.Now;
                item.StatusMessage = $"{proposals.Count} proposition(s) extraite(s)";

                UpdateSourceFilters();
                ApplyFilter();

                StatusMessage = $"✅ Analyse terminée pour '{item.Title}' : {proposals.Count} proposition(s) identifiée(s).";
                if (proposals.Count > 0)
                {
                    SelectedTabIndex = 1;
                }
            }
            catch (Exception ex)
            {
                item.Status = SourceAnalysisStatus.Error;
                item.StatusMessage = $"Erreur : {ex.Message}";
                StatusMessage = $"Erreur d'analyse pour {item.Title} : {ex.Message}";
            }
            finally
            {
                item.IsAnalyzing = false;
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        public async Task AnalyzePastedTextAsync()
        {
            if (string.IsNullOrWhiteSpace(PastedText))
            {
                MessageBox.Show("Veuillez d'abord coller un texte ou un extrait de publication dans la zone prévue.", "Texte vide", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsAnalyzing = true;
            StatusMessage = "Analyse IA de l'extrait textuel / publication...";

            try
            {
                string sourceTitle = "Publication / Extrait Collé";
                var singleCorpus = new List<(string SourceTitle, string Content)>
                {
                    (sourceTitle, PastedText.Trim())
                };

                var currentProfile = _parentViewModel.BuildProfileFromProperties();
                var proposals = await _analysisService.AnalyzeCorpusAsync(singleCorpus, currentProfile, UserFocus);

                RemoveProposalsForSource(sourceTitle);
                foreach (var p in proposals)
                {
                    p.SourceOrigin = sourceTitle;
                    AllProposals.Add(p);
                }

                UpdateSourceFilters();
                ApplyFilter();

                StatusMessage = $"✅ Analyse de l'extrait terminée : {proposals.Count} proposition(s) trouvée(s).";
                if (proposals.Count > 0)
                {
                    SelectedTabIndex = 1;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
                MessageBox.Show($"Erreur d'analyse : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        #endregion

        #region Analyse Globale de Toutes les Sources

        [RelayCommand]
        public async Task LaunchGlobalAnalysisAsync()
        {
            if (Documents.Count == 0 && Links.Count == 0 && string.IsNullOrWhiteSpace(PastedText))
            {
                MessageBox.Show("Veuillez ajouter au moins un document ou lien web avant de lancer l'analyse globale.", "Aucune source", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsAnalyzing = true;
            AllProposals.Clear();

            try
            {
                var corpus = new List<(string SourceTitle, string Content)>();

                // 1. Extraire les documents
                foreach (var doc in Documents)
                {
                    doc.Status = SourceAnalysisStatus.Analyzing;
                    doc.StatusMessage = "Extraction en cours...";
                    StatusMessage = $"Extraction du document : {doc.Title}...";

                    if (string.IsNullOrWhiteSpace(doc.ExtractedText))
                    {
                        var (success, content, error) = _analysisService.ExtractTextFromFile(doc.PathOrUrl);
                        if (success && !string.IsNullOrWhiteSpace(content))
                        {
                            doc.ExtractedText = content;
                        }
                        else
                        {
                            doc.Status = SourceAnalysisStatus.Error;
                            doc.StatusMessage = error;
                            continue;
                        }
                    }

                    corpus.Add((doc.Title, doc.ExtractedText));
                }

                // 2. Extraire les liens
                foreach (var link in Links)
                {
                    link.Status = SourceAnalysisStatus.Analyzing;
                    link.StatusMessage = "Téléchargement web...";
                    StatusMessage = $"Récupération : {link.Title}...";

                    if (string.IsNullOrWhiteSpace(link.ExtractedText))
                    {
                        var (success, content, error) = await _analysisService.FetchUrlContentAsync(link.PathOrUrl);
                        if (success && !string.IsNullOrWhiteSpace(content))
                        {
                            link.ExtractedText = content;
                        }
                        else
                        {
                            link.Status = SourceAnalysisStatus.Error;
                            link.StatusMessage = error;
                            continue;
                        }
                    }

                    corpus.Add((link.Title, link.ExtractedText));
                }

                // 3. Texte collé
                if (!string.IsNullOrWhiteSpace(PastedText))
                {
                    corpus.Add(("Publication / Extrait Collé", PastedText.Trim()));
                }

                if (corpus.Count == 0)
                {
                    StatusMessage = "Aucun texte n'a pu être extrait des sources.";
                    MessageBox.Show("Impossible d'extraire du contenu textuel.", "Avertissement", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 4. Analyse IA globale
                StatusMessage = "🧠 Analyse IA globale approfondie et synthèse comparative avec le profil...";
                var currentProfile = _parentViewModel.BuildProfileFromProperties();
                var results = await _analysisService.AnalyzeCorpusAsync(corpus, currentProfile, UserFocus);

                foreach (var p in results)
                {
                    AllProposals.Add(p);
                }

                // Mettre à jour l'état de chaque source
                foreach (var doc in Documents)
                {
                    if (doc.Status == SourceAnalysisStatus.Analyzing)
                    {
                        int docProposals = AllProposals.Count(p => string.Equals(p.SourceOrigin, doc.Title, StringComparison.OrdinalIgnoreCase));
                        doc.Status = SourceAnalysisStatus.Success;
                        doc.ProposalsCount = docProposals;
                        doc.StatusMessage = $"{docProposals} suggestion(s)";
                        doc.AnalyzedAt = DateTime.Now;
                    }
                }

                foreach (var link in Links)
                {
                    if (link.Status == SourceAnalysisStatus.Analyzing)
                    {
                        int linkProposals = AllProposals.Count(p => string.Equals(p.SourceOrigin, link.Title, StringComparison.OrdinalIgnoreCase));
                        link.Status = SourceAnalysisStatus.Success;
                        link.ProposalsCount = linkProposals;
                        link.StatusMessage = $"{linkProposals} suggestion(s)";
                        link.AnalyzedAt = DateTime.Now;
                    }
                }

                UpdateSourceFilters();
                ApplyFilter();

                if (AllProposals.Count > 0)
                {
                    SelectedTabIndex = 1;
                    StatusMessage = $"✅ Analyse globale terminée : {AllProposals.Count} proposition(s) d'amélioration détectée(s).";
                }
                else
                {
                    StatusMessage = "Analyse terminée : Aucune nouvelle information majeure détectée.";
                    MessageBox.Show("Aucune nouvelle information significative n'a été détectée pour enrichir le profil.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'analyse : {ex.Message}";
                MessageBox.Show($"Erreur lors de l'analyse : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        #endregion

        #region Prévisualisation du Texte Extrait

        [RelayCommand]
        public void PreviewSourceText(AnalysisSourceItem? item)
        {
            if (item == null) return;

            // Si le texte n'a pas encore été extrait, l'extraire maintenant
            if (string.IsNullOrWhiteSpace(item.ExtractedText))
            {
                if (item.Type == SourceType.Document)
                {
                    var (success, content, error) = _analysisService.ExtractTextFromFile(item.PathOrUrl);
                    if (success) item.ExtractedText = content;
                    else
                    {
                        MessageBox.Show($"Erreur d'extraction : {error}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Le contenu web n'a pas encore été téléchargé. Cliquez d'abord sur '⚡ Analyser' pour le charger.", "Non chargé", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            PreviewTitle = $"Contenu extrait : {item.Title}";
            PreviewContent = item.ExtractedText;
            IsPreviewOpen = true;
        }

        [RelayCommand]
        public void ClosePreview()
        {
            IsPreviewOpen = false;
            PreviewContent = string.Empty;
        }

        #endregion

        #region Filtrage des Propositions

        private void RemoveProposalsForSource(string sourceTitle)
        {
            var toRemove = AllProposals.Where(p => string.Equals(p.SourceOrigin, sourceTitle, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var r in toRemove)
            {
                AllProposals.Remove(r);
            }
            UpdateSourceFilters();
            ApplyFilter();
        }

        private void UpdateSourceFilters()
        {
            var distinctSources = AllProposals.Select(p => p.SourceOrigin).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            var list = new List<string>
            {
                $"Toutes les sources ({AllProposals.Count})"
            };

            foreach (var s in distinctSources)
            {
                int c = AllProposals.Count(p => string.Equals(p.SourceOrigin, s, StringComparison.OrdinalIgnoreCase));
                list.Add($"{s} ({c})");
            }

            SourceFilterOptions = new ObservableCollection<string>(list);

            if (!SourceFilterOptions.Contains(SelectedSourceFilter))
            {
                SelectedSourceFilter = SourceFilterOptions.FirstOrDefault() ?? "Toutes les sources";
            }
        }

        partial void OnSelectedSourceFilterChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SelectedSourceFilter) || SelectedSourceFilter.StartsWith("Toutes les sources", StringComparison.OrdinalIgnoreCase))
            {
                FilteredProposals = new ObservableCollection<ProfileEnrichmentProposal>(AllProposals);
            }
            else
            {
                string rawSource = SelectedSourceFilter;
                int parenIdx = rawSource.LastIndexOf('(');
                if (parenIdx > 0)
                {
                    rawSource = rawSource.Substring(0, parenIdx).Trim();
                }

                var filtered = AllProposals.Where(p => string.Equals(p.SourceOrigin, rawSource, StringComparison.OrdinalIgnoreCase)).ToList();
                FilteredProposals = new ObservableCollection<ProfileEnrichmentProposal>(filtered);
            }

            HasProposals = AllProposals.Count > 0;
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var p in FilteredProposals) p.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var p in FilteredProposals) p.IsSelected = false;
        }

        [RelayCommand]
        private void ApplySelectedProposals()
        {
            var selected = AllProposals.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Veuillez cocher au moins une proposition à appliquer.", "Sélection vide", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int countApplied = 0;
            var appliedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in selected)
            {
                _parentViewModel.ApplyProposal(prop);
                countApplied++;
                if (!string.IsNullOrWhiteSpace(prop.SourceOrigin))
                {
                    appliedSources.Add(prop.SourceOrigin);
                }
            }

            foreach (var src in appliedSources)
            {
                string historyEntry = $"{DateTime.Now:dd/MM/yyyy HH:mm} — Source analysée : {src}";
                _parentViewModel.AddAnalyzedSource(historyEntry);
            }

            _parentViewModel.StatusMessage = $"✅ {countApplied} modification(s) injectée(s) avec succès dans le profil.";

            MessageBox.Show($"{countApplied} amélioration(s) ont été intégrées dans les onglets du profil de l'Association.\n\nPensez à cliquer sur '💾 Enregistrer' pour pérenniser les changements.", "Modifications appliquées", MessageBoxButton.OK, MessageBoxImage.Information);

            RequestClose?.Invoke();
        }

        #endregion
    }
}

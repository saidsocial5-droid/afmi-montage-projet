using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Research;

namespace AFMI_Montage_projet_2.ViewModels
{
    /// <summary>
    /// Documents et liens du projet (ADR 25) : analyse structurée (appel à projets, étude, données),
    /// vérification des chiffres dans la source, intégration sélective au projet ; recherche de projets similaires.
    /// </summary>
    public partial class AnalyseDocumentsViewModel : ObservableObject
    {
        private readonly int _projectId;
        private readonly DocumentIntelligenceService _intelligence = new();

        [ObservableProperty]
        private ObservableCollection<ProjectDocument> _documents = new();

        [ObservableProperty]
        private ProjectDocument? _selectedDocument;

        [ObservableProperty]
        private bool _isAnalyzing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _globalRequirementsSummary = string.Empty;

        /// <summary>Informations du document sélectionné proposées à l'intégration dans le projet.</summary>
        public ObservableCollection<IntegrationProposal> Proposals { get; } = new();

        public bool HasProposals => Proposals.Count > 0;

        /// <summary>Recherche de projets similaires (second onglet).</summary>
        public SimilarProjectsViewModel SimilarProjects { get; }

        public AnalyseDocumentsViewModel(int projectId)
        {
            _projectId = projectId;
            SimilarProjects = new SimilarProjectsViewModel(projectId);
            Proposals.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProposals));
            LoadDocuments();
        }

        private void LoadDocuments(int? selectId = null)
        {
            using var db = new AppDbContext();
            var docs = db.ProjectDocuments
                .Where(d => d.ProjectId == _projectId)
                .OrderByDescending(d => d.UploadedAt)
                .ToList();

            Documents = new ObservableCollection<ProjectDocument>(docs);
            GlobalRequirementsSummary = db.Projects.Where(p => p.Id == _projectId).Select(p => p.GlobalRequirementsSummary).FirstOrDefault() ?? string.Empty;
            if (selectId.HasValue) SelectedDocument = Documents.FirstOrDefault(d => d.Id == selectId.Value);
        }

        partial void OnSelectedDocumentChanged(ProjectDocument? value)
        {
            Proposals.Clear();
            if (value == null || string.IsNullOrWhiteSpace(value.InsightsJson)) return;
            try
            {
                var insights = JsonSerializer.Deserialize<DocumentInsights>(value.InsightsJson);
                if (insights == null) return;
                foreach (var p in ProjectIntegrationService.FromDocument(insights, value.FileName, value.FilePath, DocumentIntelligenceService.IsWebDocument(value)))
                    Proposals.Add(p);
            }
            catch (JsonException ex)
            {
                AppLogger.Warn(nameof(AnalyseDocumentsViewModel), $"Analyse enregistrée illisible pour {value.FileName}.", ex);
            }
        }

        [RelayCommand]
        private void BrowseFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Sélectionner un document (appel à projets, étude, monographie, données…)",
                Filter = "Documents (*.pdf;*.docx;*.xlsx;*.txt;*.md;*.csv;*.html)|*.pdf;*.docx;*.xlsx;*.txt;*.md;*.csv;*.html;*.htm|Tous les fichiers (*.*)|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var doc = new ProjectDocument
                {
                    ProjectId = _projectId,
                    FileName = Path.GetFileName(openFileDialog.FileName),
                    FilePath = openFileDialog.FileName,
                    DocumentType = "Document",
                    UploadedAt = DateTime.Now
                };

                using var db = new AppDbContext();
                db.ProjectDocuments.Add(doc);
                db.SaveChanges();

                Documents.Insert(0, doc);
                SelectedDocument = doc;
                StatusMessage = "Document ajouté : cliquez sur « Analyser » pour en extraire les informations utiles au projet.";
            }
        }

        [RelayCommand]
        private void AddUrl()
        {
            var window = new Window
            {
                Title = "Ajouter un lien web",
                Width = 520,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Adresse de l'appel à projets, de l'étude, de la page ou du PDF en ligne :", Margin = new Thickness(0, 0, 0, 10) });
            var textBox = new System.Windows.Controls.TextBox { Height = 25 };
            Helpers.AiTextAssistant.SetIsExcluded(textBox, true);
            stack.Children.Add(textBox);

            var btnStack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var okBtn = new System.Windows.Controls.Button { Content = "OK", Width = 70, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            var cancelBtn = new System.Windows.Controls.Button { Content = "Annuler", Width = 70, IsCancel = true };
            okBtn.Click += (s, e) => { window.DialogResult = true; window.Close(); };
            btnStack.Children.Add(okBtn);
            btnStack.Children.Add(cancelBtn);
            stack.Children.Add(btnStack);
            window.Content = stack;

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(textBox.Text)) return;

            string url = textBox.Text.Trim();
            if (!url.Contains("://")) url = "https://" + url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                StatusMessage = "Adresse invalide : seules les adresses http(s) sont acceptées.";
                return;
            }

            var doc = new ProjectDocument
            {
                ProjectId = _projectId,
                FileName = uri.Host + uri.AbsolutePath.TrimEnd('/'),
                FilePath = uri.AbsoluteUri,
                DocumentType = "Lien Web",
                UploadedAt = DateTime.Now
            };

            using var db = new AppDbContext();
            db.ProjectDocuments.Add(doc);
            db.SaveChanges();

            Documents.Insert(0, doc);
            SelectedDocument = doc;
            StatusMessage = "Lien ajouté : cliquez sur « Analyser » (pages web, PDF et Word en ligne acceptés).";
        }

        [RelayCommand]
        private Task AnalyzeDocument(ProjectDocument? doc) => RunAnalysisAsync(doc, forceRefresh: false);

        [RelayCommand]
        private Task ReanalyzeDocument(ProjectDocument? doc) => RunAnalysisAsync(doc, forceRefresh: true);

        private async Task RunAnalysisAsync(ProjectDocument? doc, bool forceRefresh)
        {
            if (doc == null || IsAnalyzing) return;
            IsAnalyzing = true;
            StatusMessage = "Lecture du document et analyse en cours…";
            try
            {
                string context = ProjectContextBuilder.BuildFullContext(_projectId);
                var outcome = await _intelligence.AnalyzeAsync(doc.Id, context, forceRefresh);
                LoadDocuments(doc.Id);
                Proposals.Clear();
                foreach (var p in outcome.Proposals) Proposals.Add(p);
                StatusMessage = $"{outcome.Message} {outcome.Proposals.Count} information(s) proposée(s) à l'intégration : cochez celles à retenir puis « Intégrer au projet ».";
            }
            catch (FileNotFoundException)
            {
                StatusMessage = "Le fichier n'existe plus à cet emplacement : supprimez cette entrée et ajoutez à nouveau le document.";
            }
            catch (Exception ex) when (ex is SafeFetchException or System.Net.Http.HttpRequestException or InvalidOperationException or NotSupportedException or IOException)
            {
                AppLogger.Warn(nameof(AnalyseDocumentsViewModel), $"Analyse impossible : {doc.FilePath}", ex);
                StatusMessage = $"Analyse impossible : {ex.Message}";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AnalyseDocumentsViewModel), $"Erreur inattendue lors de l'analyse de {doc.FilePath}.", ex);
                StatusMessage = $"Erreur inattendue : {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        /// <summary>Intègre au projet les informations cochées (socle stratégique, territoire, bénéficiaires…).</summary>
        [RelayCommand]
        private async Task IntegrateSelectedAsync()
        {
            if (SelectedDocument == null || !Proposals.Any(p => p.IsSelected))
            {
                StatusMessage = "Cochez au moins une information à intégrer.";
                return;
            }

            IsAnalyzing = true;
            try
            {
                var result = await new ProjectIntegrationService().ApplyAsync(_projectId, Proposals, SessionManager.CurrentUser?.Email ?? "Système");
                using (var db = new AppDbContext())
                {
                    var doc = db.ProjectDocuments.FirstOrDefault(d => d.Id == SelectedDocument.Id);
                    if (doc != null)
                    {
                        doc.IntegratedAt = DateTime.UtcNow;
                        db.SaveChanges();
                    }
                }
                StatusMessage = $"✔ {result.Summary} Retrouvez-les dans la Planification stratégique (sources, indicateurs de contexte, besoins, SWOT) et dans « Territoire & bénéficiaires ».";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AnalyseDocumentsViewModel), "Intégration au projet impossible.", ex);
                StatusMessage = $"Intégration impossible : {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        [RelayCommand]
        private void SelectAllProposals(string? value)
        {
            bool select = value != "false";
            foreach (var p in Proposals) p.IsSelected = select;
        }

        [RelayCommand]
        private void DeleteDocument(ProjectDocument doc)
        {
            if (doc == null) return;

            if (MessageBox.Show("Retirer ce document de la liste ? (les informations déjà intégrées au projet sont conservées)", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using var db = new AppDbContext();
                var tracked = db.ProjectDocuments.FirstOrDefault(d => d.Id == doc.Id);
                if (tracked != null)
                {
                    db.ProjectDocuments.Remove(tracked);
                    db.SaveChanges();
                }

                Documents.Remove(doc);
                if (SelectedDocument == doc)
                {
                    SelectedDocument = null;
                }
            }
        }
    }
}

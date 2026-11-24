using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class NotebookLmViewModel : ObservableObject
    {
        private readonly OrganizationProfileViewModel _parentViewModel;

        [ObservableProperty]
        private string _notebookLmUrl = NotebookLmService.DefaultNotebookLmUrl;

        [ObservableProperty]
        private string _sourceMarkdownPreview = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<NotebookLmPromptPreset> _prompts = new();

        [ObservableProperty]
        private NotebookLmPromptPreset? _selectedPrompt;


        public NotebookLmViewModel(OrganizationProfileViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));

            if (!string.IsNullOrWhiteSpace(_parentViewModel.NotebookLmUrl))
            {
                NotebookLmUrl = _parentViewModel.NotebookLmUrl;
            }

            Prompts = new ObservableCollection<NotebookLmPromptPreset>(NotebookLmService.GetRecommendedPrompts());
            if (Prompts.Count > 0)
            {
                SelectedPrompt = Prompts[0];
            }

            GeneratePreview();
        }

        public void GeneratePreview()
        {
            var profile = _parentViewModel.BuildProfileFromProperties();
            SourceMarkdownPreview = NotebookLmService.GenerateKnowledgeBaseMarkdown(profile);
            StatusMessage = "Base de connaissances prête pour NotebookLM.";
        }

        [RelayCommand]
        public void OpenNotebookLm()
        {
            SaveUrl();
            bool success = NotebookLmService.OpenNotebookLm(NotebookLmUrl);
            if (success)
            {
                StatusMessage = "🌐 NotebookLM ouvert dans votre navigateur.";
            }
            else
            {
                StatusMessage = "❌ Impossible d'ouvrir le navigateur.";
            }
        }

        [RelayCommand]
        public void SaveUrl()
        {
            string url = NotebookLmUrl.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                url = NotebookLmService.DefaultNotebookLmUrl;
            }
            NotebookLmUrl = url;
            _parentViewModel.NotebookLmUrl = url;
            _parentViewModel.Save();
            StatusMessage = "✅ Lien du carnet NotebookLM enregistré.";
        }

        [RelayCommand]
        public void CopyKnowledgeBase()
        {
            var profile = _parentViewModel.BuildProfileFromProperties();
            string markdown = NotebookLmService.GenerateKnowledgeBaseMarkdown(profile);
            Clipboard.SetText(markdown);
            StatusMessage = "📋 Dossier copié dans le presse-papier ! Collez-le dans NotebookLM (Ajouter source > Texte copié).";
            MessageBox.Show("Le dossier source complet a été copié dans votre presse-papier.\n\nDans NotebookLM :\n1. Cliquez sur '+ Ajouter des sources'\n2. Choisissez 'Texte copié'\n3. Collez (Ctrl+V) et validez !", "Source Prête pour NotebookLM", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public void ExportSourceFile()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Exporter le Dossier Source pour NotebookLM",
                Filter = "Document Markdown (*.md)|*.md|Fichier Texte (*.txt)|*.txt|Fichier PDF (*.pdf)|*.pdf",
                FileName = $"AFMI_Dossier_Source_NotebookLM_{DateTime.Now:yyyyMMdd}.md"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var profile = _parentViewModel.BuildProfileFromProperties();
                    NotebookLmService.ExportKnowledgeBase(sfd.FileName, profile);
                    StatusMessage = $"✅ Fichier exporté : {Path.GetFileName(sfd.FileName)}";
                    MessageBox.Show($"Le fichier source a été généré avec succès :\n{sfd.FileName}\n\nVous pouvez maintenant le glisser-déposer directement dans votre carnet NotebookLM.", "Export Réussi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur d'export : {ex.Message}";
                    MessageBox.Show($"Erreur lors de l'export : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public void CopyPrompt(NotebookLmPromptPreset? preset)
        {
            preset ??= SelectedPrompt;
            if (preset != null && !string.IsNullOrWhiteSpace(preset.PromptText))
            {
                Clipboard.SetText(preset.PromptText);
                StatusMessage = $"📋 Prompt '{preset.Title}' copié ! Collez-le dans la zone de chat NotebookLM.";
            }
        }
    }
}

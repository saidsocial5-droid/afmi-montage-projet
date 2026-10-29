using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class IdeationInnovationViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Project> _availableProjects = new();
        [ObservableProperty] private Project? _selectedProject;

        [ObservableProperty] private bool _isGenerating;
        [ObservableProperty] private string _statusMessage = string.Empty;

        // Generated Results
        [ObservableProperty] private string _generatedTitle = string.Empty;
        [ObservableProperty] private string _generatedContext = string.Empty;
        [ObservableProperty] private string _generatedObjective = string.Empty;

        // Integration Checkboxes
        [ObservableProperty] private bool _integrateTitle = false;
        [ObservableProperty] private bool _integrateContext = false;
        [ObservableProperty] private bool _integrateObjective = false;

        public IdeationInnovationViewModel()
        {
            LoadProjects();
        }

        public IdeationInnovationViewModel(string initialTitle, string initialContext, string initialObjective = "") : this()
        {
            GeneratedTitle = initialTitle ?? string.Empty;
            GeneratedContext = initialContext ?? string.Empty;
            GeneratedObjective = initialObjective ?? string.Empty;
            StatusMessage = "Termes de référence et données préchargés depuis le Radar des Projets.";
            IntegrateTitle = !string.IsNullOrWhiteSpace(GeneratedTitle);
            IntegrateContext = !string.IsNullOrWhiteSpace(GeneratedContext);
            IntegrateObjective = !string.IsNullOrWhiteSpace(GeneratedObjective);
        }

        private void LoadProjects()
        {
            using var db = new AppDbContext();
            // Récupérer les projets "en cours de montage" (ici tous les projets pour l'instant)
            AvailableProjects = new ObservableCollection<Project>(db.Projects.ToList());
        }

        [RelayCommand]
        public async Task GenerateIdeasAsync()
        {
            if (SelectedProject == null)
            {
                StatusMessage = "Veuillez sélectionner un projet à améliorer.";
                return;
            }

            IsGenerating = true;
            StatusMessage = "Génération et recherche sur Internet en cours (cela peut prendre un moment)...";

            try
            {
                using var db = new AppDbContext();
                var docs = await db.ProjectDocuments.Where(d => d.ProjectId == SelectedProject.Id).ToListAsync();
                string docsSummary = string.Join("\n\n", docs.Select(d => d.AiSummary).Where(t => !string.IsNullOrWhiteSpace(t)));

                string prompt = $@"
Améliore et innove ce projet en cours de montage. Fais une recherche sur Internet pour t'inspirer de projets similaires récents et efficaces.
Titre actuel: {SelectedProject.Title}
Contexte actuel: {SelectedProject.ContextJustification}
Objectif actuel: {SelectedProject.GeneralObjective}

Documents associés (le cas échéant):
{docsSummary}

Retourne UNIQUEMENT un objet JSON avec les propriétés suivantes (sans markdown autour) :
- Title : un nouveau titre accrocheur et innovant
- Context : un contexte amélioré avec une justification solide inspirée par les recherches
- Objective : un objectif général ambitieux et mesurable
";

                string sys = "Tu es un consultant en idéation et innovation de projets associatifs. Réponds EXCLUSIVEMENT en JSON strict. Ne mets pas de bloc markdown ```json.";
                
                string result = await AiAssistantService.CallAiAsync(prompt, sys, enableSearch: true);
                
                // Nettoyage robuste du JSON (extraction entre le premier { et le dernier })
                string cleaned = result.Trim();
                var mdMatch = System.Text.RegularExpressions.Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (mdMatch.Success) cleaned = mdMatch.Groups[1].Value.Trim();

                int firstBrace = cleaned.IndexOf('{');
                int lastBrace = cleaned.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
                }

                using var doc = JsonDocument.Parse(cleaned);
                if (doc.RootElement.TryGetProperty("Title", out var tProp)) GeneratedTitle = tProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("Context", out var cProp)) GeneratedContext = cProp.GetString() ?? string.Empty;
                if (doc.RootElement.TryGetProperty("Objective", out var oProp)) GeneratedObjective = oProp.GetString() ?? string.Empty;

                StatusMessage = "Idées générées avec succès ! Vous pouvez maintenant choisir de les intégrer.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur lors de la génération : " + ex.Message;
            }
            finally
            {
                IsGenerating = false;
            }
        }

        [RelayCommand]
        public void IntegrateIdeas(Window window)
        {
            if (SelectedProject == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbProject = db.Projects.FirstOrDefault(p => p.Id == SelectedProject.Id);
                if (dbProject != null)
                {
                    if (IntegrateTitle && !string.IsNullOrWhiteSpace(GeneratedTitle))
                        dbProject.Title = GeneratedTitle;
                    
                    if (IntegrateContext && !string.IsNullOrWhiteSpace(GeneratedContext))
                        dbProject.ContextJustification = GeneratedContext;

                    if (IntegrateObjective && !string.IsNullOrWhiteSpace(GeneratedObjective))
                        dbProject.GeneralObjective = GeneratedObjective;

                    db.SaveChanges();
                    MessageBox.Show("Les éléments choisis ont été intégrés avec succès au projet !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur lors de l'intégration : " + ex.Message;
            }
        }
    }
}



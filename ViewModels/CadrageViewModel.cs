using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Radar;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class CadrageViewModel : ObservableObject
    {
        private readonly MainViewModel _mainViewModel;
        private readonly Project _project;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _interventionZone;

        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private DateTime? _endDate;

        [ObservableProperty]
        private decimal _totalBudget;

        [ObservableProperty]
        private string _currency;

        [ObservableProperty]
        private decimal _localContribution;

        [ObservableProperty]
        private decimal _partnerContribution;

        [ObservableProperty]
        private string _leadOrganization;

        [ObservableProperty]
        private string _background;

        [ObservableProperty]
        private string _contextJustification;

        [ObservableProperty]
        private string _justification;

        [ObservableProperty]
        private string _projectApproach;

        [ObservableProperty]
        private string _projectImpacts;

        [ObservableProperty]
        private string _strategicAlignment;

        [ObservableProperty]
        private string _sustainability;

        [ObservableProperty]
        private string _organizationalStrengths;

        [ObservableProperty]
        private string _swotStrengths;

        [ObservableProperty]
        private string _swotWeaknesses;

        [ObservableProperty]
        private string _swotOpportunities;

        [ObservableProperty]
        private string _swotThreats;

        [ObservableProperty]
        private string _generalObjective;

        [ObservableProperty]
        private ObservableCollection<Objective> _objectives = new();

        [ObservableProperty]
        private string _newObjectiveDescription = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _brainstormingResult = string.Empty;

        [ObservableProperty]
        private bool _isBrainstorming;

        public CadrageViewModel(Project project, MainViewModel mainViewModel)
        {
            _project = project;
            _mainViewModel = mainViewModel;

            // Initialiser les champs avec les valeurs existantes
            _title = project.Title;
            _interventionZone = project.InterventionZone;
            _startDate = project.StartDate;
            _endDate = project.EndDate;
            _totalBudget = project.TotalBudget;
            _currency = project.Currency;
            _localContribution = project.LocalContribution;
            _partnerContribution = project.PartnerContribution;
            _leadOrganization = project.LeadOrganization;
            _background = project.Background;
            _contextJustification = project.ContextJustification;
            _justification = project.Justification;
            _projectApproach = project.ProjectApproach;
            _projectImpacts = project.ProjectImpacts;
            _strategicAlignment = project.StrategicAlignment;
            _sustainability = project.Sustainability;
            _organizationalStrengths = project.OrganizationalStrengths;
            _swotStrengths = project.SwotStrengths;
            _swotWeaknesses = project.SwotWeaknesses;
            _swotOpportunities = project.SwotOpportunities;
            _swotThreats = project.SwotThreats;
            _generalObjective = project.GeneralObjective;

            LoadObjectives();
        }

        private void LoadObjectives()
        {
            using var db = new AppDbContext();
            var dbObjectives = db.Objectives
                .Where(o => o.ProjectId == _project.Id)
                .OrderBy(o => o.Code)
                .ToList();

            Objectives = new ObservableCollection<Objective>(dbObjectives);
        }

        [RelayCommand]
        public async Task GenerateIdeationAsync()
        {
            if (IsBrainstorming) return;
            IsBrainstorming = true;
            StatusMessage = "Génération d'idées et recherche en cours (cela peut prendre un moment)...";
            BrainstormingResult = "Recherche sur Internet et analyse des documents en cours...\n\nVeuillez patienter.";

            try
            {
                using var db = new AppDbContext();
                var docs = await db.ProjectDocuments.Where(d => d.ProjectId == _project.Id).ToListAsync();
                string docsSummary = string.Join("\n\n", docs.Select(d => d.AiSummary).Where(t => !string.IsNullOrWhiteSpace(t)));

                string prompt = "Génère 3 propositions d'idées de projets complètes et innovantes. " +
                                "Base-toi sur les documents suivants (s'il y en a) pour comprendre le contexte local :\n" + 
                                (string.IsNullOrWhiteSpace(docsSummary) ? "Aucun document fourni." : docsSummary) + "\n\n" +
                                "Fais obligatoirement une recherche sur Internet pour t'inspirer de projets similaires existants qui ont fait leurs preuves. " +
                                "Pour chaque proposition, fournis :\n" +
                                "- Un Titre accrocheur\n" +
                                "- Un Contexte et une justification\n" +
                                "- Les Objectifs principaux\n" +
                                "- La Zone d'intervention recommandée\n\n" +
                                "Présente cela sous forme de Markdown bien structuré.";

                string sys = "Tu es un consultant expert en idéation et montage de projets. Tu dois faire des recherches en ligne si nécessaire. Réponds IMPÉRATIVEMENT en FRANÇAIS.";

                // Appel de l'IA avec enableSearch = true
                string result = await AiAssistantService.CallAiAsync(prompt, sys, enableSearch: true);
                
                BrainstormingResult = result;
                StatusMessage = "Idéation générée avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur : " + ex.Message;
                BrainstormingResult = "Erreur lors de la génération : " + ex.Message;
            }
            finally
            {
                IsBrainstorming = false;
            }
        }

        [RelayCommand]
        public async Task GenererTitreIa()
        {
            StatusMessage = "Génération de suggestions de titres par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 titres pour ce projet. Le titre doit être accrocheur, professionnel et refléter l'impact du projet. Rédige-les sous forme de tirets. Voici le contexte actuel :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets. Réponds IMPÉRATIVEMENT en FRANÇAIS, donne UNIQUEMENT la liste avec des tirets (-), sans introduction ni commentaires.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = result.Split(new[] { "\n- ", "\n* " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') }).ToList();
                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Titre du projet", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    Title = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Titre mis à jour avec succès !";
                }
            }
            catch (Exception ex) { StatusMessage = "Erreur d'appel IA : " + ex.Message; }
        }
        [RelayCommand]
        public void SaveProject()
        {
            try
            {
                using var db = new AppDbContext();
                
                // Récupérer le projet frais de la base de données
                var dbProject = db.Projects.FirstOrDefault(p => p.Id == _project.Id);
                if (dbProject != null)
                {
                    dbProject.Title = Title.Trim();
                    dbProject.InterventionZone = InterventionZone?.Trim() ?? string.Empty;
                    dbProject.StartDate = StartDate;
                    dbProject.EndDate = EndDate;
                    dbProject.TotalBudget = TotalBudget;
                    dbProject.Currency = Currency ?? "EUR";
                    dbProject.LocalContribution = LocalContribution;
                    dbProject.PartnerContribution = PartnerContribution;
                    dbProject.LeadOrganization = LeadOrganization?.Trim() ?? string.Empty;
                    dbProject.Background = Background?.Trim() ?? string.Empty;
                    dbProject.ContextJustification = ContextJustification?.Trim() ?? string.Empty;
                    dbProject.Justification = Justification?.Trim() ?? string.Empty;
                    dbProject.ProjectApproach = ProjectApproach?.Trim() ?? string.Empty;
                    dbProject.ProjectImpacts = ProjectImpacts?.Trim() ?? string.Empty;
                    dbProject.StrategicAlignment = StrategicAlignment?.Trim() ?? string.Empty;
                    dbProject.Sustainability = Sustainability?.Trim() ?? string.Empty;
                    dbProject.OrganizationalStrengths = OrganizationalStrengths?.Trim() ?? string.Empty;
                    dbProject.SwotStrengths = SwotStrengths?.Trim() ?? string.Empty;
                    dbProject.SwotWeaknesses = SwotWeaknesses?.Trim() ?? string.Empty;
                    dbProject.SwotOpportunities = SwotOpportunities?.Trim() ?? string.Empty;
                    dbProject.SwotThreats = SwotThreats?.Trim() ?? string.Empty;
                    dbProject.GeneralObjective = GeneralObjective?.Trim() ?? string.Empty;
                    dbProject.UpdatedAt = DateTime.UtcNow;

                    db.SaveChanges();

                    // Mettre à  jour l'instance locale partagée
                    _project.Title = dbProject.Title;
                    _project.InterventionZone = dbProject.InterventionZone;
                    _project.StartDate = dbProject.StartDate;
                    _project.EndDate = dbProject.EndDate;
                    _project.TotalBudget = dbProject.TotalBudget;
                    _project.Currency = dbProject.Currency;
                    _project.LocalContribution = dbProject.LocalContribution;
                    _project.PartnerContribution = dbProject.PartnerContribution;
                    _project.LeadOrganization = dbProject.LeadOrganization;
                    _project.Background = dbProject.Background;
                    _project.ContextJustification = dbProject.ContextJustification;
                    _project.Justification = dbProject.Justification;
                    _project.ProjectApproach = dbProject.ProjectApproach;
                    _project.ProjectImpacts = dbProject.ProjectImpacts;
                    _project.StrategicAlignment = dbProject.StrategicAlignment;
                    _project.Sustainability = dbProject.Sustainability;
                    _project.OrganizationalStrengths = dbProject.OrganizationalStrengths;
                    _project.SwotStrengths = dbProject.SwotStrengths;
                    _project.SwotWeaknesses = dbProject.SwotWeaknesses;
                    _project.SwotOpportunities = dbProject.SwotOpportunities;
                    _project.SwotThreats = dbProject.SwotThreats;
                    _project.GeneralObjective = dbProject.GeneralObjective;

                    StatusMessage = "Modifications enregistrées avec succà¨s !";
                }
                else
                {
                    StatusMessage = "Erreur : Projet introuvable dans la base.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la sauvegarde : {ex.Message}";
            }
        }

        [RelayCommand]
        public void AddObjective()
        {
            if (string.IsNullOrWhiteSpace(NewObjectiveDescription))
                return;

            try
            {
                using var db = new AppDbContext();
                
                // Compter pour générer le code OS (Objectif Spécifique)
                int count = db.Objectives.Count(o => o.ProjectId == _project.Id);
                string code = $"OS{count + 1}";

                var newObj = new Objective
                {
                    ProjectId = _project.Id,
                    Code = code,
                    Description = NewObjectiveDescription.Trim()
                };

                db.Objectives.Add(newObj);
                db.SaveChanges();

                Objectives.Add(newObj);
                NewObjectiveDescription = string.Empty;
                StatusMessage = "Objectif spécifique ajouté.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'ajout : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteObjective(Objective? objective)
        {
            if (objective == null) return;

            try
            {
                using var db = new AppDbContext();

                if (objective.Id > 0)
                {
                    var dbObj = db.Objectives.FirstOrDefault(o => o.Id == objective.Id);
                    if (dbObj != null)
                    {
                        // 1. Récupérer les identifiants des résultats liés
                        var resultIds = db.Results
                            .Where(r => r.ObjectiveId == objective.Id)
                            .Select(r => r.Id)
                            .ToList();

                        // 2. Récupérer les identifiants des activités liées
                        var activityIds = resultIds.Any()
                            ? db.Activities.Where(a => resultIds.Contains(a.ResultId)).Select(a => a.Id).ToList()
                            : new List<int>();

                        // 3. Nettoyer les dépendances des activités
                        if (activityIds.Any())
                        {
                            var reports = db.ActivityReports.Where(ar => activityIds.Contains(ar.ActivityId)).ToList();
                            if (reports.Any()) db.ActivityReports.RemoveRange(reports);

                            var budgetItems = db.BudgetItems.Where(b => b.ActivityId.HasValue && activityIds.Contains(b.ActivityId.Value)).ToList();
                            foreach (var b in budgetItems) b.ActivityId = null;

                            var actIndicators = db.Indicators.Where(i => i.ActivityId.HasValue && activityIds.Contains(i.ActivityId.Value)).ToList();
                            if (actIndicators.Any()) db.Indicators.RemoveRange(actIndicators);

                            var activities = db.Activities.Where(a => activityIds.Contains(a.Id)).ToList();
                            if (activities.Any()) db.Activities.RemoveRange(activities);
                        }

                        // 4. Nettoyer les indicateurs liés directement à cet OS
                        var directIndicators = db.Indicators.Where(i => i.ObjectiveId == objective.Id).ToList();
                        if (directIndicators.Any())
                        {
                            db.Indicators.RemoveRange(directIndicators);
                        }

                        // 5. Supprimer les résultats
                        if (resultIds.Any())
                        {
                            var results = db.Results.Where(r => resultIds.Contains(r.Id)).ToList();
                            if (results.Any()) db.Results.RemoveRange(results);
                        }

                        // 6. Nettoyer les liens de traçabilité et dérivation
                        var derivationLinks = db.StrategicLinks
                            .Where(l => (l.TargetEntityType == "Objective" && l.TargetEntityId == objective.Id) ||
                                        (l.SourceEntityType == "Objective" && l.SourceEntityId == objective.Id))
                            .ToList();
                        if (derivationLinks.Any()) db.StrategicLinks.RemoveRange(derivationLinks);

                        // 7. Supprimer l'objectif
                        db.Objectives.Remove(dbObj);
                        db.SaveChanges();
                    }
                }

                // Supprimer de la collection en mémoire
                var inMem = Objectives.FirstOrDefault(o => (objective.Id > 0 && o.Id == objective.Id) || o == objective);
                if (inMem != null) Objectives.Remove(inMem);
                else Objectives.Remove(objective);

                // Réindexer les codes des objectifs restants (OS1, OS2, etc.)
                ReindexObjectiveCodes();
                StatusMessage = "Objectif spécifique supprimé avec succès.";
            }
            catch (Exception ex)
            {
                var inMem = Objectives.FirstOrDefault(o => (objective.Id > 0 && o.Id == objective.Id) || o == objective);
                if (inMem != null) Objectives.Remove(inMem);
                StatusMessage = $"Suppression effectuée : {ex.Message}";
            }
        }

        private void ReindexObjectiveCodes()
        {
            try
            {
                using var db = new AppDbContext();
                var dbObjectives = db.Objectives
                    .Where(o => o.ProjectId == _project.Id)
                    .OrderBy(o => o.Id)
                    .ToList();

                for (int i = 0; i < dbObjectives.Count; i++)
                {
                    dbObjectives[i].Code = $"OS{i + 1}";
                }

                db.SaveChanges();
            }
            catch { }

            // Réindexer également les éléments en mémoire pour cohérence instantanée
            for (int i = 0; i < Objectives.Count; i++)
            {
                Objectives[i].Code = $"OS{i + 1}";
            }

            LoadObjectives();
        }

        #region Assistant IA Rédactionnel (Enrichir, Reformuler, Détailler)

        [RelayCommand]
        public async Task EnrichirField(string fieldName)
        {
            await OpenFieldAssistDialogAsync(fieldName, TextAssistActionType.Enrichir);
        }

        [RelayCommand]
        public async Task ReformulerField(string fieldName)
        {
            await OpenFieldAssistDialogAsync(fieldName, TextAssistActionType.Reformuler);
        }

        [RelayCommand]
        public async Task DetaillerField(string fieldName)
        {
            await OpenFieldAssistDialogAsync(fieldName, TextAssistActionType.Detailler);
        }

        private async Task OpenFieldAssistDialogAsync(string fieldName, TextAssistActionType action)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return;

            string currentText = fieldName switch
            {
                nameof(Background) => Background ?? string.Empty,
                nameof(Justification) => Justification ?? string.Empty,
                nameof(GeneralObjective) => GeneralObjective ?? string.Empty,
                nameof(ProjectApproach) => ProjectApproach ?? string.Empty,
                nameof(ProjectImpacts) => ProjectImpacts ?? string.Empty,
                nameof(StrategicAlignment) => StrategicAlignment ?? string.Empty,
                nameof(Sustainability) => Sustainability ?? string.Empty,
                nameof(OrganizationalStrengths) => OrganizationalStrengths ?? string.Empty,
                nameof(SwotStrengths) => SwotStrengths ?? string.Empty,
                nameof(SwotWeaknesses) => SwotWeaknesses ?? string.Empty,
                nameof(SwotOpportunities) => SwotOpportunities ?? string.Empty,
                nameof(SwotThreats) => SwotThreats ?? string.Empty,
                nameof(Title) => Title ?? string.Empty,
                _ => string.Empty
            };

            var semanticType = fieldName switch
            {
                nameof(Background) => FieldSemanticType.ContexteGeneral,
                nameof(Justification) => FieldSemanticType.Generique,
                nameof(GeneralObjective) => FieldSemanticType.ObjectifGeneral,
                nameof(ProjectApproach) => FieldSemanticType.Generique,
                nameof(ProjectImpacts) => FieldSemanticType.Effet,
                nameof(StrategicAlignment) => FieldSemanticType.ObjectifGeneral,
                nameof(Sustainability) => FieldSemanticType.Generique,
                nameof(OrganizationalStrengths) => FieldSemanticType.Generique,
                nameof(SwotStrengths) => FieldSemanticType.Cause,
                nameof(SwotWeaknesses) => FieldSemanticType.ProblemeCentral,
                nameof(SwotOpportunities) => FieldSemanticType.Cause,
                nameof(SwotThreats) => FieldSemanticType.Risque,
                nameof(Title) => FieldSemanticType.Titre,
                _ => FieldSemanticType.Generique
            };

            string label = fieldName switch
            {
                nameof(Background) => "Contexte général du projet",
                nameof(Justification) => "Justification du projet",
                nameof(GeneralObjective) => "Objectif général du projet",
                nameof(ProjectApproach) => "Démarche et approche méthodologique",
                nameof(ProjectImpacts) => "Impacts attendus du projet",
                nameof(StrategicAlignment) => "Alignement stratégique",
                nameof(Sustainability) => "Durabilité à long terme (Pérennité)",
                nameof(OrganizationalStrengths) => "Forces organisationnelles",
                nameof(SwotStrengths) => "Forces (Strengths)",
                nameof(SwotWeaknesses) => "Faiblesses (Weaknesses)",
                nameof(SwotOpportunities) => "Opportunités (Opportunities)",
                nameof(SwotThreats) => "Menaces (Threats)",
                nameof(Title) => "Titre officiel du projet",
                _ => fieldName
            };

            var descriptor = new FieldDescriptor($"Cadrage.{fieldName}", label, semanticType);
            int? projId = _project != null ? _project.Id : (int?)null;

            var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: currentText,
                isSelection: false,
                initialAction: action,
                projectId: projId);

            var ownerWindow = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                              ?? System.Windows.Application.Current?.MainWindow;
            if (ownerWindow != null)
            {
                dialog.Owner = ownerWindow;
            }

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                string newText = dialog.ResultText;
                switch (fieldName)
                {
                    case nameof(Background): Background = newText; break;
                    case nameof(Justification): Justification = newText; break;
                    case nameof(GeneralObjective): GeneralObjective = newText; break;
                    case nameof(ProjectApproach): ProjectApproach = newText; break;
                    case nameof(ProjectImpacts): ProjectImpacts = newText; break;
                    case nameof(StrategicAlignment): StrategicAlignment = newText; break;
                    case nameof(Sustainability): Sustainability = newText; break;
                    case nameof(OrganizationalStrengths): OrganizationalStrengths = newText; break;
                    case nameof(SwotStrengths): SwotStrengths = newText; break;
                    case nameof(SwotWeaknesses): SwotWeaknesses = newText; break;
                    case nameof(SwotOpportunities): SwotOpportunities = newText; break;
                    case nameof(SwotThreats): SwotThreats = newText; break;
                    case nameof(Title): Title = newText; break;
                }

                StatusMessage = $"{label} mis à jour avec succès via l'assistant IA !";
            }

            await Task.CompletedTask;
        }

        [RelayCommand]
        public async Task EnrichirObjective(Objective obj)
        {
            if (obj == null) return;
            await AssistObjectiveAsync(obj, TextAssistActionType.Enrichir);
        }

        [RelayCommand]
        public async Task ReformulerObjective(Objective obj)
        {
            if (obj == null) return;
            await AssistObjectiveAsync(obj, TextAssistActionType.Reformuler);
        }

        [RelayCommand]
        public async Task DetaillerObjective(Objective obj)
        {
            if (obj == null) return;
            await AssistObjectiveAsync(obj, TextAssistActionType.Detailler);
        }

        private async Task AssistObjectiveAsync(Objective obj, TextAssistActionType action)
        {
            var descriptor = new FieldDescriptor($"Cadrage.Objective_{obj.Id}", $"Objectif Spécifique ({obj.Code})", FieldSemanticType.ObjectifSpecifique);
            var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: obj.Description ?? string.Empty,
                isSelection: false,
                initialAction: action,
                projectId: _project?.Id);

            var ownerWindow = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                              ?? System.Windows.Application.Current?.MainWindow;
            if (ownerWindow != null) dialog.Owner = ownerWindow;

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                obj.Description = dialog.ResultText;
                StatusMessage = $"Objectif {obj.Code} mis à jour avec succès !";
            }
            await Task.CompletedTask;
        }

        #endregion

        [RelayCommand]
        public async Task SuggererBackground()
        {
            StatusMessage = "Génération de suggestions de contexte par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 options de 'Contexte général du projet' (1 paragraphe chacune). Décris le cadre géographique, social et économique. Rédige-les sous forme de tirets. Voici les informations actuelles du projet :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets. Réponds IMPÉRATIVEMENT en FRANÇAIS, sans introduction, donne la liste avec des tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = result.Split(new[] { "\n- ", "\n* " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') }).ToList();
                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Contexte général", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    Background = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Contexte mis à jour avec succès !";
                }
            }
            catch (Exception ex) { StatusMessage = "Erreur d'appel IA : " + ex.Message; }
        }

        [RelayCommand]
        public async Task GenererBackground()
        {
            StatusMessage = "Génération du contexte par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte pour la section 'Contexte général du projet' (environ 2 paragraphes). Décris le cadre géographique, social et économique. Base-toi sur ces infos:\n\n" + projectContext;
                string sys = "Tu es un rédacteur professionnel de projets. Rédige UNIQUEMENT le texte demandé en FRANÇAIS, sans commentaires.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                Background = result.Trim();
                StatusMessage = "Contexte généré avec succès !";
            }
            catch (Exception ex) { StatusMessage = "Erreur d'appel IA : " + ex.Message; }
        }

        [RelayCommand]
        public async Task SuggererJustification()
        {
            StatusMessage = "Génération de suggestions de justification...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 options de 'Justification du projet' (1 paragraphe chacune). Explique pourquoi ce projet est indispensable. Liste avec des tirets. Infos:\n\n" + projectContext;
                string sys = "Tu es un expert. Rédige en FRANÇAIS sans intro, avec des tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = result.Split(new[] { "\n- ", "\n* " }, StringSplitOptions.RemoveEmptyEntries).Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') }).ToList();
                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Justification", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText)) { Justification = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r'); StatusMessage = "Justification mise à jour !"; }
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task GenererJustification()
        {
            StatusMessage = "Génération de la justification...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte pour 'Justification du projet' (2 paragraphes). Explique pourquoi le projet est indispensable et quels besoins il comble. Infos:\n\n" + projectContext;
                string sys = "Tu es un rédacteur professionnel. Rédige UNIQUEMENT le texte, en FRANÇAIS.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                Justification = result.Trim();
                StatusMessage = "Justification générée !";
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task SuggererGeneralObjective()
        {
            StatusMessage = "Suggestions d'objectif général...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 options courtes pour l' 'Objectif global' (1 phrase chacune). Tirets. Infos:\n\n" + projectContext;
                string sys = "Expert. Sans intro, tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = result.Split(new[] { "\n- ", "\n* " }, StringSplitOptions.RemoveEmptyEntries).Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') }).ToList();
                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Objectif global", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText)) { GeneralObjective = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r'); StatusMessage = "Objectif global mis à jour !"; }
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task GenererGeneralObjective()
        {
            StatusMessage = "Génération de l'objectif général...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Rédige l' 'Objectif global' du projet en une ou deux phrases maximum, basé sur :\n\n" + projectContext;
                string sys = "Rédacteur pro. UNIQUEMENT le texte.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                GeneralObjective = result.Trim();
                StatusMessage = "Objectif global généré !";
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task SuggererProjectApproach()
        {
            StatusMessage = "Suggestions d'approche...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 options pour 'Approche d'intervention' (méthodologie). 1 paragraphe chaque. Tirets. Infos:\n\n" + projectContext;
                string sys = "Expert. Sans intro, tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = result.Split(new[] { "\n- ", "\n* " }, StringSplitOptions.RemoveEmptyEntries).Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') }).ToList();
                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Approche", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText)) { ProjectApproach = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r'); StatusMessage = "Approche mise à jour !"; }
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task GenererProjectApproach()
        {
            StatusMessage = "Génération de l'approche...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte pour 'Approche d'intervention / Méthodologie' (2-3 paragraphes). Base-toi sur:\n\n" + projectContext;
                string sys = "Rédacteur pro. UNIQUEMENT le texte.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                ProjectApproach = result.Trim();
                StatusMessage = "Approche générée !";
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task SuggererObjective()
        {
            StatusMessage = "Suggestions d'objectifs spécifiques...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 'Objectifs spécifiques' (1 phrase chaque). Tirets. Infos:\n\n" + projectContext;
                string sys = "Expert. Sans intro, tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = result.Split(new[] { "\n- ", "\n* " }, StringSplitOptions.RemoveEmptyEntries).Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') }).ToList();
                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Objectif spécifique", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText)) { NewObjectiveDescription = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r'); StatusMessage = "Prêt à ajouter !"; }
            }
            catch (Exception ex) { StatusMessage = "Erreur : " + ex.Message; }
        }

        [RelayCommand]
        public async Task GenererObjectives()
        {
            StatusMessage = "Génération d'objectifs spécifiques par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère 3 'Objectifs spécifiques' clairs, précis et mesurables en français. Formatte avec des tirets (- ). Base-toi sur les informations du projet :\n\n" + projectContext;
                string sys = "Tu es un rédacteur professionnel de projets de développement. Réponds en français. Renvoie UNIQUEMENT les 3 objectifs sous forme de liste à puces avec des tirets (- ), sans phrase d'introduction, sans conclusion et sans titres en gras.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                var obs = result.Split(new[] { "\n- ", "\n* ", "\r\n- ", "\r\n* " }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => CleanObjectiveText(s))
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .ToList();

                using var db = new AppDbContext();
                int count = db.Objectives.Count(o => o.ProjectId == _project.Id);

                foreach (var o in obs)
                {
                    count++;
                    var newObj = new Objective
                    {
                        ProjectId = _project.Id,
                        Code = $"OS{count}",
                        Description = o
                    };
                    db.Objectives.Add(newObj);
                    db.SaveChanges(); // Définit un Id valide et persistant en base
                    Objectives.Add(newObj);
                }

                StatusMessage = "Objectifs spécifiques générés et enregistrés avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur : " + ex.Message;
            }
        }

        private static string CleanObjectiveText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string text = raw.Trim();

            // Supprimer les puces initiales
            text = text.TrimStart('-', '*', ' ', '\t', '\r', '\n');

            // Filtrer les phrases d'introduction conversationnelles en anglais ou français
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

            var validLines = new List<string>();
            foreach (var line in lines)
            {
                string l = line;
                if (l.StartsWith("Here are", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Voici", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Sure", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Bien sûr", StringComparison.OrdinalIgnoreCase) ||
                    l.EndsWith("with dashes:", StringComparison.OrdinalIgnoreCase) ||
                    l.EndsWith("avec des tirets :", StringComparison.OrdinalIgnoreCase) ||
                    l.EndsWith("suivants :", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Supprimer les préfixes comme **Objectif Spécifique 1 :** ou Objectif 1:
                l = System.Text.RegularExpressions.Regex.Replace(l, @"^\*+Objectif\s*Sp[eé]cifique\s*\d*[\s:\-]*\*+[\s:\-]*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                l = System.Text.RegularExpressions.Regex.Replace(l, @"^Objectif\s*Sp[eé]cifique\s*\d*[\s:\-]*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                l = System.Text.RegularExpressions.Regex.Replace(l, @"^\*+OS\s*\d*[\s:\-]*\*+[\s:\-]*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                l = l.TrimStart('-', '*', ' ', ':', '\t');

                if (!string.IsNullOrWhiteSpace(l))
                {
                    validLines.Add(l);
                }
            }

            return string.Join(" ", validLines).Trim();
        }
        [RelayCommand]
        public async Task SuggererProjectImpacts()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Impacts du projet' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Impacts du projet", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    ProjectImpacts = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererProjectImpacts()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte pour la section 'Impacts du projet à long terme' de 2 à 3 paragraphes. Décris les effets indirects attendus (changements systémiques, autonomisation). Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un rédacteur expert de projet de développement associatif. Rédige UNIQUEMENT le texte demandé en FRANÇAIS, sans introduction ni conclusion.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                ProjectImpacts = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererStrategicAlignment()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Alignement stratégique' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Alignement stratégique", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    StrategicAlignment = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererStrategicAlignment()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte pour la section 'Lien vers les stratégies nationales et internationales' de 2 à 3 paragraphes. Explique comment ce projet s'aligne avec les priorités politiques locales ou les ODD internationaux. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un rédacteur expert de projet de développement associatif. Rédige UNIQUEMENT le texte demandé en FRANÇAIS, sans introduction ni conclusion.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                StrategicAlignment = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererSustainability()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Durabilité / Pérennité' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Durabilité / Pérennité", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    Sustainability = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererSustainability()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte pour la section 'Durabilité et Pérennité du projet' de 2 à 3 paragraphes. Détaille comment le projet sera maintenu après la fin du financement (autonomie financière, appropriation locale). Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un spécialiste en montage de projet. Rédige UNIQUEMENT le texte demandé en FRANÇAIS, sans introduction ni conclusion.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                Sustainability = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public void ImporterDepuisProfilOng()
        {
            try
            {
                var profile = ProjectRadarConfigService.LoadProfile();
                if (!string.IsNullOrWhiteSpace(profile.OrganizationName))
                {
                    LeadOrganization = profile.OrganizationName;
                }
                if (profile.GeographicZones != null && profile.GeographicZones.Any())
                {
                    InterventionZone = string.Join(", ", profile.GeographicZones);
                }

                OrganizationalStrengths = $"{profile.OrganizationName} ({profile.Acronym}) — Fondée en {profile.CreationDate}.\n\n" +
                    $"• Statut & Gouvernance : {profile.LegalStatus} ({profile.RegistrationNumber}).\n\n" +
                    $"• Mission : {profile.Mission}\n\n" +
                    $"• Réalisations & Bilan d'impact : {profile.KeyAchievements}\n\n" +
                    $"• Capacité financière & fiducière : {profile.FinancialManagementCapacity}\n\n" +
                    $"• Équipe & Moyens : {profile.HumanResourcesCapacity} | {profile.LogisticalMaterialCapacity}\n\n" +
                    $"• Expérience Bailleurs : {profile.PastProjectsReferences}";

                StatusMessage = "Forces organisationnelles et identité importées depuis le Profil ONG !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'import : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererOrganizationalStrengths()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Forces organisationnelles' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Forces organisationnelles", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    OrganizationalStrengths = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererOrganizationalStrengths()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère un texte décrivant les 'Forces organisationnelles de l''association' (2 à 3 paragraphes). Mets en valeur l'expérience, les ressources humaines et matérielles, et la capacité de gestion de l'organisation. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en évaluation organisationnelle. Rédige UNIQUEMENT le texte demandé en FRANÇAIS, sans introduction ni conclusion.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                OrganizationalStrengths = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererSwotIa()
        {
            StatusMessage = "L'Agent IA étudie votre projet pour générer la matrice SWOT...";
            try
            {
                string projectContext = $"Titre du projet : {Title}\n" +
                                        $"Objectif général : {GeneralObjective}\n" +
                                        $"Contexte & Justification : {Justification}\n" +
                                        $"Approche méthodologique : {ProjectApproach}\n" +
                                        $"Forces de l'association : {OrganizationalStrengths}";

                string prompt = $"Voici les informations de cadrage d'un projet de développement local :\n\n" +
                                $"{projectContext}\n\n" +
                                "Génère une analyse SWOT complète et cohérente pour ce projet sous forme d'un objet JSON strict.\n" +
                                "Renvoie exclusivement le JSON suivant, sans balises markdown, sans texte d'introduction ni de conclusion :\n" +
                                "{\n" +
                                "  \"strengths\": \"Liste des forces internes de la structure et du projet (ex: expertise de l'équipe, partenariats locaux, etc.) sous forme de tirets...\",\n" +
                                "  \"weaknesses\": \"Liste des faiblesses ou limites internes (ex: dépendance financière externe, équipe bénévole restreinte, etc.) sous forme de tirets...\",\n" +
                                "  \"opportunities\": \"Liste des opportunités externes à saisir (ex: nouvelles réglementations favorables, soutien d'autres bailleurs, besoins croissants, etc.) sous forme de tirets...\",\n" +
                                "  \"threats\": \"Liste des menaces ou risques externes à surveiller (ex: instabilité politique locale, inflation, réticences culturelles, etc.) sous forme de tirets...\"\n" +
                                "}";

                string systemInstruction = "Tu es un consultant en stratégie de projet de développement international. Tu rédiges exclusivement au format JSON strict spécifié. Ne renvoie AUCUN texte avant ou après le JSON.";

                string result = await AiAssistantService.CallAiAsync(prompt, systemInstruction);

                // Extraction robuste multi-niveaux (JSON direct, JSON avec préambule/markdown, Regex, fallback texte brut)
                ParseSwotResponse(result, out string s, out string w, out string o, out string t);

                if (string.IsNullOrWhiteSpace(s) && string.IsNullOrWhiteSpace(w) &&
                    string.IsNullOrWhiteSpace(o) && string.IsNullOrWhiteSpace(t))
                {
                    StatusMessage = "L'IA a répondu mais aucun élément SWOT n'a pu être extrait. Veuillez réessayer.";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(s)) SwotStrengths = s;
                if (!string.IsNullOrWhiteSpace(w)) SwotWeaknesses = w;
                if (!string.IsNullOrWhiteSpace(o)) SwotOpportunities = o;
                if (!string.IsNullOrWhiteSpace(t)) SwotThreats = t;

                StatusMessage = "Matrice SWOT générée avec succès par l'IA ! Pensez à enregistrer.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'auto-génération SWOT : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererSwotStrengths()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Forces internes' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Forces internes", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    SwotStrengths = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererSwotStrengths()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère 3 à 4 forces internes pour la matrice SWOT du projet. Liste-les avec des tirets. Concentre-toi sur les atouts de l'association (expérience, équipe, ancrage). Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un consultant stratégique. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste sous forme de tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                SwotStrengths = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererSwotWeaknesses()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Faiblesses internes' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Faiblesses internes", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    SwotWeaknesses = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererSwotWeaknesses()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère 3 à 4 faiblesses internes (axes d'amélioration) pour la matrice SWOT du projet. Liste-les avec des tirets. Sois lucide et constructif. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un consultant stratégique. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste sous forme de tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                SwotWeaknesses = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererSwotOpportunities()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Opportunités externes' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Opportunités externes", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    SwotOpportunities = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererSwotOpportunities()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère 3 à 4 opportunités externes pour la matrice SWOT du projet. Liste-les avec des tirets. Concentre-toi sur le contexte externe favorable (politiques, environnement économique). Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un consultant stratégique. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste sous forme de tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                SwotOpportunities = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererSwotThreats()
        {
            StatusMessage = "Génération de suggestions par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Suggère 3 propositions de 'Menaces externes' (1 paragraphe chacune) pour ce projet. Rédige-les sous forme de tirets. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                
                var suggestions = result.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Menaces externes", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    SwotThreats = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Texte mis à jour avec succès !";
                }
                else
                {
                    StatusMessage = "Génération annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task GenererSwotThreats()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_project.Id, " ");
                string prompt = "Génère 3 à 4 menaces externes (risques) pour la matrice SWOT du projet. Liste-les avec des tirets. Concentre-toi sur les risques liés au contexte local ou économique. Voici les informations :\n\n" + projectContext;
                string sys = "Tu es un consultant stratégique. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste sous forme de tirets.";
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                SwotThreats = result.Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        public static void ParseSwotResponse(string rawResult, out string strengths, out string weaknesses, out string opportunities, out string threats)
        {
            strengths = string.Empty;
            weaknesses = string.Empty;
            opportunities = string.Empty;
            threats = string.Empty;

            if (string.IsNullOrWhiteSpace(rawResult))
                return;

            // 1. Tenter l'extraction de l'objet JSON (gère les préambules "Here is...", blocs ```json, etc.)
            string jsonCandidate = ExtractJsonObject(rawResult);
            if (!string.IsNullOrEmpty(jsonCandidate))
            {
                try
                {
                    var docOptions = new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    };
                    using var doc = JsonDocument.Parse(jsonCandidate, docOptions);
                    var root = doc.RootElement;

                    strengths = GetStringOrArrayFromElement(root, "strengths", "forces", "atouts");
                    weaknesses = GetStringOrArrayFromElement(root, "weaknesses", "faiblesses", "limites");
                    opportunities = GetStringOrArrayFromElement(root, "opportunities", "opportunites", "opportunités");
                    threats = GetStringOrArrayFromElement(root, "threats", "menaces", "risques");
                }
                catch
                {
                    // Fallback Regex si JsonDocument.Parse échoue (ex: virgules ou guillemets non échappés)
                    strengths = ExtractJsonFieldRegex(jsonCandidate, "strengths", "forces", "atouts");
                    weaknesses = ExtractJsonFieldRegex(jsonCandidate, "weaknesses", "faiblesses", "limites");
                    opportunities = ExtractJsonFieldRegex(jsonCandidate, "opportunities", "opportunites", "opportunités");
                    threats = ExtractJsonFieldRegex(jsonCandidate, "threats", "menaces", "risques");
                }
            }

            // 2. Si aucune donnée n'a été extraite, tenter le parsing depuis du texte brut ou Markdown
            if (string.IsNullOrWhiteSpace(strengths) && string.IsNullOrWhiteSpace(weaknesses) &&
                string.IsNullOrWhiteSpace(opportunities) && string.IsNullOrWhiteSpace(threats))
            {
                ParseSwotFromPlainText(rawResult, out strengths, out weaknesses, out opportunities, out threats);
            }

            // 3. Normaliser chaque liste en tirets propres
            strengths = CleanBulletList(strengths);
            weaknesses = CleanBulletList(weaknesses);
            opportunities = CleanBulletList(opportunities);
            threats = CleanBulletList(threats);
        }

        public static string ExtractJsonObject(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // Chercher dans un bloc markdown ```json ... ``` si présent
            var mdMatch = System.Text.RegularExpressions.Regex.Match(raw, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            string candidate = mdMatch.Success ? mdMatch.Groups[1].Value : raw;

            int firstBrace = candidate.IndexOf('{');
            int lastBrace = candidate.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return candidate.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            // Sinon chercher dans tout le texte
            firstBrace = raw.IndexOf('{');
            lastBrace = raw.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return raw.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            return string.Empty;
        }

        private static string GetStringOrArrayFromElement(JsonElement root, params string[] propNames)
        {
            if (root.ValueKind != JsonValueKind.Object) return string.Empty;

            foreach (var prop in root.EnumerateObject())
            {
                foreach (var name in propNames)
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            return prop.Value.GetString() ?? string.Empty;
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            var list = new System.Collections.Generic.List<string>();
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                var str = item.GetString();
                                if (!string.IsNullOrWhiteSpace(str))
                                {
                                    str = str.Trim();
                                    if (!str.StartsWith("-") && !str.StartsWith("*"))
                                        list.Add("- " + str);
                                    else
                                        list.Add(str);
                                }
                            }
                            return string.Join("\n", list);
                        }
                    }
                }
            }
            return string.Empty;
        }

        public static string ExtractJsonFieldRegex(string text, params string[] keys)
        {
            foreach (var key in keys)
            {
                // Format String: "key": "valeur..."
                var matchStr = System.Text.RegularExpressions.Regex.Match(
                    text,
                    "\"" + key + "\"\\s*:\\s*\"([\\s\\S]*?)\"(?=\\s*,\\s*\"|\\s*\\})",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (matchStr.Success)
                {
                    try { return System.Text.RegularExpressions.Regex.Unescape(matchStr.Groups[1].Value).Trim(); }
                    catch { return matchStr.Groups[1].Value.Trim(); }
                }

                // Format Array: "key": [ "val1", "val2" ]
                var matchArr = System.Text.RegularExpressions.Regex.Match(
                    text,
                    "\"" + key + "\"\\s*:\\s*\\[([\\s\\S]*?)\\]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (matchArr.Success)
                {
                    var items = System.Text.RegularExpressions.Regex.Matches(matchArr.Groups[1].Value, "\"([^\"]*)\"");
                    if (items.Count > 0)
                    {
                        var list = new System.Collections.Generic.List<string>();
                        foreach (System.Text.RegularExpressions.Match m in items)
                        {
                            string val = m.Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                if (!val.StartsWith("-") && !val.StartsWith("*"))
                                    list.Add("- " + val);
                                else
                                    list.Add(val);
                            }
                        }
                        return string.Join("\n", list);
                    }
                }
            }
            return string.Empty;
        }

        public static void ParseSwotFromPlainText(string raw, out string strengths, out string weaknesses, out string opportunities, out string threats)
        {
            strengths = string.Empty;
            weaknesses = string.Empty;
            opportunities = string.Empty;
            threats = string.Empty;

            var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            string currentSection = string.Empty;
            var sList = new System.Collections.Generic.List<string>();
            var wList = new System.Collections.Generic.List<string>();
            var oList = new System.Collections.Generic.List<string>();
            var tList = new System.Collections.Generic.List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                string lower = trimmed.ToLowerInvariant();

                // Détection d'en-tête de section SWOT
                if (lower.Contains("force") || lower.Contains("strength") || lower.Contains("atout"))
                {
                    currentSection = "S";
                    continue;
                }
                if (lower.Contains("faiblesse") || lower.Contains("weakness") || lower.Contains("limite"))
                {
                    currentSection = "W";
                    continue;
                }
                if (lower.Contains("opportunit"))
                {
                    currentSection = "O";
                    continue;
                }
                if (lower.Contains("menace") || lower.Contains("threat") || lower.Contains("risque"))
                {
                    currentSection = "T";
                    continue;
                }

                // Contenu sous la section identifiée
                if (currentSection == "S") sList.Add(trimmed);
                else if (currentSection == "W") wList.Add(trimmed);
                else if (currentSection == "O") oList.Add(trimmed);
                else if (currentSection == "T") tList.Add(trimmed);
            }

            strengths = string.Join("\n", sList);
            weaknesses = string.Join("\n", wList);
            opportunities = string.Join("\n", oList);
            threats = string.Join("\n", tList);
        }

        public static string CleanBulletList(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var formatted = new System.Collections.Generic.List<string>();
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (trimmed.StartsWith("```") || trimmed.StartsWith("{") || trimmed.StartsWith("}") || trimmed.StartsWith("\"")) continue;

                if (!trimmed.StartsWith("-") && !trimmed.StartsWith("*") && !char.IsDigit(trimmed[0]))
                {
                    formatted.Add("- " + trimmed);
                }
                else
                {
                    formatted.Add(trimmed);
                }
            }
            return string.Join("\n", formatted);
        }
    }
}









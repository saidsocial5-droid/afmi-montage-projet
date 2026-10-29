using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class CadreLogiqueViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<Objective> _objectives = new();

        [ObservableProperty]
        private Objective? _selectedObjective;

        [ObservableProperty]
        private string _newResultDescription = string.Empty;

        [ObservableProperty]
        private string _newActivityDescription = string.Empty;

        // Propriétés additionnelles pour les Produits (Outputs)
        [ObservableProperty]
        private ObservableCollection<Output> _availableOutputsForSelect = new();

        [ObservableProperty]
        private Output? _selectedOutputForNewActivity;

        [ObservableProperty]
        private string _newOutputDescription = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // Propriétés pour l'ajout d'indicateurs
        [ObservableProperty]
        private string _newIndicatorCode = string.Empty;

        [ObservableProperty]
        private string _newIndicatorDescription = string.Empty;

        [ObservableProperty]
        private string _newIndicatorBaseline = string.Empty;

        [ObservableProperty]
        private string _newIndicatorTarget = string.Empty;

        [ObservableProperty]
        private string _newIndicatorSource = string.Empty;

        public CadreLogiqueViewModel(Project project)
        {
            _project = project;
            LoadCadreLogique();
        }

        private void LoadCadreLogique()
        {
            using var db = new AppDbContext();
            
            // Charger les objectifs avec leurs résultats, activités, et les indicateurs associés
            var dbObjectives = db.Objectives
                .Where(o => o.ProjectId == _project.Id)
                .Include(o => o.ProposedByUser)
                .Include(o => o.Results).ThenInclude(r => r.ProposedByUser)
                .Include(o => o.Results).ThenInclude(r => r.Activities).ThenInclude(a => a.ProposedByUser)
                .OrderBy(o => o.Code)
                .ToList();

            // Charger les indicateurs et les produits (outputs)
            var allOutputsList = new System.Collections.Generic.List<Output>();
            foreach (var obj in dbObjectives)
            {
                obj.Results = obj.Results.OrderBy(r => r.Code).ToList();
                foreach (var res in obj.Results)
                {
                    // Charger les produits (outputs) pour ce résultat
                    res.Outputs = db.Outputs.Where(o => o.ResultId == res.Id).OrderBy(o => o.Code).ToList();
                    allOutputsList.AddRange(res.Outputs);

                    res.Activities = res.Activities.OrderBy(a => a.Code).ToList();
                    foreach (var act in res.Activities)
                    {
                        // Assigner l'objet Output s'il est configuré
                        if (act.OutputId.HasValue)
                        {
                            act.Output = res.Outputs.FirstOrDefault(o => o.Id == act.OutputId.Value);
                        }

                        // Charger les indicateurs de l'activité
                        act.Indicators = db.Indicators
                            .Where(i => i.ActivityId == act.Id)
                            .ToList();
                            
                        // Calculer le budget prévisionnel
                        act.TotalBudget = db.BudgetItems
                            .Where(b => b.ActivityId == act.Id)
                            .AsEnumerable()
                            .Sum(b => b.TotalCost);
                    }
                }
            }

            Objectives = new ObservableCollection<Objective>(dbObjectives);
            AvailableOutputsForSelect = new ObservableCollection<Output>(allOutputsList);
            
            // Sélectionner le premier objectif par défaut
            if (Objectives.Any() && SelectedObjective == null)
            {
                SelectedObjective = Objectives.First();
            }

            // Sélectionner le premier produit par défaut s'il y en a
            if (AvailableOutputsForSelect.Any() && SelectedOutputForNewActivity == null)
            {
                SelectedOutputForNewActivity = AvailableOutputsForSelect.First();
            }
        }

        [RelayCommand]
        public async Task SuggererResultat()
        {
            if (SelectedObjective == null) return;
            StatusMessage = "Recherche de suggestions...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Suggère 3 propositions de 'Résultat attendu' (1 phrase chacune) pour l'objectif spécifique suivant : {SelectedObjective.Description}. Rédige-les sous forme de tirets. Voici les informations du projet :\n\n{projectContext}";
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

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Résultat attendu", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    NewResultDescription = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
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
        public async Task GenererResultat()
        {
            if (SelectedObjective == null) return;
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Génère un 'Résultat attendu' concret, mesurable et réaliste pour l'objectif spécifique suivant : {SelectedObjective.Description}. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT le texte généré (1 seule phrase courte et claire).";
                
                string result = await AiAssistantService.CallAiAsync(prompt, sys);
                NewResultDescription = result.TrimStart('-', '*', ' ', '\n', '\r').Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggererActivite(Result result)
        {
            if (result == null) return;
            StatusMessage = "Recherche de suggestions...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Suggère 3 propositions d''Activité' (1 phrase chacune) pour atteindre le résultat suivant : {result.Description}. Rédige-les sous forme de tirets. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                
                string aiResult = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = aiResult.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = aiResult.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Activité", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    NewActivityDescription = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
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
        public async Task GenererActivite(Result result)
        {
            if (result == null) return;
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Génère une 'Activité' concrète et réalisable pour atteindre le résultat suivant : {result.Description}. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT le texte généré (1 seule phrase courte).";
                
                string aiResult = await AiAssistantService.CallAiAsync(prompt, sys);
                NewActivityDescription = aiResult.TrimStart('-', '*', ' ', '\n', '\r').Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }
        [RelayCommand]
        public void AddResult()
        {
            if (SelectedObjective == null || string.IsNullOrWhiteSpace(NewResultDescription))
                return;

            try
            {
                using var db = new AppDbContext();
                
                // Calculer le code du résultat (ex: R1.1, R1.2)
                int objIndex = Objectives.IndexOf(SelectedObjective) + 1;
                int resCount = db.Results.Count(r => r.ObjectiveId == SelectedObjective.Id);
                string code = $"R{objIndex}.{resCount + 1}";

                var newRes = new Result
                {
                    ObjectiveId = SelectedObjective.Id,
                    Code = code,
                    Description = NewResultDescription.Trim(),
                    ProposedByUserId = SessionManager.CurrentUser?.Id,
                    Status = SessionManager.CurrentUser != null && SessionManager.CurrentUser.IsSystemAdmin ? "Approved" : "Pending"
                };

                db.Results.Add(newRes);
                db.SaveChanges();

                if (newRes.Status == "Pending")
                {
                    NotificationService.NotifyAdminsOfProject(_project.Id, "Nouveau Résultat proposé", 
                        $"Le résultat '{newRes.Code}' a été proposé par @{SessionManager.CurrentUser?.Username}.");
                }

                // Recharger pour mettre à  jour l'arbre
                LoadCadreLogique();
                
                // Conserver la sélection
                SelectedObjective = Objectives.FirstOrDefault(o => o.Id == SelectedObjective.Id);
                
                NewResultDescription = string.Empty;
                StatusMessage = "Résultat attendu ajouté avec succà¨s.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteResult(Result? result)
        {
            if (result == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbRes = db.Results.FirstOrDefault(r => r.Id == result.Id);
                if (dbRes != null)
                {
                    db.Results.Remove(dbRes);
                    db.SaveChanges();

                    // Réindexer les codes des résultats restants de cet objectif
                    ReindexResults(result.ObjectiveId);
                    
                    int? selObjId = SelectedObjective?.Id;
                    LoadCadreLogique();
                    
                    if (selObjId.HasValue)
                        SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                    StatusMessage = "Résultat attendu supprimé.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        private void ReindexResults(int objectiveId)
        {
            using var db = new AppDbContext();
            var results = db.Results
                .Where(r => r.ObjectiveId == objectiveId)
                .OrderBy(r => r.Id)
                .ToList();

            var obj = db.Objectives.FirstOrDefault(o => o.Id == objectiveId);
            if (obj == null) return;
            
            // Déterminer l'index de l'objectif dans le projet
            var objectives = db.Objectives
                .Where(o => o.ProjectId == _project.Id)
                .OrderBy(o => o.Id)
                .ToList();
            int objIndex = objectives.IndexOf(obj) + 1;

            for (int i = 0; i < results.Count; i++)
            {
                results[i].Code = $"R{objIndex}.{i + 1}";
            }
            db.SaveChanges();
        }

        [RelayCommand]
        public void AddActivity(Result? result)
        {
            if (result == null || string.IsNullOrWhiteSpace(NewActivityDescription))
                return;

            try
            {
                using var db = new AppDbContext();
                
                // Calculer le code de l'activité (ex: A1.1.1, A1.1.2)
                int actCount = db.Activities.Count(a => a.ResultId == result.Id);
                string code = $"A{result.Code.Substring(1)}.{actCount + 1}";

                var newAct = new Activity
                {
                    ResultId = result.Id,
                    Code = code,
                    Description = NewActivityDescription.Trim(),
                    OutputId = SelectedOutputForNewActivity?.Id, // Assigner l'Output s'il y en a un sélectionné
                    ProposedByUserId = SessionManager.CurrentUser?.Id,
                    Status = SessionManager.CurrentUser != null && SessionManager.CurrentUser.IsSystemAdmin ? "Approved" : "Pending"
                };

                db.Activities.Add(newAct);
                db.SaveChanges();

                if (newAct.Status == "Pending")
                {
                    NotificationService.NotifyAdminsOfProject(_project.Id, "Nouvelle Activité proposée", 
                        $"L'activité '{newAct.Code}' a été proposée par @{SessionManager.CurrentUser?.Username}.");
                }

                int? selObjId = SelectedObjective?.Id;
                LoadCadreLogique();
                if (selObjId.HasValue)
                    SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                NewActivityDescription = string.Empty;
                StatusMessage = "Activité ajoutée avec succà¨s.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

                [RelayCommand]
        public async Task SuggererProduit(Result result)
        {
            if (result == null) return;
            StatusMessage = "Recherche de suggestions...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Suggère 3 propositions de 'Produit (Output)' (1 phrase courte chacune) livrables pour atteindre le résultat suivant : {result.Description}. Rédige-les sous forme de tirets. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                
                string aiResult = await AiAssistantService.CallAiAsync(prompt, sys);
                var suggestions = aiResult.Split(new[] { "\n- " }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ', '\n', '\r') })
                                        .ToList();

                if (suggestions.Count <= 1) {
                    suggestions = aiResult.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Where(s => s.Trim().StartsWith("- ") || s.Trim().StartsWith("* "))
                                        .Select(s => new AFMI_Montage_projet_2.Services.AiSuggestion { Text = s.TrimStart('-', '*', ' ') })
                                        .ToList();
                }

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Produit (Output)", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    NewOutputDescription = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
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
        public async Task GenererProduit(Result result)
        {
            if (result == null) return;
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Génère un 'Produit (Output)' concret et livrable pour le résultat suivant : {result.Description}. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en montage de projets associatifs. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT le texte généré (1 seule phrase courte).";
                
                string aiResult = await AiAssistantService.CallAiAsync(prompt, sys);
                NewOutputDescription = aiResult.TrimStart('-', '*', ' ', '\n', '\r').Trim();
                StatusMessage = "Texte généré avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        #region Assistant IA Rédactionnel (Enrichir, Reformuler, Détailler)

        [RelayCommand]
        public async Task EnrichirResultat()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewResult", 
                "Résultat attendu", 
                FieldSemanticType.Resultat, 
                NewResultDescription, 
                TextAssistActionType.Enrichir, 
                txt => NewResultDescription = txt);
        }

        [RelayCommand]
        public async Task ReformulerResultat()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewResult", 
                "Résultat attendu", 
                FieldSemanticType.Resultat, 
                NewResultDescription, 
                TextAssistActionType.Reformuler, 
                txt => NewResultDescription = txt);
        }

        [RelayCommand]
        public async Task DetaillerResultat()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewResult", 
                "Résultat attendu", 
                FieldSemanticType.Resultat, 
                NewResultDescription, 
                TextAssistActionType.Detailler, 
                txt => NewResultDescription = txt);
        }

        [RelayCommand]
        public async Task EnrichirActivite()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewActivity", 
                "Activité", 
                FieldSemanticType.Activite, 
                NewActivityDescription, 
                TextAssistActionType.Enrichir, 
                txt => NewActivityDescription = txt);
        }

        [RelayCommand]
        public async Task ReformulerActivite()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewActivity", 
                "Activité", 
                FieldSemanticType.Activite, 
                NewActivityDescription, 
                TextAssistActionType.Reformuler, 
                txt => NewActivityDescription = txt);
        }

        [RelayCommand]
        public async Task DetaillerActivite()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewActivity", 
                "Activité", 
                FieldSemanticType.Activite, 
                NewActivityDescription, 
                TextAssistActionType.Detailler, 
                txt => NewActivityDescription = txt);
        }

        [RelayCommand]
        public async Task EnrichirProduit()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewOutput", 
                "Produit (Output)", 
                FieldSemanticType.Activite, 
                NewOutputDescription, 
                TextAssistActionType.Enrichir, 
                txt => NewOutputDescription = txt);
        }

        [RelayCommand]
        public async Task ReformulerProduit()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewOutput", 
                "Produit (Output)", 
                FieldSemanticType.Activite, 
                NewOutputDescription, 
                TextAssistActionType.Reformuler, 
                txt => NewOutputDescription = txt);
        }

        [RelayCommand]
        public async Task DetaillerProduit()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewOutput", 
                "Produit (Output)", 
                FieldSemanticType.Activite, 
                NewOutputDescription, 
                TextAssistActionType.Detailler, 
                txt => NewOutputDescription = txt);
        }

        [RelayCommand]
        public async Task EnrichirIndicateur()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewIndicator", 
                "Indicateur", 
                FieldSemanticType.Indicateur, 
                NewIndicatorDescription, 
                TextAssistActionType.Enrichir, 
                txt => NewIndicatorDescription = txt);
        }

        [RelayCommand]
        public async Task ReformulerIndicateur()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewIndicator", 
                "Indicateur", 
                FieldSemanticType.Indicateur, 
                NewIndicatorDescription, 
                TextAssistActionType.Reformuler, 
                txt => NewIndicatorDescription = txt);
        }

        [RelayCommand]
        public async Task DetaillerIndicateur()
        {
            await OpenTextAssistDialog(
                "CadreLogique.NewIndicator", 
                "Indicateur", 
                FieldSemanticType.Indicateur, 
                NewIndicatorDescription, 
                TextAssistActionType.Detailler, 
                txt => NewIndicatorDescription = txt);
        }

        private async Task OpenTextAssistDialog(string id, string label, FieldSemanticType semanticType, string currentText, TextAssistActionType action, Action<string> onApplied)
        {
            var descriptor = new FieldDescriptor(id, label, semanticType);
            var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: currentText ?? string.Empty,
                isSelection: false,
                initialAction: action,
                projectId: _project?.Id);

            var ownerWindow = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                              ?? System.Windows.Application.Current?.MainWindow;
            if (ownerWindow != null) dialog.Owner = ownerWindow;

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                onApplied(dialog.ResultText);
                StatusMessage = $"{label} mis à jour avec succès via l'assistant IA !";
            }
            await Task.CompletedTask;
        }

        #endregion

        [RelayCommand]
        public void AddOutput(Result? result)
        {
            if (result == null || string.IsNullOrWhiteSpace(NewOutputDescription))
                return;

            try
            {
                using var db = new AppDbContext();
                int outCount = db.Outputs.Count(o => o.ResultId == result.Id);
                string code = $"P{result.Code.Substring(1)}.{outCount + 1}";

                var newOut = new Output
                {
                    ResultId = result.Id,
                    Code = code,
                    Description = NewOutputDescription.Trim()
                };

                db.Outputs.Add(newOut);
                db.SaveChanges();

                int? selObjId = SelectedObjective?.Id;
                LoadCadreLogique();
                if (selObjId.HasValue)
                    SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                NewOutputDescription = string.Empty;
                StatusMessage = "Produit (Output) ajouté avec succà¨s.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteOutput(Output? output)
        {
            if (output == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbOut = db.Outputs.FirstOrDefault(o => o.Id == output.Id);
                if (dbOut != null)
                {
                    db.Outputs.Remove(dbOut);
                    db.SaveChanges();

                    int? selObjId = SelectedObjective?.Id;
                    LoadCadreLogique();
                    if (selObjId.HasValue)
                        SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                    StatusMessage = "Produit supprimé.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteActivity(Activity? activity)
        {
            if (activity == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbAct = db.Activities.FirstOrDefault(a => a.Id == activity.Id);
                if (dbAct != null)
                {
                    db.Activities.Remove(dbAct);
                    db.SaveChanges();

                    ReindexActivities(activity.ResultId);

                    int? selObjId = SelectedObjective?.Id;
                    LoadCadreLogique();
                    if (selObjId.HasValue)
                        SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                    StatusMessage = "Activité supprimée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        private void ReindexActivities(int resultId)
        {
            using var db = new AppDbContext();
            var activities = db.Activities
                .Where(a => a.ResultId == resultId)
                .OrderBy(a => a.Id)
                .ToList();

            var result = db.Results.FirstOrDefault(r => r.Id == resultId);
            if (result == null) return;

            for (int i = 0; i < activities.Count; i++)
            {
                activities[i].Code = $"A{result.Code.Substring(1)}.{i + 1}";
            }
            db.SaveChanges();
        }

        [RelayCommand]
        public void AddIndicator(Activity? activity)
        {
            if (activity == null || string.IsNullOrWhiteSpace(NewIndicatorDescription))
                return;

            try
            {
                using var db = new AppDbContext();
                
                // Code de l'indicateur (ex: Ind-A1.1.1)
                string code = string.IsNullOrWhiteSpace(NewIndicatorCode) 
                    ? $"Ind-{activity.Code}" 
                    : NewIndicatorCode.Trim();

                var newInd = new Indicator
                {
                    ActivityId = activity.Id,
                    Code = code,
                    Description = NewIndicatorDescription.Trim(),
                    Baseline = NewIndicatorBaseline?.Trim() ?? string.Empty,
                    Target = NewIndicatorTarget?.Trim() ?? string.Empty,
                    VerificationSource = NewIndicatorSource?.Trim() ?? string.Empty
                };

                db.Indicators.Add(newInd);
                db.SaveChanges();

                int? selObjId = SelectedObjective?.Id;
                LoadCadreLogique();
                if (selObjId.HasValue)
                    SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                // Réinitialiser les champs
                NewIndicatorCode = string.Empty;
                NewIndicatorDescription = string.Empty;
                NewIndicatorBaseline = string.Empty;
                NewIndicatorTarget = string.Empty;
                NewIndicatorSource = string.Empty;

                StatusMessage = "Indicateur SMART ajouté à  l'activité.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'ajout de l'indicateur : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteIndicator(Indicator? indicator)
        {
            if (indicator == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbInd = db.Indicators.FirstOrDefault(i => i.Id == indicator.Id);
                if (dbInd != null)
                {
                    db.Indicators.Remove(dbInd);
                    db.SaveChanges();

                    int? selObjId = SelectedObjective?.Id;
                    LoadCadreLogique();
                    if (selObjId.HasValue)
                        SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                    StatusMessage = "Indicateur supprimé.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggereIndicateurs(Activity? activity)
        {
            if (activity == null) return;

            StatusMessage = "Interrogation de l'IA pour suggérer un indicateur...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"{projectContext}\n\nActivité : {activity.Description}";
                
                string result = await AiAssistantService.CallAiAsync(
                    prompt,
                    "Tu es un expert en suivi-évaluation pour des projets d'ONG. En tenant compte des exigences éventuelles du bailleur (si spécifiées), suggà¨re pour l'activité fournie 1 indicateur SMART réaliste sous un format structuré et précis. Renvoie UNIQUEMENT la réponse sous la forme suivante (une seule ligne, sans introduction, sans astérisque, sans commentaires) :\nIndicateur: [nom de l'indicateur] | Réf: [valeur de référence de départ, ex: 0 ou Néant] | Cible: [valeur cible réaliste] | Source: [source de vérification, ex: Rapport d'activité ou Fiches de présence]"
                );

                // Parser le résultat (format: Indicateur: X | Réf: Y | Cible: Z | Source: W)
                string[] parts = result.Split('|');
                if (parts.Length >= 4)
                {
                    NewIndicatorCode = $"Ind-{activity.Code}";
                    NewIndicatorDescription = parts[0].Replace("Indicateur:", "").Trim();
                    NewIndicatorBaseline = parts[1].Replace("Réf:", "").Trim();
                    NewIndicatorTarget = parts[2].Replace("Cible:", "").Trim();
                    NewIndicatorSource = parts[3].Replace("Source:", "").Trim();
                    StatusMessage = "Indicateur suggéré par l'IA ! Vous pouvez maintenant le valider et l'ajouter.";
                }
                else
                {
                    // Si le format n'est pas respecté, on met tout dans la description pour que l'utilisateur puisse le voir
                    NewIndicatorCode = $"Ind-{activity.Code}";
                    NewIndicatorDescription = result.Trim();
                    NewIndicatorBaseline = "0";
                    NewIndicatorTarget = "à€ définir";
                    NewIndicatorSource = "Rapports";
                    StatusMessage = "Indicateur généré (format brut).";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        [RelayCommand]
        public void ApproveResult(Result? result)
        {
            if (result == null) return;
            UpdateStatus(result, "Approved");
        }

        [RelayCommand]
        public void ApproveAndAnonymizeResult(Result? result)
        {
            if (result == null) return;
            UpdateStatus(result, "Approved", true);
        }

        [RelayCommand]
        public void RejectResult(Result? result)
        {
            if (result == null) return;
            UpdateStatus(result, "Rejected");
        }

        [RelayCommand]
        public void ApproveActivity(Activity? activity)
        {
            if (activity == null) return;
            UpdateStatus(activity, "Approved");
        }

        [RelayCommand]
        public void ApproveAndAnonymizeActivity(Activity? activity)
        {
            if (activity == null) return;
            UpdateStatus(activity, "Approved", true);
        }

        [RelayCommand]
        public void RejectActivity(Activity? activity)
        {
            if (activity == null) return;
            UpdateStatus(activity, "Rejected");
        }

        private void UpdateStatus(object item, string status, bool anonymize = false)
        {
            try
            {
                using var db = new AppDbContext();
                if (item is Result r)
                {
                    var dbItem = db.Results.Find(r.Id);
                    if (dbItem != null)
                    {
                        dbItem.Status = status;
                        if (anonymize) dbItem.IsAnonymized = true;
                        
                        // Add audit log
                        db.AuditLogs.Add(new AuditLog {
                            EntityType = "Result", EntityId = r.Id, ActionUserId = SessionManager.CurrentUser?.Id ?? 0,
                            ActionType = anonymize ? "Anonymize" : status, Timestamp = DateTime.UtcNow
                        });
                    }
                }
                else if (item is Activity a)
                {
                    var dbItem = db.Activities.Find(a.Id);
                    if (dbItem != null)
                    {
                        dbItem.Status = status;
                        if (anonymize) dbItem.IsAnonymized = true;

                        db.AuditLogs.Add(new AuditLog {
                            EntityType = "Activity", EntityId = a.Id, ActionUserId = SessionManager.CurrentUser?.Id ?? 0,
                            ActionType = anonymize ? "Anonymize" : status, Timestamp = DateTime.UtcNow
                        });
                    }
                }

                db.SaveChanges();
                int? selObjId = SelectedObjective?.Id;
                LoadCadreLogique();
                if (selObjId.HasValue)
                    SelectedObjective = Objectives.FirstOrDefault(o => o.Id == selObjId.Value);

                StatusMessage = $"Statut mis à  jour : {status}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }
    }
}




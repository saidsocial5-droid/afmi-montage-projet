using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class IndicateursViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<Indicator> _indicators = new();

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        public IndicateursViewModel(Project project)
        {
            _project = project;
            LoadIndicators();
        }

        private void LoadIndicators()
        {
            try
            {
                using var db = new AppDbContext();
                
                // Charger tous les indicateurs rattachés au projet directement OU via ses activités
                var dbIndicators = db.Indicators
                    .Include(i => i.Activity)
                        .ThenInclude(a => a!.Result)
                            .ThenInclude(r => r!.Objective)
                    .Where(i => i.ProjectId == _project.Id || (i.Activity != null && i.Activity.Result!.Objective!.ProjectId == _project.Id))
                    .ToList();

                Indicators = new ObservableCollection<Indicator>(dbIndicators);
                StatusMessage = $"{Indicators.Count} indicateur(s) chargé(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors du chargement des indicateurs : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SaveIndicators()
        {
            try
            {
                using var db = new AppDbContext();

                // Parcourir les indicateurs de notre liste
                foreach (var indicator in Indicators)
                {
                    if (indicator.Id == 0)
                    {
                        // Nouvel indicateur global
                        db.Indicators.Add(indicator);
                    }
                    else
                    {
                        // Indicateur existant à mettre à jour
                        var dbInd = db.Indicators.FirstOrDefault(i => i.Id == indicator.Id);
                        if (dbInd != null)
                        {
                            dbInd.Description = indicator.Description;
                            dbInd.Baseline = indicator.Baseline;
                            dbInd.Target = indicator.Target;
                            dbInd.VerificationSource = indicator.VerificationSource;
                            dbInd.CollectionMethod = indicator.CollectionMethod;
                            dbInd.Frequency = indicator.Frequency;
                            dbInd.ResponsibleRole = indicator.ResponsibleRole;
                        }
                    }
                }

                db.SaveChanges();
                StatusMessage = "Tous les indicateurs ont été sauvegardés avec succès !";
                LoadIndicators(); // Recharger pour avoir les bons IDs
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la sauvegarde : {ex.Message}";
            }
        }

        [RelayCommand]
        public void AddGlobalIndicator()
        {
            int nextNum = Indicators.Count(i => i.Code.StartsWith("IND-G-")) + 1;
            
            var newInd = new Indicator
            {
                ProjectId = _project.Id,
                Code = $"IND-G-{nextNum:00}",
                Description = "Saisir la description de l'indicateur global...",
                Baseline = "0",
                Target = "100",
                VerificationSource = "Rapport d'activité",
                Frequency = "Trimestrielle",
                ResponsibleRole = "Responsable Suivi-Évaluation"
            };

            Indicators.Add(newInd);
            StatusMessage = $"Indicateur global {newInd.Code} ajouté. Pensez à enregistrer.";
        }

        [RelayCommand]
        public void DeleteIndicator(Indicator indicator)
        {
            if (indicator == null) return;

            try
            {
                using var db = new AppDbContext();
                
                if (indicator.Id != 0)
                {
                    var dbInd = db.Indicators.FirstOrDefault(i => i.Id == indicator.Id);
                    if (dbInd != null)
                    {
                        db.Indicators.Remove(dbInd);
                        db.SaveChanges();
                    }
                }

                Indicators.Remove(indicator);
                StatusMessage = $"Indicateur {indicator.Code} supprimé avec succès.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la suppression : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task SuggereIndicateursIa()
        {
            IsBusy = true;
            StatusMessage = "L'Agent IA génère des propositions d'indicateurs globaux...";

            try
            {
                string projectContext = $"Titre du projet : {_project.Title}\n" +
                                        $"Objectif général : {_project.GeneralObjective}\n" +
                                        $"Contexte & Justification : {_project.ContextJustification}";

                string prompt = $"Voici les informations de cadrage d'un projet de développement :\n\n" +
                                $"{projectContext}\n\n" +
                                "Propose exactement 3 indicateurs globaux stratégiques pour le suivi-évaluation de ce projet.\n" +
                                "Renvoie ta réponse uniquement sous forme d'un tableau JSON valide, sans balises markdown, sans texte d'introduction ni de conclusion, sous ce format :\n" +
                                "[\n" +
                                "  { \"description\": \"Description de l'indicateur 1\", \"baseline\": \"valeur de départ\", \"target\": \"valeur cible\", \"source\": \"source de vérification\" },\n" +
                                "  { \"description\": \"Description de l'indicateur 2\", \"baseline\": \"valeur de départ\", \"target\": \"valeur cible\", \"source\": \"source de vérification\" },\n" +
                                "  { \"description\": \"Description de l'indicateur 3\", \"baseline\": \"valeur de départ\", \"target\": \"valeur cible\", \"source\": \"source de vérification\" }\n" +
                                "]";

                string systemInstruction = "Tu es un spécialiste en suivi-évaluation de projets internationaux. Tu crées des indicateurs SMART (Spécifiques, Mesurables, Atteignables, Réalistes, Temporellement définis). Tu réponds exclusivement en JSON valide.";

                string result = await AiAssistantService.CallAiAsync(prompt, systemInstruction);

                // Parser le JSON
                string jsonCleaned = CleanJsonResult(result);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var suggestions = JsonSerializer.Deserialize<IndicatorSuggestion[]>(jsonCleaned, options);

                if (suggestions != null && suggestions.Length > 0)
                {
                    using var db = new AppDbContext();
                    int count = 0;
                    
                    foreach (var sug in suggestions)
                    {
                        count++;
                        int nextNum = Indicators.Count(i => i.Code.StartsWith("IND-G-")) + 1;
                        var newInd = new Indicator
                        {
                            ProjectId = _project.Id,
                            Code = $"IND-G-{nextNum:00}",
                            Description = sug.Description ?? "Indicateur suggéré",
                            Baseline = sug.Baseline ?? "0",
                            Target = sug.Target ?? "100",
                            VerificationSource = sug.Source ?? "Rapport de suivi",
                            Frequency = "Trimestrielle",
                            ResponsibleRole = "Responsable Suivi-Évaluation"
                        };

                        Indicators.Add(newInd);
                        db.Indicators.Add(newInd);
                    }

                    db.SaveChanges();
                    StatusMessage = $"{suggestions.Length} indicateurs suggérés par l'IA ont été ajoutés et sauvegardés !";
                }
                else
                {
                    StatusMessage = "L'IA a répondu mais aucun indicateur n'a pu être extrait. Veuillez réessayer.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la suggestion IA : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task EnrichirIndicateurs()
        {
            if (Indicators.Count == 0)
            {
                StatusMessage = "Aucun indicateur à enrichir. Ajoutez d'abord un indicateur ou utilisez Suggérer.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Enrichissement des indicateurs en cours avec le contexte du projet...";

                var textAssist = new TextAssistService();
                int updatedCount = 0;

                foreach (var ind in Indicators)
                {
                    if (string.IsNullOrWhiteSpace(ind.Description)) continue;

                    var descriptor = new FieldDescriptor(
                        $"indicator_{ind.Id}",
                        $"Indicateur {ind.Code}",
                        FieldSemanticType.Indicateur)
                    {
                        EntityId = ind.Id
                    };

                    var res = await textAssist.ProcessActionAsync(
                        TextAssistActionType.Enrichir,
                        ind.Description,
                        descriptor,
                        projectId: _project.Id);

                    if (res.IsSuccess && !string.IsNullOrWhiteSpace(res.Proposal))
                    {
                        ind.Description = res.Proposal;
                        updatedCount++;
                    }
                }

                SaveIndicators();
                StatusMessage = $"{updatedCount} indicateur(s) enrichi(s) et sauvegardé(s) avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'enrichissement : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ReformulerIndicateurs()
        {
            if (Indicators.Count == 0)
            {
                StatusMessage = "Aucun indicateur à reformuler.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Reformulation SMART des indicateurs en cours...";

                var textAssist = new TextAssistService();
                int updatedCount = 0;

                foreach (var ind in Indicators)
                {
                    if (string.IsNullOrWhiteSpace(ind.Description)) continue;

                    var descriptor = new FieldDescriptor(
                        $"indicator_{ind.Id}",
                        $"Indicateur {ind.Code}",
                        FieldSemanticType.Indicateur)
                    {
                        EntityId = ind.Id
                    };

                    var res = await textAssist.ProcessActionAsync(
                        TextAssistActionType.Reformuler,
                        ind.Description,
                        descriptor,
                        projectId: _project.Id);

                    if (res.IsSuccess && !string.IsNullOrWhiteSpace(res.Proposal))
                    {
                        ind.Description = res.Proposal;
                        updatedCount++;
                    }
                }

                SaveIndicators();
                StatusMessage = $"{updatedCount} indicateur(s) reformulé(s) selon les normes SMART et sauvegardé(s) !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la reformulation : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string CleanJsonResult(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson)) return "[]";
            string cleaned = rawJson.Trim();

            // Retirer bloc markdown si présent
            var matchMarkdown = System.Text.RegularExpressions.Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matchMarkdown.Success)
            {
                cleaned = matchMarkdown.Groups[1].Value.Trim();
            }

            // Extraire entre le premier [ et le dernier ]
            int firstBracket = cleaned.IndexOf('[');
            int lastBracket = cleaned.LastIndexOf(']');
            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                cleaned = cleaned.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

            return cleaned.Trim();
        }

        private class IndicatorSuggestion
        {
            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("baseline")]
            public string? Baseline { get; set; }

            [JsonPropertyName("target")]
            public string? Target { get; set; }

            [JsonPropertyName("source")]
            public string? Source { get; set; }
        }
    }
}


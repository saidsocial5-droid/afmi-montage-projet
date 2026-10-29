using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Views;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class RisquesViewModel : ObservableObject
    {
        private readonly Project _project;
        private readonly RiskAiService _riskAiService = new();

        [ObservableProperty]
        private ObservableCollection<Risk> _risks = new();

        [ObservableProperty]
        private Risk? _selectedRisk;

        // Propriétés du formulaire d'édition
        [ObservableProperty]
        private string _category = "Interne";

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private int _probability = 3;

        [ObservableProperty]
        private int _impact = 3;

        [ObservableProperty]
        private string _mitigationMeasure = string.Empty;

        [ObservableProperty]
        private string _responsibleRole = string.Empty;

        [ObservableProperty]
        private string _status = "Actif";

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isAiBusy;

        public int CriticalityScore => Probability * Impact;

        public string CriticalityText => CriticalityScore switch
        {
            >= 12 => $"Score : {CriticalityScore} / 25 — Critique (Zone Rouge)",
            >= 6 => $"Score : {CriticalityScore} / 25 — Modéré (Zone Orange)",
            _ => $"Score : {CriticalityScore} / 25 — Faible (Zone Verte)"
        };

        public string CriticalityBrush => CriticalityScore switch
        {
            >= 12 => "#EF4444",
            >= 6 => "#F59E0B",
            _ => "#10B981"
        };

        partial void OnProbabilityChanged(int value)
        {
            OnPropertyChanged(nameof(CriticalityScore));
            OnPropertyChanged(nameof(CriticalityText));
            OnPropertyChanged(nameof(CriticalityBrush));
        }

        partial void OnImpactChanged(int value)
        {
            OnPropertyChanged(nameof(CriticalityScore));
            OnPropertyChanged(nameof(CriticalityText));
            OnPropertyChanged(nameof(CriticalityBrush));
        }

        // Matrice 5x5 pour la Heatmap (dictionnaire indexé par "Probabilité_Impact")
        public ObservableCollection<HeatmapCell> HeatmapCells { get; set; } = new();

        public ObservableCollection<string> Categories { get; } = new()
        {
            "Interne", "Externe", "Financier", "Social", "Environnemental", "Technique", "Gouvernance"
        };

        public ObservableCollection<string> Statuses { get; } = new()
        {
            "Actif", "Atténué", "Clos"
        };

        public RisquesViewModel(Project project)
        {
            _project = project;
            LoadRisks();
        }

        private void LoadRisks()
        {
            try
            {
                using var db = new AppDbContext();
                var list = db.Risks.Where(r => r.ProjectId == _project.Id).ToList();
                Risks = new ObservableCollection<Risk>(list);
                UpdateHeatmap();
                StatusMessage = "Registre des risques chargé.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors du chargement : {ex.Message}";
            }
        }

        private void UpdateHeatmap()
        {
            HeatmapCells.Clear();
            // Créer une matrice 5x5 vide
            for (int p = 5; p >= 1; p--) // Lignes (Probabilité descendante)
            {
                for (int i = 1; i <= 5; i++) // Colonnes (Impact croissant)
                {
                    int count = Risks.Count(r => r.Probability == p && r.Impact == i && r.Status == "Actif");
                    
                    // Déterminer la couleur de la cellule
                    string color = "#22C55E"; // Vert (Faible)
                    int score = p * i;
                    if (score >= 12)
                        color = "#EF4444"; // Rouge (Critique)
                    else if (score >= 6)
                        color = "#F59E0B"; // Orange/Jaune (Moyen)

                    HeatmapCells.Add(new HeatmapCell
                    {
                        Probability = p,
                        Impact = i,
                        Count = count,
                        ColorBrush = color
                    });
                }
            }
        }

        #region Assistance IA Globale pour la Section Risques

        /// <summary>
        /// Génère automatiquement une liste complète de risques spécifiques au projet avec cotation et atténuation.
        /// </summary>
        [RelayCommand]
        public async Task AutoGenerateRisks()
        {
            IsAiBusy = true;
            StatusMessage = "🤖 L'IA analyse votre projet et génère le registre des risques...";

            try
            {
                var proposals = await _riskAiService.GenerateRisksAsync(_project.Id);
                IsAiBusy = false;

                if (proposals == null || proposals.Count == 0)
                {
                    StatusMessage = "Aucun risque n'a pu être généré.";
                    return;
                }

                var dialog = new AiRiskGenerationDialog(proposals);
                var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                            ?? Application.Current?.MainWindow;
                if (owner != null) dialog.Owner = owner;

                if (dialog.ShowDialog() == true && dialog.SelectedProposals.Count > 0)
                {
                    using var db = new AppDbContext();
                    int addedCount = 0;

                    foreach (var p in dialog.SelectedProposals)
                    {
                        var newRisk = new Risk
                        {
                            ProjectId = _project.Id,
                            Category = p.Category,
                            Description = p.Description.Trim(),
                            Probability = p.Probability,
                            Impact = p.Impact,
                            MitigationMeasure = p.MitigationMeasure.Trim(),
                            ResponsibleRole = p.ResponsibleRole.Trim(),
                            Status = "Actif",
                            CreatedAt = DateTime.UtcNow
                        };

                        db.Risks.Add(newRisk);
                        Risks.Add(newRisk);
                        addedCount++;
                    }

                    await db.SaveChangesAsync();
                    UpdateHeatmap();
                    StatusMessage = $"🎉 {addedCount} nouveau(x) risque(s) importé(s) avec succès dans le registre !";
                }
                else
                {
                    StatusMessage = "Génération des risques annulée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la génération IA : {ex.Message}";
            }
            finally
            {
                IsAiBusy = false;
            }
        }

        /// <summary>
        /// Audite et diagnostique la matrice des risques actuelle du projet pour les bailleurs de fonds.
        /// </summary>
        [RelayCommand]
        public async Task AuditRisks()
        {
            IsAiBusy = true;
            StatusMessage = "🛡️ L'IA audite la matrice et la couverture des risques du projet...";

            try
            {
                var report = await _riskAiService.AuditRiskMatrixAsync(Risks.ToList(), _project.Id);
                IsAiBusy = false;

                var dialog = new AiRiskAuditDialog(report);
                var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                            ?? Application.Current?.MainWindow;
                if (owner != null) dialog.Owner = owner;

                dialog.ShowDialog();
                StatusMessage = "Rapport d'audit de la matrice des risques généré.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'audit IA : {ex.Message}";
            }
            finally
            {
                IsAiBusy = false;
            }
        }

        /// <summary>
        /// Propose automatiquement une mesure d'atténuation et un rôle responsable adaptés au risque en cours de saisie.
        /// </summary>
        [RelayCommand]
        public async Task ProposerMitigationIa()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                StatusMessage = "Veuillez d'abord saisir une description du risque.";
                return;
            }

            IsAiBusy = true;
            StatusMessage = "🤖 L'IA conçoit une mesure d'atténuation adaptée...";

            try
            {
                var (mitigation, role) = await _riskAiService.SuggestMitigationAsync(Description, Category, _project.Id);
                if (!string.IsNullOrWhiteSpace(mitigation))
                {
                    MitigationMeasure = mitigation;
                    if (string.IsNullOrWhiteSpace(ResponsibleRole) || ResponsibleRole == "Chef de projet")
                    {
                        ResponsibleRole = role;
                    }
                    StatusMessage = "Mesure d'atténuation et responsable proposés par l'IA !";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsAiBusy = false;
            }
        }

        /// <summary>
        /// Évalue et calibre automatiquement la Probabilité et l'Impact du risque en cours de saisie.
        /// </summary>
        [RelayCommand]
        public async Task EstimerScoreIa()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                StatusMessage = "Veuillez d'abord saisir une description du risque pour l'évaluer.";
                return;
            }

            IsAiBusy = true;
            StatusMessage = "🤖 L'IA évalue la sévérité et la probabilité du risque...";

            try
            {
                var (p, i, exp) = await _riskAiService.EstimateScoreAsync(Description, _project.Id);
                Probability = p;
                Impact = i;
                StatusMessage = $"Évaluation IA : Probabilité={p}, Impact={i} ({(string.IsNullOrWhiteSpace(exp) ? "Calibré" : exp)})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsAiBusy = false;
            }
        }

        #endregion

        [RelayCommand]
        public async Task SuggererRisque()
        {
            StatusMessage = "Recherche de suggestions...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Suggère 3 risques potentiels majeurs pour ce projet. Rédige-les sous forme de tirets. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en gestion de risques de projets. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                
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

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Risque", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    Description = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
                    StatusMessage = "Description mise à jour avec succès !";
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
        public async Task GenererRisque()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Génère une description très concrète d'un risque majeur pour ce projet. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert en gestion de risques. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction, donne UNIQUEMENT le texte (1 seule phrase très courte).";
                
                string aiResult = await AiAssistantService.CallAiAsync(prompt, sys);
                Description = aiResult.TrimStart('-', '*', ' ', '\n', '\r').Trim();
                StatusMessage = "Description générée avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        #region Assistant IA Rédactionnel (Enrichir, Reformuler, Détailler)

        [RelayCommand]
        public async Task EnrichirRisque()
        {
            await OpenTextAssistDialog(
                "Risques.Description", 
                "Description du risque", 
                FieldSemanticType.Risque, 
                Description, 
                TextAssistActionType.Enrichir, 
                txt => Description = txt);
        }

        [RelayCommand]
        public async Task ReformulerRisque()
        {
            await OpenTextAssistDialog(
                "Risques.Description", 
                "Description du risque", 
                FieldSemanticType.Risque, 
                Description, 
                TextAssistActionType.Reformuler, 
                txt => Description = txt);
        }

        [RelayCommand]
        public async Task DetaillerRisque()
        {
            await OpenTextAssistDialog(
                "Risques.Description", 
                "Description du risque", 
                FieldSemanticType.Risque, 
                Description, 
                TextAssistActionType.Detailler, 
                txt => Description = txt);
        }

        [RelayCommand]
        public async Task EnrichirMitigation()
        {
            await OpenTextAssistDialog(
                "Risques.MitigationMeasure", 
                "Mesure d'atténuation", 
                FieldSemanticType.Hypothese, 
                MitigationMeasure, 
                TextAssistActionType.Enrichir, 
                txt => MitigationMeasure = txt);
        }

        [RelayCommand]
        public async Task ReformulerMitigation()
        {
            await OpenTextAssistDialog(
                "Risques.MitigationMeasure", 
                "Mesure d'atténuation", 
                FieldSemanticType.Hypothese, 
                MitigationMeasure, 
                TextAssistActionType.Reformuler, 
                txt => MitigationMeasure = txt);
        }

        [RelayCommand]
        public async Task DetaillerMitigation()
        {
            await OpenTextAssistDialog(
                "Risques.MitigationMeasure", 
                "Mesure d'atténuation", 
                FieldSemanticType.Hypothese, 
                MitigationMeasure, 
                TextAssistActionType.Detailler, 
                txt => MitigationMeasure = txt);
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

            var ownerWindow = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                              ?? Application.Current?.MainWindow;
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
        public void SaveRisk()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                StatusMessage = "Veuillez saisir une description pour le risque.";
                return;
            }

            try
            {
                using var db = new AppDbContext();

                if (SelectedRisk == null)
                {
                    // Ajouter un risque
                    var newRisk = new Risk
                    {
                        ProjectId = _project.Id,
                        Category = Category,
                        Description = Description.Trim(),
                        Probability = Probability,
                        Impact = Impact,
                        MitigationMeasure = MitigationMeasure.Trim(),
                        ResponsibleRole = ResponsibleRole.Trim(),
                        Status = Status
                    };

                    db.Risks.Add(newRisk);
                    db.SaveChanges();
                    Risks.Add(newRisk);
                    StatusMessage = "Nouveau risque ajouté avec succès !";
                }
                else
                {
                    // Modifier le risque sélectionné
                    var dbRisk = db.Risks.FirstOrDefault(r => r.Id == SelectedRisk.Id);
                    if (dbRisk != null)
                    {
                        dbRisk.Category = Category;
                        dbRisk.Description = Description.Trim();
                        dbRisk.Probability = Probability;
                        dbRisk.Impact = Impact;
                        dbRisk.MitigationMeasure = MitigationMeasure.Trim();
                        dbRisk.ResponsibleRole = ResponsibleRole.Trim();
                        dbRisk.Status = Status;

                        db.SaveChanges();

                        // Mettre à jour dans la liste locale
                        SelectedRisk.Category = Category;
                        SelectedRisk.Description = Description;
                        SelectedRisk.Probability = Probability;
                        SelectedRisk.Impact = Impact;
                        SelectedRisk.MitigationMeasure = MitigationMeasure;
                        SelectedRisk.ResponsibleRole = ResponsibleRole;
                        SelectedRisk.Status = Status;

                        // Notifier WPF du changement de propriétés
                        var index = Risks.IndexOf(SelectedRisk);
                        if (index >= 0)
                        {
                            var temp = SelectedRisk;
                            Risks.RemoveAt(index);
                            Risks.Insert(index, temp);
                            SelectedRisk = temp;
                        }

                        StatusMessage = "Risque mis à jour avec succès !";
                    }
                }

                ClearForm();
                UpdateHeatmap();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la sauvegarde : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteRisk()
        {
            if (SelectedRisk == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbRisk = db.Risks.FirstOrDefault(r => r.Id == SelectedRisk.Id);
                if (dbRisk != null)
                {
                    db.Risks.Remove(dbRisk);
                    db.SaveChanges();
                    Risks.Remove(SelectedRisk);
                    StatusMessage = "Risque supprimé avec succès.";
                }

                ClearForm();
                UpdateHeatmap();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur de suppression : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SelectRisk(Risk? risk)
        {
            SelectedRisk = risk;
            if (risk != null)
            {
                Category = risk.Category;
                Description = risk.Description;
                Probability = risk.Probability;
                Impact = risk.Impact;
                MitigationMeasure = risk.MitigationMeasure;
                ResponsibleRole = risk.ResponsibleRole;
                Status = risk.Status;
            }
            else
            {
                ClearForm();
            }
        }

        [RelayCommand]
        public void ClearForm()
        {
            SelectedRisk = null;
            Category = "Interne";
            Description = string.Empty;
            Probability = 3;
            Impact = 3;
            MitigationMeasure = string.Empty;
            ResponsibleRole = string.Empty;
            Status = "Actif";
        }
    }

    public class HeatmapCell
    {
        public int Probability { get; set; }
        public int Impact { get; set; }
        public int Count { get; set; }
        public string ColorBrush { get; set; } = "#FFFFFF";

        public string Label => Count > 0 ? Count.ToString() : "";
    }
}

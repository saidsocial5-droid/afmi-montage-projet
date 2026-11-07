using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.Views
{
    public class BatchWorkItem
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CurrentText { get; set; } = string.Empty;
        public string ProposedText { get; set; } = string.Empty;
        public FieldSemanticType SemanticType { get; set; } = FieldSemanticType.Generique;
        public int? ParentId { get; set; }
        public bool IsProcessed { get; set; }
        public bool WasApplied { get; set; }
    }

    public partial class AiBatchAssistDialog : Window
    {
        private readonly TextAssistService _service = new();
        private readonly int _projectId;
        private List<BatchWorkItem> _items = new();
        private int _currentIndex = -1;
        private TextAssistActionType _selectedAction = TextAssistActionType.Enrichir;
        private int _appliedCount = 0;

        public AiBatchAssistDialog(int projectId)
        {
            InitializeComponent();
            _projectId = projectId;

            using var db = new AppDbContext();
            var proj = db.Projects.FirstOrDefault(p => p.Id == _projectId);
            TxtBatchScopeInfo.Text = proj != null ? $"Projet : « {proj.Title} »" : $"Projet #{_projectId}";
        }

        private TextAssistActionType GetSelectedAction()
        {
            int idx = CbBatchAction.SelectedIndex;
            return idx switch
            {
                1 => TextAssistActionType.Detailler,
                2 => TextAssistActionType.Reformuler,
                _ => TextAssistActionType.Enrichir
            };
        }

        private async void BtnStartBatch_Click(object sender, RoutedEventArgs e)
        {
            _selectedAction = GetSelectedAction();
            string scope = (CbBatchScope.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tous les Indicateurs SMART";

            _items = LoadBatchItems(scope);
            if (!_items.Any())
            {
                MessageBox.Show("Aucun élément trouvé dans cette section pour ce projet.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BorderWelcome.Visibility = Visibility.Collapsed;
            GridItemReview.Visibility = Visibility.Visible;
            PanelReviewActions.Visibility = Visibility.Visible;

            _appliedCount = 0;
            _currentIndex = 0;
            UpdateProgressDisplay();

            await LoadAndGenerateCurrentItemAsync();
        }

        private List<BatchWorkItem> LoadBatchItems(string scope)
        {
            var list = new List<BatchWorkItem>();
            using var db = new AppDbContext();

            if (scope.Contains("Indicateurs"))
            {
                var indicators = db.Indicators
                    .Include(i => i.Objective)
                    .Where(i => i.Objective != null && i.Objective.ProjectId == _projectId)
                    .ToList();

                foreach (var ind in indicators)
                {
                    list.Add(new BatchWorkItem
                    {
                        Id = ind.Id,
                        EntityType = "Indicator",
                        Code = !string.IsNullOrWhiteSpace(ind.Code) ? ind.Code : $"IND{ind.Id}",
                        Title = $"Indicateur #{ind.Id}",
                        CurrentText = ind.Description ?? string.Empty,
                        SemanticType = FieldSemanticType.Indicateur,
                        ParentId = ind.ObjectiveId
                    });
                }
            }
            else if (scope.Contains("Activités"))
            {
                var activities = db.Activities
                    .Include(a => a.Result)
                    .ThenInclude(r => r!.Objective)
                    .Where(a => a.Result != null && a.Result.Objective != null && a.Result.Objective.ProjectId == _projectId)
                    .ToList();

                foreach (var act in activities)
                {
                    list.Add(new BatchWorkItem
                    {
                        Id = act.Id,
                        EntityType = "Activity",
                        Code = !string.IsNullOrWhiteSpace(act.Code) ? act.Code : $"A{act.Id}",
                        Title = $"Activité #{act.Id}",
                        CurrentText = act.Description ?? string.Empty,
                        SemanticType = FieldSemanticType.Activite,
                        ParentId = act.ResultId
                    });
                }
            }
            else if (scope.Contains("Résultats"))
            {
                var results = db.Results
                    .Include(r => r.Objective)
                    .Where(r => r.Objective != null && r.Objective.ProjectId == _projectId)
                    .ToList();

                foreach (var res in results)
                {
                    list.Add(new BatchWorkItem
                    {
                        Id = res.Id,
                        EntityType = "Result",
                        Code = !string.IsNullOrWhiteSpace(res.Code) ? res.Code : $"R{res.Id}",
                        Title = $"Résultat #{res.Id}",
                        CurrentText = res.Description ?? string.Empty,
                        SemanticType = FieldSemanticType.Resultat,
                        ParentId = res.ObjectiveId
                    });
                }
            }
            else if (scope.Contains("Objectifs"))
            {
                var objectives = db.Objectives
                    .Where(o => o.ProjectId == _projectId)
                    .ToList();

                foreach (var obj in objectives)
                {
                    list.Add(new BatchWorkItem
                    {
                        Id = obj.Id,
                        EntityType = "Objective",
                        Code = !string.IsNullOrWhiteSpace(obj.Code) ? obj.Code : $"OS{obj.Id}",
                        Title = $"Objectif Spécifique #{obj.Id}",
                        CurrentText = obj.Description ?? string.Empty,
                        SemanticType = FieldSemanticType.ObjectifSpecifique,
                        ParentId = obj.ProjectId
                    });
                }
            }
            else if (scope.Contains("budgétaires"))
            {
                var items = db.BudgetItems
                    .Where(b => b.ProjectId == _projectId)
                    .ToList();

                foreach (var b in items)
                {
                    list.Add(new BatchWorkItem
                    {
                        Id = b.Id,
                        EntityType = "BudgetItem",
                        Code = $"Ligne #{b.Id}",
                        Title = b.Description,
                        CurrentText = !string.IsNullOrWhiteSpace(b.Notes) ? b.Notes : b.Description,
                        SemanticType = FieldSemanticType.JustificationBudgetaire,
                        ParentId = b.ActivityId
                    });
                }
            }
            else if (scope.Contains("Cadrage"))
            {
                var p = db.Projects.FirstOrDefault(pr => pr.Id == _projectId);
                if (p != null)
                {
                    list.Add(new BatchWorkItem
                    {
                        Id = p.Id,
                        EntityType = "Project.Title",
                        Code = "Titre",
                        Title = "Intitulé du projet",
                        CurrentText = p.Title ?? string.Empty,
                        SemanticType = FieldSemanticType.Titre
                    });
                    list.Add(new BatchWorkItem
                    {
                        Id = p.Id,
                        EntityType = "Project.GeneralObjective",
                        Code = "OG",
                        Title = "Objectif Général",
                        CurrentText = p.GeneralObjective ?? string.Empty,
                        SemanticType = FieldSemanticType.ObjectifGeneral
                    });
                    list.Add(new BatchWorkItem
                    {
                        Id = p.Id,
                        EntityType = "Project.Justification",
                        Code = "Contexte",
                        Title = "Justification et contexte",
                        CurrentText = p.Justification ?? string.Empty,
                        SemanticType = FieldSemanticType.ContexteGeneral
                    });
                }
            }

            return list;
        }

        private async Task LoadAndGenerateCurrentItemAsync()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count)
            {
                FinishBatch();
                return;
            }

            var item = _items[_currentIndex];
            TxtCurrentItemCode.Text = $"[{item.Code}]";
            TxtCurrentItemTitle.Text = item.Title;
            TxtCurrentItemType.Text = $"Type : {item.SemanticType}";
            TxtItemOriginal.Text = item.CurrentText;

            LoadingItemOverlay.Visibility = Visibility.Visible;
            TxtItemProposal.Text = string.Empty;
            TxtItemProposalStatus.Text = string.Empty;

            try
            {
                var descriptor = new FieldDescriptor(
                    fieldIdentifier: $"{item.EntityType}.{item.Id}",
                    fieldLabel: item.Title,
                    semanticType: item.SemanticType)
                {
                    EntityId = item.Id,
                    ParentEntityId = item.ParentId
                };

                var response = await _service.ProcessActionAsync(
                    action: _selectedAction,
                    currentText: item.CurrentText,
                    descriptor: descriptor,
                    projectId: _projectId);

                item.ProposedText = response.Proposal;
                TxtItemProposal.Text = item.ProposedText;
                TxtItemProposalStatus.Text = $"✅ Proposition IA ({response.ProviderUsed})";
            }
            catch (Exception ex)
            {
                TxtItemProposal.Text = item.CurrentText;
                TxtItemProposalStatus.Text = $"⚠️ Erreur IA : {ex.Message}";
            }
            finally
            {
                LoadingItemOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateProgressDisplay()
        {
            int total = _items.Count;
            int currentNumber = Math.Min(_currentIndex + 1, total);
            TxtProgressCounter.Text = $"Élément {currentNumber} sur {total}";

            double percent = total > 0 ? (double)_currentIndex / total * 100 : 0;
            PbBatchProgress.Value = percent;
            TxtProgressPercent.Text = $"{(int)percent} %";
        }

        private async void BtnApplyItem_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex >= 0 && _currentIndex < _items.Count)
            {
                var item = _items[_currentIndex];
                string finalValue = TxtItemProposal.Text;
                await ApplyItemUpdateAsync(item, finalValue);
                item.WasApplied = true;
                _appliedCount++;
            }

            _currentIndex++;
            UpdateProgressDisplay();
            await LoadAndGenerateCurrentItemAsync();
        }

        private async void BtnSkipItem_Click(object sender, RoutedEventArgs e)
        {
            _currentIndex++;
            UpdateProgressDisplay();
            await LoadAndGenerateCurrentItemAsync();
        }

        private async void BtnRegenerateItem_Click(object sender, RoutedEventArgs e)
        {
            await LoadAndGenerateCurrentItemAsync();
        }

        private async void BtnApplyAll_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                $"Voulez-vous générer et appliquer automatiquement les propositions sur les {_items.Count - _currentIndex} élément(s) restant(s) ?",
                "Confirmation du mode lot",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            LoadingItemOverlay.Visibility = Visibility.Visible;

            while (_currentIndex < _items.Count)
            {
                var item = _items[_currentIndex];
                var descriptor = new FieldDescriptor($"{item.EntityType}.{item.Id}", item.Title, item.SemanticType)
                {
                    EntityId = item.Id,
                    ParentEntityId = item.ParentId
                };

                try
                {
                    var response = await _service.ProcessActionAsync(
                        action: _selectedAction,
                        currentText: item.CurrentText,
                        descriptor: descriptor,
                        projectId: _projectId);

                    string proposal = !string.IsNullOrWhiteSpace(response.Proposal) ? response.Proposal : item.CurrentText;
                    await ApplyItemUpdateAsync(item, proposal);
                    _appliedCount++;
                }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiBatchAssistDialog), $"Erreur ignorée : {swallowedEx.Message}"); }

                _currentIndex++;
                UpdateProgressDisplay();
            }

            LoadingItemOverlay.Visibility = Visibility.Collapsed;
            FinishBatch();
        }

        private async Task ApplyItemUpdateAsync(BatchWorkItem item, string newText)
        {
            using var db = new AppDbContext();

            if (item.EntityType == "Indicator")
            {
                var ind = db.Indicators.FirstOrDefault(i => i.Id == item.Id);
                if (ind != null)
                {
                    ind.Description = newText;
                    await db.SaveChangesAsync();
                }
            }
            else if (item.EntityType == "Activity")
            {
                var act = db.Activities.FirstOrDefault(a => a.Id == item.Id);
                if (act != null)
                {
                    act.Description = newText;
                    await db.SaveChangesAsync();
                }
            }
            else if (item.EntityType == "Result")
            {
                var res = db.Results.FirstOrDefault(r => r.Id == item.Id);
                if (res != null)
                {
                    res.Description = newText;
                    await db.SaveChangesAsync();
                }
            }
            else if (item.EntityType == "Objective")
            {
                var obj = db.Objectives.FirstOrDefault(o => o.Id == item.Id);
                if (obj != null)
                {
                    obj.Description = newText;
                    await db.SaveChangesAsync();
                }
            }
            else if (item.EntityType == "BudgetItem")
            {
                var b = db.BudgetItems.FirstOrDefault(bi => bi.Id == item.Id);
                if (b != null)
                {
                    b.Notes = newText;
                    await db.SaveChangesAsync();
                }
            }
            else if (item.EntityType.StartsWith("Project."))
            {
                var p = db.Projects.FirstOrDefault(pr => pr.Id == item.Id);
                if (p != null)
                {
                    if (item.EntityType.EndsWith("Title")) p.Title = newText;
                    else if (item.EntityType.EndsWith("GeneralObjective")) p.GeneralObjective = newText;
                    else if (item.EntityType.EndsWith("Justification")) p.Justification = newText;
                    await db.SaveChangesAsync();
                }
            }

            // Historique de version
            await _service.SaveVersionHistoryAsync(
                fieldIdentifier: $"{item.EntityType}.{item.Id}",
                originalText: item.CurrentText,
                proposedText: newText,
                finalText: newText,
                actionType: _selectedAction,
                semanticType: item.SemanticType,
                providerUsed: "BatchAssistant",
                modelUsed: "Batch",
                projectId: _projectId);
        }

        private void FinishBatch()
        {
            PbBatchProgress.Value = 100;
            TxtProgressPercent.Text = "100 %";
            TxtProgressCounter.Text = $"Terminé : {_appliedCount} élément(s) mis à jour";

            MessageBox.Show(
                $"Traitement en lot terminé avec succès !\n\n{_appliedCount} élément(s) ont été mis à jour et historisés dans le projet.",
                "Lot terminé",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

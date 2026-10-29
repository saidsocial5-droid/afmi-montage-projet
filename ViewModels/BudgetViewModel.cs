using System;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Services;

using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class BudgetViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<BudgetItemRowViewModel> _budgetItems = new();

        [ObservableProperty]
        private ObservableCollection<BudgetCategory> _categories = new();

        // Totaux globaux
        [ObservableProperty]
        private decimal _grandTotal;

        [ObservableProperty]
        private decimal _totalAssociation;

        [ObservableProperty]
        private decimal _totalCofinancing;

        [ObservableProperty]
        private decimal _totalRequested;

        // Champs d'ajout
        [ObservableProperty]
        private string _newDescription = string.Empty;

        [ObservableProperty]
        private BudgetCategory? _newCategory;

        [ObservableProperty]
        private ObservableCollection<Activity> _activities = new();

        [ObservableProperty]
        private Activity? _newActivity;

        [ObservableProperty]
        private string _newUnit = "Mois";

        [ObservableProperty]
        private decimal _newQuantity = 1;

        [ObservableProperty]
        private decimal _newUnitCost = 0;

        [ObservableProperty]
        private decimal _newFrequency = 1;

        [ObservableProperty]
        private decimal _newFundingAssociation = 0;

        [ObservableProperty]
        private decimal _newFundingCofinancing = 0;

        [ObservableProperty]
        private decimal _newFundingRequested = 0;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // Propriétés pour le plan de cofinancement multi-sources
        [ObservableProperty]
        private ObservableCollection<FundingSource> _fundingSources = new();

        [ObservableProperty]
        private string _newSourceName = string.Empty;

        [ObservableProperty]
        private string _newSourceType = "Bailleur principal"; // Bailleur principal, Apport propre, Cofinancement espèces, Cofinancement nature

        [ObservableProperty]
        private string _newSourceNature = "Espèces"; // Espèces, Nature

        [ObservableProperty]
        private string _newSourceStatus = "Disponible"; // Disponible, Projeté

        [ObservableProperty]
        private decimal _newSourceAmount = 0;

        [ObservableProperty]
        private string _newSourceCurrency = "EUR";

        [ObservableProperty]
        private decimal _cofinancingRatio;

        [ObservableProperty]
        private string _alertMessage = string.Empty;

        public ObservableCollection<string> FundingTypes { get; } = new()
        {
            "Bailleur principal", "Apport propre", "Cofinancement espèces", "Cofinancement nature"
        };

        public ObservableCollection<string> FundingNatures { get; } = new()
        {
            "Espèces", "Nature"
        };

        public ObservableCollection<string> FundingStatuses { get; } = new()
        {
            "Disponible", "Projeté"
        };

        public BudgetViewModel(Project project)
        {
            _project = project;
            LoadBudgetData();
        }

        private void LoadBudgetData()
        {
            using var db = new AppDbContext();
            db.SeedDefaultData(); // S'assurer que les catégories par défaut existent
            
            Categories = new ObservableCollection<BudgetCategory>(db.BudgetCategories.OrderBy(c => c.Code).ToList());
            
            // Sélectionner la première catégorie par défaut pour l'ajout
            if (Categories.Any() && NewCategory == null)
            {
                NewCategory = Categories.First();
            }

            var items = db.BudgetItems
                .Where(b => b.ProjectId == _project.Id)
                .Include(b => b.Category)
                .Include(b => b.Activity)
                .ToList();

            var rows = items.Select(i => new BudgetItemRowViewModel(i, this)).ToList();
            BudgetItems = new ObservableCollection<BudgetItemRowViewModel>(rows);

            Activities = new ObservableCollection<Activity>(
                db.Activities.Where(a => a.Result != null && a.Result.Objective.ProjectId == _project.Id).ToList()
            );

            // Charger les sources de financement
            var sources = db.FundingSources
                .Where(s => s.ProjectId == _project.Id)
                .ToList();
            FundingSources = new ObservableCollection<FundingSource>(sources);

            CalculateTotals();
        }

        public void CalculateTotals()
        {
            GrandTotal = BudgetItems.Sum(item => item.TotalCost);
            TotalAssociation = BudgetItems.Sum(item => item.FundingAssociation);
            TotalCofinancing = BudgetItems.Sum(item => item.FundingCofinancing);
            TotalRequested = BudgetItems.Sum(item => item.FundingRequested);

            // Calculer le ratio de cofinancement (cofinancement / montant demandé au bailleur)
            if (TotalRequested > 0)
            {
                CofinancingRatio = Math.Round((TotalCofinancing + TotalAssociation) / TotalRequested, 2);
            }
            else
            {
                CofinancingRatio = 0;
            }

            // Vérifier le plafond du bailleur principal s'il est configuré dans les sources de financement
            var principalSource = FundingSources.FirstOrDefault(s => s.Type == "Bailleur principal");
            if (principalSource != null && TotalRequested > principalSource.TotalAmountAllocated)
            {
                AlertMessage = $"⚠️ Attention : Le montant demandé ({TotalRequested:N2} {principalSource.Currency}) dépasse le plafond alloué par le bailleur principal ({principalSource.TotalAmountAllocated:N2} {principalSource.Currency}) !";
            }
            else
            {
                AlertMessage = string.Empty;
            }
        }

        [RelayCommand]
        public async Task SuggererLigneBudgetaire()
        {
            StatusMessage = "Recherche de suggestions...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Suggère 3 intitulés de lignes budgétaires (dépenses) pertinentes pour ce projet. Rédige-les sous forme de tirets. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert financier en montage de projets. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction ni de conclusion, donne UNIQUEMENT la liste demandée sous forme de tirets (-).";
                
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

                var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Suggérer : Ligne budgétaire", suggestions);
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
                {
                    NewDescription = dialog.SelectedText.TrimStart('-', ' ', '\n', '\r');
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
        public async Task GenererLigneBudgetaire()
        {
            StatusMessage = "Génération par l'IA...";
            try
            {
                string projectContext = ProjectContextBuilder.BuildFullContext(_project.Id);
                string prompt = $"Génère un intitulé de ligne budgétaire (dépense) très concret pour ce projet. Voici les informations du projet :\n\n{projectContext}";
                string sys = "Tu es un expert financier. Réponds IMPÉRATIVEMENT en FRANÇAIS. Ne fournis JAMAIS d'introduction, donne UNIQUEMENT le texte (1 seule phrase très courte).";
                
                string aiResult = await AiAssistantService.CallAiAsync(prompt, sys);
                NewDescription = aiResult.TrimStart('-', '*', ' ', '\n', '\r').Trim();
                StatusMessage = "Description générée avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'appel IA : {ex.Message}";
            }
        }

        #region Assistant IA Rédactionnel (Enrichir, Reformuler, Détailler)

        [RelayCommand]
        public async Task EnrichirLigneBudgetaire()
        {
            await OpenTextAssistDialog(
                "Budget.NewDescription", 
                "Description de la dépense", 
                FieldSemanticType.LigneBudgetaire, 
                NewDescription, 
                TextAssistActionType.Enrichir, 
                txt => NewDescription = txt);
        }

        [RelayCommand]
        public async Task ReformulerLigneBudgetaire()
        {
            await OpenTextAssistDialog(
                "Budget.NewDescription", 
                "Description de la dépense", 
                FieldSemanticType.LigneBudgetaire, 
                NewDescription, 
                TextAssistActionType.Reformuler, 
                txt => NewDescription = txt);
        }

        [RelayCommand]
        public async Task DetaillerLigneBudgetaire()
        {
            await OpenTextAssistDialog(
                "Budget.NewDescription", 
                "Description de la dépense", 
                FieldSemanticType.LigneBudgetaire, 
                NewDescription, 
                TextAssistActionType.Detailler, 
                txt => NewDescription = txt);
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
        public void AddBudgetItem()
        {
            if (string.IsNullOrWhiteSpace(NewDescription) || NewCategory == null)
            {
                StatusMessage = "Veuillez remplir la description et la catégorie.";
                return;
            }

            // Vérification de la cohérence de la répartition budgétaire
            decimal calculatedTotal = NewQuantity * NewUnitCost * NewFrequency;
            decimal sumContributions = NewFundingAssociation + NewFundingCofinancing + NewFundingRequested;
            
            if (calculatedTotal != sumContributions)
            {
                // Si l'utilisateur n'a pas défini la répartition, on l'affecte par défaut au budget demandé
                if (NewFundingAssociation == 0 && NewFundingCofinancing == 0 && NewFundingRequested == 0)
                {
                    NewFundingRequested = calculatedTotal;
                }
                else
                {
                    StatusMessage = "Avertissement : La somme des contributions ne correspond pas au coût total.";
                }
            }

            try
            {
                using var db = new AppDbContext();
                var item = new BudgetItem
                {
                    ProjectId = _project.Id,
                    CategoryId = NewCategory.Id,
                    ActivityId = NewActivity?.Id,
                    Description = NewDescription.Trim(),
                    Unit = NewUnit.Trim(),
                    Quantity = NewQuantity,
                    UnitCost = NewUnitCost,
                    Frequency = NewFrequency,
                    FundingAssociation = NewFundingAssociation,
                    FundingCofinancing = NewFundingCofinancing,
                    FundingRequested = NewFundingRequested
                };

                db.BudgetItems.Add(item);
                db.SaveChanges();

                // Recharger
                LoadBudgetData();

                // Réinitialiser les champs de saisie
                NewDescription = string.Empty;
                NewActivity = null;
                NewQuantity = 1;
                NewUnitCost = 0;
                NewFrequency = 1;
                NewFundingAssociation = 0;
                NewFundingCofinancing = 0;
                NewFundingRequested = 0;

                StatusMessage = "Ligne budgétaire ajoutée !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'ajout : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteBudgetItem(BudgetItemRowViewModel? row)
        {
            if (row == null) return;

            try
            {
                using var db = new AppDbContext();
                var item = db.BudgetItems.FirstOrDefault(b => b.Id == row.ItemId);
                if (item != null)
                {
                    db.BudgetItems.Remove(item);
                    db.SaveChanges();
                    
                    BudgetItems.Remove(row);
                    CalculateTotals();
                    StatusMessage = "Ligne budgétaire supprimée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la suppression : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SaveBudget()
        {
            try
            {
                using var db = new AppDbContext();
                foreach (var row in BudgetItems)
                {
                    var item = db.BudgetItems.FirstOrDefault(b => b.Id == row.ItemId);
                    if (item != null)
                    {
                        item.Description = row.Description.Trim();
                        item.Unit = row.Unit.Trim();
                        item.Quantity = row.Quantity;
                        item.UnitCost = row.UnitCost;
                        item.Frequency = row.Frequency;
                        item.FundingAssociation = row.FundingAssociation;
                        item.FundingCofinancing = row.FundingCofinancing;
                        item.FundingRequested = row.FundingRequested;
                    }
                }
                db.SaveChanges();
                
                // Mettre à jour également le budget global sur la fiche projet
                var project = db.Projects.FirstOrDefault(p => p.Id == _project.Id);
                if (project != null)
                {
                    project.TotalBudget = GrandTotal;
                    project.LocalContribution = TotalAssociation;
                    project.PartnerContribution = TotalCofinancing;
                    db.SaveChanges();
                    
                    _project.TotalBudget = GrandTotal;
                    _project.LocalContribution = TotalAssociation;
                    _project.PartnerContribution = TotalCofinancing;
                }

                CalculateTotals();
                StatusMessage = "Budget sauvegardé et fiche projet mise à jour !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la sauvegarde : {ex.Message}";
            }
        }

        [RelayCommand]
        public void AddFundingSource()
        {
            if (string.IsNullOrWhiteSpace(NewSourceName))
            {
                StatusMessage = "Veuillez saisir le nom de la source de cofinancement.";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var newSource = new FundingSource
                {
                    ProjectId = _project.Id,
                    Name = NewSourceName.Trim(),
                    Type = NewSourceType,
                    NatureOfFunding = NewSourceNature,
                    Status = NewSourceStatus,
                    TotalAmountAllocated = NewSourceAmount,
                    Currency = NewSourceCurrency
                };

                db.FundingSources.Add(newSource);
                db.SaveChanges();

                FundingSources.Add(newSource);
                
                // Réinitialiser le formulaire
                NewSourceName = string.Empty;
                NewSourceAmount = 0;

                CalculateTotals();
                StatusMessage = "Source de cofinancement ajoutée avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'ajout de la source : {ex.Message}";
            }
        }

        [RelayCommand]
        public void DeleteFundingSource(FundingSource? source)
        {
            if (source == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbSource = db.FundingSources.FirstOrDefault(s => s.Id == source.Id);
                if (dbSource != null)
                {
                    db.FundingSources.Remove(dbSource);
                    db.SaveChanges();
                    FundingSources.Remove(source);
                    CalculateTotals();
                    StatusMessage = "Source de cofinancement supprimée.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur de suppression : {ex.Message}";
            }
        }
    }

    public partial class BudgetItemRowViewModel : ObservableObject
    {
        private readonly BudgetViewModel _parent;

        public int ItemId { get; }
        public string CategoryCode { get; }
        public string CategoryName { get; }
        public string ActivityName { get; }

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _unit = string.Empty;

        [ObservableProperty]
        private decimal _quantity;

        [ObservableProperty]
        private decimal _unitCost;

        [ObservableProperty]
        private decimal _frequency;

        [ObservableProperty]
        private decimal _fundingAssociation;

        [ObservableProperty]
        private decimal _fundingCofinancing;

        [ObservableProperty]
        private decimal _fundingRequested;

        public decimal TotalCost => Quantity * UnitCost * Frequency;

        // Indiquer si la somme des répartitions financières correspond au total
        public bool IsDistributionBalanced => TotalCost == (FundingAssociation + FundingCofinancing + FundingRequested);

        public BudgetItemRowViewModel(BudgetItem item, BudgetViewModel parent)
        {
            _parent = parent;
            ItemId = item.Id;
            CategoryCode = item.Category?.Code ?? string.Empty;
            CategoryName = item.Category?.Name ?? string.Empty;
            ActivityName = item.Activity?.DisplayName ?? "N/A";
            
            _description = item.Description;
            _unit = item.Unit;
            _quantity = item.Quantity;
            _unitCost = item.UnitCost;
            _frequency = item.Frequency;
            _fundingAssociation = item.FundingAssociation;
            _fundingCofinancing = item.FundingCofinancing;
            _fundingRequested = item.FundingRequested;
        }

        partial void OnQuantityChanged(decimal value) => OnRowUpdated();
        partial void OnUnitCostChanged(decimal value) => OnRowUpdated();
        partial void OnFrequencyChanged(decimal value) => OnRowUpdated();
        partial void OnFundingAssociationChanged(decimal value) => OnRowUpdated();
        partial void OnFundingCofinancingChanged(decimal value) => OnRowUpdated();
        partial void OnFundingRequestedChanged(decimal value) => OnRowUpdated();

        private void OnRowUpdated()
        {
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(IsDistributionBalanced));
            _parent.CalculateTotals();
        }
    }
}


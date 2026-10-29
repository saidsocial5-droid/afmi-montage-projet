using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ArbreProblemeViewModel : ObservableObject
    {
        private readonly int _projectId;

        [ObservableProperty]
        private ObservableCollection<ProblemNode> _rootNodes = new();

        [ObservableProperty]
        private ProblemNode? _selectedNode;

        public ArbreProblemeViewModel(int projectId)
        {
            _projectId = projectId;
            LoadNodes();
        }

        private void LoadNodes()
        {
            using var db = new AppDbContext();
            // Charger tous les noeuds de ce projet
            var allNodes = db.ProblemNodes
                .Include(n => n.ProposedByUser)
                .Where(n => n.ProjectId == _projectId)
                .ToList();

            // Reconstruire l'arbre en mémoire pour l'affichage WPF
            var roots = allNodes.Where(n => n.ParentNodeId == null).ToList();
            
            foreach (var root in roots)
            {
                BuildTree(root, allNodes);
            }

            RootNodes = new ObservableCollection<ProblemNode>(roots);
        }

        private void BuildTree(ProblemNode parent, System.Collections.Generic.List<ProblemNode> allNodes)
        {
            var children = allNodes.Where(n => n.ParentNodeId == parent.Id).ToList();
            parent.ChildNodes = new ObservableCollection<ProblemNode>(children);
            foreach (var child in children)
            {
                BuildTree(child, allNodes);
            }
        }

        [RelayCommand]
        private void AddCoreProblem()
        {
            if (RootNodes.Any(n => n.NodeType == "Core"))
            {
                MessageBox.Show("Il y a déjà  un problà¨me central défini pour ce projet.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newNode = new ProblemNode
            {
                ProjectId = _projectId,
                Title = "Nouveau Problà¨me Central",
                Description = "Description du problà¨me principal",
                NodeType = "Core"
            };

            SaveNewNode(newNode, "Problà¨me Central");
        }

        [RelayCommand]
        private void AddCause(ProblemNode parentNode)
        {
            if (parentNode == null) return;

            var newNode = new ProblemNode
            {
                ProjectId = _projectId,
                Title = "Nouvelle Cause",
                Description = "Description de la cause",
                NodeType = "Cause",
                ParentNodeId = parentNode.Id
            };

            SaveNewNode(newNode, "Cause");
        }

        [RelayCommand]
        private void AddConsequence(ProblemNode parentNode)
        {
            if (parentNode == null) return;

            var newNode = new ProblemNode
            {
                ProjectId = _projectId,
                Title = "Nouvelle Conséquence",
                Description = "Description de la conséquence",
                NodeType = "Consequence",
                ParentNodeId = parentNode.Id
            };

            SaveNewNode(newNode, "Conséquence");
        }

        private void SaveNewNode(ProblemNode newNode, string typeName)
        {
            var currentUser = SessionManager.CurrentUser;
            bool isAdmin = false;
            
            using (var checkDb = new AppDbContext())
            {
                isAdmin = checkDb.ProjectRoles.Any(r => r.ProjectId == _projectId && r.UserId == currentUser.Id && r.RoleName == "Admin") 
                          || (currentUser?.IsSystemAdmin ?? false);
            }

            newNode.ProposedByUserId = currentUser?.Id;
            newNode.Status = isAdmin ? "Approved" : "Pending";

            using var db = new AppDbContext();
            db.ProblemNodes.Add(newNode);
            db.SaveChanges();

            if (!isAdmin)
            {
                AFMI_Montage_projet_2.Services.NotificationService.NotifyAdminsOfProject(_projectId, $"Nouveau nÅ“ud ({typeName}) proposé",
                    $"Le nÅ“ud '{newNode.Title}' a été proposé par @{currentUser?.Username} dans l'Arbre à  Problà¨mes.");
            }

            LoadNodes();
        }

        [RelayCommand]
        private void SaveNode(ProblemNode node)
        {
            if (node == null) return;

            using var db = new AppDbContext();
            db.ProblemNodes.Update(node);
            db.SaveChanges();
            MessageBox.Show("NÅ“ud enregistré avec succà¨s.", "Succà¨s", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadNodes();
        }

        [RelayCommand]
        private void DeleteNode(ProblemNode node)
        {
            if (node == null) return;

            if (MessageBox.Show("Voulez-vous vraiment supprimer ce nÅ“ud et tous ses enfants ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                using var db = new AppDbContext();
                db.ProblemNodes.Remove(node);
                db.SaveChanges();
                LoadNodes();
                SelectedNode = null;
            }
        }

        [RelayCommand]
        private void ApproveNode(ProblemNode node)
        {
            if (node == null) return;
            using var db = new AppDbContext();
            var dbNode = db.ProblemNodes.Find(node.Id);
            if (dbNode != null)
            {
                dbNode.Status = "Approved";
                db.SaveChanges();
                LoadNodes();
            }
        }

        [RelayCommand]
        private void ApproveAndAnonymizeNode(ProblemNode node)
        {
            if (node == null) return;
            using var db = new AppDbContext();
            var dbNode = db.ProblemNodes.Find(node.Id);
            if (dbNode != null)
            {
                dbNode.Status = "Approved";
                dbNode.IsAnonymized = true;
                db.SaveChanges();
                LoadNodes();
            }
        }

        private string BuildProjectContext()
        {
            var coreNode = RootNodes.FirstOrDefault(n => n.NodeType == "Core");
            string baseContext = coreNode != null ? $"Le problà¨me central de ce projet est : {coreNode.Title}." : "Aucun problà¨me central défini.";
            return AFMI_Montage_projet_2.Services.ProjectContextBuilder.BuildFullContext(_projectId, baseContext);
        }

        // --- COMMANDES ASSISTANT IA ---
        
        [RelayCommand]
        private async Task AiEnrichTitle(ProblemNode node)
        {
            if (node == null) return;
            var descriptor = new FieldDescriptor("ArbreProbleme.Title", "Titre du problème", FieldSemanticType.Probleme);
            var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: node.Title ?? string.Empty,
                isSelection: false,
                initialAction: TextAssistActionType.Enrichir,
                projectId: _projectId);
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
            if (owner != null) dialog.Owner = owner;
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                node.Title = dialog.ResultText;
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task AiReformulateTitle(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Title)) return;
            node.Title = await ProblemTreeAiHelper.ReformulateTitleAsync(node.Title, BuildProjectContext());
        }

        [RelayCommand]
        private async Task AiCorrectTitle(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Title)) return;
            node.Title = await ProblemTreeAiHelper.CorrectTitleAsync(node.Title, BuildProjectContext());
        }

        [RelayCommand]
        private async Task AiGenerate(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Title)) return;
            node.Description = await ProblemTreeAiHelper.GenerateDescriptionAsync(node.Title, BuildProjectContext());
        }

        [RelayCommand]
        private async Task AiEnrich(ProblemNode node)
        {
            if (node == null) return;
            var descriptor = new FieldDescriptor("ArbreProbleme.Description", "Description du problème", FieldSemanticType.Probleme);
            var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: node.Description ?? string.Empty,
                isSelection: false,
                initialAction: TextAssistActionType.Enrichir,
                projectId: _projectId);
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
            if (owner != null) dialog.Owner = owner;
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                node.Description = dialog.ResultText;
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task AiDetail(ProblemNode node)
        {
            if (node == null) return;
            var descriptor = new FieldDescriptor("ArbreProbleme.Description", "Description du problème", FieldSemanticType.Probleme);
            var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: node.Description ?? string.Empty,
                isSelection: false,
                initialAction: TextAssistActionType.Detailler,
                projectId: _projectId);
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
            if (owner != null) dialog.Owner = owner;
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                node.Description = dialog.ResultText;
            }
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task AiReformulate(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Description)) return;
            node.Description = await ProblemTreeAiHelper.ReformulateDescriptionAsync(node.Description, BuildProjectContext());
        }

        [RelayCommand]
        private async Task AiCorrect(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Description)) return;
            node.Description = await ProblemTreeAiHelper.CorrectDescriptionAsync(node.Description, BuildProjectContext());
        }

        [RelayCommand]
        private async Task AiAnalyze(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Description)) return;
            var suggestions = await ProblemTreeAiHelper.AnalyzeAsListAsync(node.Description, BuildProjectContext());
            var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Assistant IA - Analyse", suggestions);
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
            {
                node.Description = await ProblemTreeAiHelper.MergeIntoDescriptionAsync(node.Description, dialog.SelectedText, BuildProjectContext());
            }
        }

                [RelayCommand]
        private async Task AiSuggestTitle(ProblemNode node)
        {
            if (node == null) return;
            string parentTitle = node.ParentNode?.Title ?? "";
            var suggestions = await ProblemTreeAiHelper.SuggestTitlesForNodeAsync(node.NodeType, BuildProjectContext(), parentTitle);
            var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Assistant IA - Suggestions de Titre", suggestions);
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
            {
                node.Title = dialog.SelectedText.Trim();
            }
        }

        [RelayCommand]
        private async Task AiSuggest(ProblemNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Title)) return;
            var suggestions = await ProblemTreeAiHelper.SuggestAsListAsync(node.Title, BuildProjectContext());
            var dialog = new AFMI_Montage_projet_2.Views.AiSuggestionWindow("Assistant IA - Suggestions", suggestions);
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedText))
            {
                node.Description = await ProblemTreeAiHelper.MergeIntoDescriptionAsync(node.Description, dialog.SelectedText, BuildProjectContext());
            }
        }
    }
}



using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AFMI_Montage_projet_2.Models
{
    public class ProblemNode : ObservableObject
    {
        public int Id { get; set; }
        
        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        
        // Type: "Core", "Cause", "Consequence"
        public string NodeType { get; set; } = string.Empty;

        // Auto-référence pour l'arbre
        public int? ParentNodeId { get; set; }
        public ProblemNode? ParentNode { get; set; }
        public ICollection<ProblemNode> ChildNodes { get; set; } = new List<ProblemNode>();

        // V3 : Fonctionnalités Collaboratives
        public int? ProposedByUserId { get; set; }
        public User? ProposedByUser { get; set; }
        
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string Status { get; set; } = "Approved"; // Pending, Approved, Rejected
        
        public bool IsAnonymized { get; set; } = false;
        public string AiSuggestionBadge { get; set; } = string.Empty;
    }
}





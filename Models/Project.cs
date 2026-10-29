using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Project : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(150)]
        public string InterventionZone { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal TotalBudget { get; set; }
        public string Currency { get; set; } = "EUR";

        public decimal LocalContribution { get; set; }
        public decimal PartnerContribution { get; set; }

        [MaxLength(150)]
        public string LeadOrganization { get; set; } = string.Empty;

        public string Background { get; set; } = string.Empty;
        public string ContextJustification { get; set; } = string.Empty; // Gardé pour compatibilité ascendante
        public string Justification { get; set; } = string.Empty;
        public string ProjectApproach { get; set; } = string.Empty;
        public string ProjectImpacts { get; set; } = string.Empty;
        public string StrategicAlignment { get; set; } = string.Empty;
        public string Sustainability { get; set; } = string.Empty;
        public string OrganizationalStrengths { get; set; } = string.Empty;
        public string GeneralObjective { get; set; } = string.Empty;

        // Analyse SWOT
        public string SwotStrengths { get; set; } = string.Empty;
        public string SwotWeaknesses { get; set; } = string.Empty;
        public string SwotOpportunities { get; set; } = string.Empty;
        public string SwotThreats { get; set; } = string.Empty;

        // Synthèse globale des documents d'appui
        public string GlobalRequirementsSummary { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Contrôle de l'IA Cloud par projet (Opt-in spécifique au projet)
        public bool AllowCloudAi { get; set; } = false;

        // Relations
        public List<Objective> Objectives { get; set; } = new();
        public List<BudgetItem> BudgetItems { get; set; } = new();
        public Strategic.StrategicFoundation? StrategicFoundation { get; set; }
        public List<Strategic.StrategicLink> StrategicLinks { get; set; } = new();
    }
}





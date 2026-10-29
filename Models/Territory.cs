using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace AFMI_Montage_projet_2.Models
{
    /// <summary>
    /// Profil du territoire d'intervention (ADR 25) : localisation, milieu naturel, population,
    /// économie, problèmes et vulnérabilités. Source unique de la zone d'intervention :
    /// <see cref="Project.InterventionZone"/> et StrategicFoundation.TargetZone en sont dérivés.
    /// </summary>
    public class TerritoryProfile : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        // Localisation
        [MaxLength(100)] public string Country { get; set; } = "Maroc";
        [MaxLength(150)] public string Region { get; set; } = string.Empty;
        [MaxLength(150)] public string Province { get; set; } = string.Empty;
        public string Communes { get; set; } = string.Empty;
        public string Localities { get; set; } = string.Empty;

        /// <summary>Rural, Urbain, Périurbain, Montagne, Oasis, Littoral (plusieurs possibles, séparés par des virgules).</summary>
        [MaxLength(150)] public string AreaType { get; set; } = string.Empty;
        [MaxLength(100)] public string Surface { get; set; } = string.Empty;
        [MaxLength(100)] public string Altitude { get; set; } = string.Empty;

        // Milieu naturel
        public string Climate { get; set; } = string.Empty;
        public string Ecosystems { get; set; } = string.Empty;
        public string NaturalResources { get; set; } = string.Empty;
        public string WaterResources { get; set; } = string.Empty;

        // Population et économie
        [MaxLength(150)] public string Population { get; set; } = string.Empty;
        [MaxLength(150)] public string Households { get; set; } = string.Empty;
        [MaxLength(150)] public string PovertyRate { get; set; } = string.Empty;
        public string SocialProfile { get; set; } = string.Empty;
        public string MainLivelihoods { get; set; } = string.Empty;
        public string InfrastructureAccess { get; set; } = string.Empty;

        // Problèmes, vulnérabilités, atouts
        public string EnvironmentalIssues { get; set; } = string.Empty;
        public string SocialIssues { get; set; } = string.Empty;
        public string EconomicIssues { get; set; } = string.Empty;
        public string ClimateVulnerabilities { get; set; } = string.Empty;
        public string InstitutionalActors { get; set; } = string.Empty;
        public string Opportunities { get; set; } = string.Empty;

        /// <summary>Sources des informations (RGPH, monographie communale, PCD, études…).</summary>
        public string Sources { get; set; } = string.Empty;

        /// <summary>Synthèse rédigée (IA ou manuelle) du diagnostic territorial.</summary>
        public string Synthesis { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Libellé de la zone d'intervention dérivé de la localisation (communes, province, région).</summary>
        [NotMapped]
        public string ZoneLabel
        {
            get
            {
                var parts = new[] { Communes, Province, Region }
                    .Select(p => p?.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                if (parts.Count == 0) return string.Empty;
                string label = string.Join(" — ", parts);
                return string.IsNullOrWhiteSpace(Country) || label.Contains(Country, StringComparison.OrdinalIgnoreCase) ? label : $"{label} ({Country})";
            }
        }

        [NotMapped]
        public bool HasLocation => !string.IsNullOrWhiteSpace(Region) || !string.IsNullOrWhiteSpace(Province) || !string.IsNullOrWhiteSpace(Communes);

        /// <summary>Nombre de rubriques de diagnostic renseignées (sur 12), pour mesurer l'avancement.</summary>
        [NotMapped]
        public int FilledDiagnosticSections => new[]
        {
            Climate, Ecosystems, NaturalResources, WaterResources, Population, SocialProfile, MainLivelihoods,
            InfrastructureAccess, EnvironmentalIssues, SocialIssues, EconomicIssues, ClimateVulnerabilities
        }.Count(s => !string.IsNullOrWhiteSpace(s));
    }

    public static class BeneficiaryTypes
    {
        public const string Direct = "Direct";
        public const string Indirect = "Indirect";
    }

    /// <summary>
    /// Groupe de bénéficiaires (direct ou indirect) du projet (ADR 25). Source unique des bénéficiaires :
    /// StrategicFoundation.TargetBeneficiaries en est la synthèse ; les groupes directs alimentent les parties prenantes.
    /// </summary>
    public class BeneficiaryGroup : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;

        /// <summary>Direct ou Indirect.</summary>
        [MaxLength(20)] public string BeneficiaryType { get; set; } = BeneficiaryTypes.Direct;

        /// <summary>Femmes, Jeunes, Agriculteurs/éleveurs, Coopératives, Associations (AGS…), Ménages, Élèves, Institutions…</summary>
        [MaxLength(100)] public string Category { get; set; } = string.Empty;

        public int? EstimatedCount { get; set; }
        [MaxLength(50)] public string CountUnit { get; set; } = "personnes";

        /// <summary>Part des femmes (0-100), si connue.</summary>
        public int? WomenPercent { get; set; }

        /// <summary>Part des jeunes (0-100), si connue.</summary>
        public int? YouthPercent { get; set; }

        public string Location { get; set; } = string.Empty;
        public string Characteristics { get; set; } = string.Empty;
        public string Needs { get; set; } = string.Empty;
        public string ExpectedBenefits { get; set; } = string.Empty;
        public string SelectionCriteria { get; set; } = string.Empty;

        /// <summary>Partie prenante créée à partir de ce groupe (synchronisation sans doublon).</summary>
        public int? LinkedStakeholderId { get; set; }

        [NotMapped]
        public bool IsDirect => string.Equals(BeneficiaryType, BeneficiaryTypes.Direct, StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public string CountLabel => EstimatedCount.HasValue ? $"{EstimatedCount.Value:N0} {CountUnit}" : "effectif à préciser";
    }
}

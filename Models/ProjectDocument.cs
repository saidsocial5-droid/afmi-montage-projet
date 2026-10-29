using System;

namespace AFMI_Montage_projet_2.Models
{
    public class ProjectDocument : BaseEntity
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        // Ex: "AppelAProjet", "Canevas", "Budget"
        public string DocumentType { get; set; } = string.Empty;

        public string AiSummary { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // Analyse structurée (ADR 25)

        /// <summary>Texte extrait du fichier ou de la page web (conservé pour éviter une nouvelle lecture).</summary>
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>Empreinte du texte extrait : l'analyse n'est refaite que si le contenu a changé.</summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Résultat structuré de l'analyse (JSON <c>DocumentInsights</c>).</summary>
        public string InsightsJson { get; set; } = string.Empty;

        /// <summary>Nature détectée : AppelAProjets, Etude, Rapport, Canevas, Statistiques, PageWeb…</summary>
        public string DetectedKind { get; set; } = string.Empty;

        public DateTime? AnalyzedAt { get; set; }

        /// <summary>Date à laquelle les informations ont été intégrées au projet.</summary>
        public DateTime? IntegratedAt { get; set; }
    }
}

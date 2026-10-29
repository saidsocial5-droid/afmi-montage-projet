using System;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    /// <summary>
    /// Porte les métadonnées sémantiques et contextuelles d'une zone de texte dans l'application.
    /// </summary>
    public class FieldDescriptor
    {
        /// <summary>
        /// Identifiant stable du champ (ex: "Cadrage.Title", "Objective.Description_4", "Activity.Justification_12")
        /// </summary>
        public string FieldIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Libellé lisible pour l'utilisateur (ex: "Titre du projet", "Objectif Spécifique 1", "Indicateur SMART")
        /// </summary>
        public string FieldLabel { get; set; } = "Champ de formulaire";

        /// <summary>
        /// Type sémantique dans la chaîne de résultats GAR / Cadre Logique
        /// </summary>
        public FieldSemanticType SemanticType { get; set; } = FieldSemanticType.Generique;

        /// <summary>
        /// Identifiant de l'entité en base (ex: Id de l'Objective, de l'Activity, etc.)
        /// </summary>
        public int? EntityId { get; set; }

        /// <summary>
        /// Identifiant de l'entité parente si applicable (ex: ResultId pour une Activity)
        /// </summary>
        public int? ParentEntityId { get; set; }

        /// <summary>
        /// Type de l'entité parente (ex: "Objective", "Result", "Activity")
        /// </summary>
        public string? ParentEntityType { get; set; }

        /// <summary>
        /// Longueur maximale recommandée
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Indique si ce champ est obligatoire
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Indique si ce champ est explicitement exclu de l'assistance IA (mots de passe, tokens, purement numérique)
        /// </summary>
        public bool IsExcluded { get; set; }

        /// <summary>
        /// Raison documentée de l'exclusion
        /// </summary>
        public string? ExclusionReason { get; set; }

        public FieldDescriptor() { }

        public FieldDescriptor(string fieldIdentifier, string fieldLabel, FieldSemanticType semanticType = FieldSemanticType.Generique)
        {
            FieldIdentifier = fieldIdentifier;
            FieldLabel = fieldLabel;
            SemanticType = semanticType;
        }
    }
}

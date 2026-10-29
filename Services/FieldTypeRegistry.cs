using System;
using System.Collections.Generic;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    /// <summary>
    /// Référentiel des règles sémantiques RBM / GAR et des heuristiques de détection des types de champs.
    /// </summary>
    public static class FieldTypeRegistry
    {
        private static readonly Dictionary<FieldSemanticType, string> Guidelines = new()
        {
            [FieldSemanticType.Titre] =
                "RÈGLE RBM : Titre concis, accrocheur, orienté sur l'impact final ou l'extrant majeur, sans acronymes obscurs ni verbiage creux.",

            [FieldSemanticType.ProblemeCentral] =
                "RÈGLE RBM : Formulation négative stricte décrivant un état de fait dommageable actuel, et JAMAIS une absence de solution (ex: « L'eau est insalubre et contaminée » et NON « Il manque une station de filtrage »).",

            [FieldSemanticType.Cause] =
                "RÈGLE RBM : Cause directe ou structurelle du problème, formulée de manière négative, factuelle et vérifiable.",

            [FieldSemanticType.Effet] =
                "RÈGLE RBM : Conséquence dommageable directe ou indirecte sur les populations cibles, l'écosystème ou l'économie locale.",

            [FieldSemanticType.ObjectifGeneral] =
                "RÈGLE RBM : Impact de développement sociétal à long terme auquel le projet contribue, aligné sur les politiques nationales et les ODD.",

            [FieldSemanticType.ObjectifSpecifique] =
                "RÈGLE RBM : Changement institutionnel, comportemental ou matériel direct généré par le projet, formulé positivement avec un verbe d'action à l'infinitif ou comme un état atteint.",

            [FieldSemanticType.Resultat] =
                "RÈGLE RBM : Formulé STRICTEMENT au participe passé ou comme un état atteint (ex: « 500 jeunes formés », « 12 puits réhabilités », « L'accès à l'eau potable est accru de 40% »).",

            [FieldSemanticType.Activite] =
                "RÈGLE RBM : Verbe d'action à l'infinitif au début, précis, opérationnel et délimité dans le temps (ex: « Organiser 5 sessions de formation pratique », « Réhabiliter 3 forages »).",

            [FieldSemanticType.Indicateur] =
                "RÈGLE RBM : Structure SMART intégrale : Quantité (valeur cible), Qualité (norme), Temps (échéance), Cible (bénéficiaires), Lieu (zone d'intervention). Ex: « 450 femmes maraîchères (Cible) ont doublé leur rendement (Qualité/Quantité) d'ici M18 (Temps) à Bouira (Lieu) ».",

            [FieldSemanticType.SourceVerification] =
                "RÈGLE RBM : Document ou preuve tangible, existante ou à produire, vérifiable par un évaluateur indépendant (ex: « Registres d'émargement signés », « Rapports d'analyse bactériologique de l'eau »).",

            [FieldSemanticType.Hypothese] =
                "RÈGLE RBM : Formulation POSITIVE d'une condition externe indispensable au succès du projet, échappant au contrôle direct de l'équipe de mise en œuvre.",

            [FieldSemanticType.Risque] =
                "RÈGLE RBM : Formulation NÉGATIVE d'un événement incertain pouvant compromettre l'atteinte des résultats, assortie d'une probabilité et d'une mesure d'atténuation réaliste.",

            [FieldSemanticType.JustificationBudgetaire] =
                "RÈGLE RBM : Lien direct et explicite entre la ligne de dépense et l'activité qu'elle soutient, détail de calcul unitaire (Prix unitaire x Quantité x Fréquence) et principe d'économie de moyens.",

            [FieldSemanticType.ContexteGeneral] =
                "RÈGLE RBM : Ancrage territorial précis, données de base chiffrées (baseline), cartographie des besoins réels et cohérence stratégique avec les priorités du bailleur.",

            [FieldSemanticType.Generique] =
                "RÈGLE GÉNÉRALE : Clarté, rigueur stylistique, vocabulaire institutionnel de la coopération internationale et concision."
        };

        /// <summary>
        /// Renvoie les directives RBM spécifiques pour un type sémantique.
        /// </summary>
        public static string GetSemanticGuidelines(FieldSemanticType type)
        {
            if (Guidelines.TryGetValue(type, out var val)) return val;
            return Guidelines[FieldSemanticType.Generique];
        }

        /// <summary>
        /// Détecte intelligemment le type sémantique à partir du nom du contrôle, du libellé ou du tag.
        /// </summary>
        public static FieldSemanticType DetectSemanticType(string? fieldName, string? fieldLabel, string? tag = null)
        {
            string combined = $"{fieldName} {fieldLabel} {tag}".ToLowerInvariant();

            if (combined.Contains("titre") || combined.Contains("title") || combined.Contains("intitulé"))
                return FieldSemanticType.Titre;

            if (combined.Contains("problème central") || combined.Contains("probleme central") || combined.Contains("coreproblem"))
                return FieldSemanticType.ProblemeCentral;

            if (combined.Contains("cause"))
                return FieldSemanticType.Cause;

            if (combined.Contains("effet") || combined.Contains("consequence") || combined.Contains("conséquence"))
                return FieldSemanticType.Effet;

            if (combined.Contains("objectif général") || combined.Contains("objectif general") || combined.Contains("generalobjective"))
                return FieldSemanticType.ObjectifGeneral;

            if (combined.Contains("objectif spécifique") || combined.Contains("objectif specifique") || combined.Contains("os") || combined.Contains("specificobjective"))
                return FieldSemanticType.ObjectifSpecifique;

            if (combined.Contains("résultat") || combined.Contains("resultat") || combined.Contains("extrant") || combined.Contains("output") || combined.Contains("outcome"))
                return FieldSemanticType.Resultat;

            if (combined.Contains("activité") || combined.Contains("activite") || combined.Contains("action"))
                return FieldSemanticType.Activite;

            if (combined.Contains("indicateur") || combined.Contains("indicator") || combined.Contains("cible") || combined.Contains("target"))
                return FieldSemanticType.Indicateur;

            if (combined.Contains("source") || combined.Contains("vérification") || combined.Contains("verification") || combined.Contains("mov"))
                return FieldSemanticType.SourceVerification;

            if (combined.Contains("hypothèse") || combined.Contains("hypothese") || combined.Contains("assumption"))
                return FieldSemanticType.Hypothese;

            if (combined.Contains("risque") || combined.Contains("risk") || combined.Contains("mitigation") || combined.Contains("atténuation"))
                return FieldSemanticType.Risque;

            if (combined.Contains("justification budgétaire") || combined.Contains("budget") || combined.Contains("cout") || combined.Contains("coût") || combined.Contains("dépense"))
                return FieldSemanticType.JustificationBudgetaire;

            if (combined.Contains("contexte") || combined.Contains("justification") || combined.Contains("background") || combined.Contains("besoin") || combined.Contains("need"))
                return FieldSemanticType.ContexteGeneral;

            return FieldSemanticType.Generique;
        }
    }
}

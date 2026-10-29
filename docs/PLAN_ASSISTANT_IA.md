# Plan de Développement - Assistant IA Rédactionnel Universel
**Application :** AFMI Montage Projet  
**Date :** 13 Octobre 2026  
**Auteur :** Architecte Logiciel Senior .NET / RBM Expert  
**Statut :** En cours d'exécution autonome (Pré-approbation active)

---

## 1. Vision et Objectifs
L'objectif est d'intégrer un assistant IA de rédaction généralisé à **tous les composants et zones de texte du logiciel sans exception**, du titre du projet jusqu'aux justifications budgétaires et indicateurs, avec trois actions cardinales :
1. **Enrichir** : Ajoute du contenu pertinent issu du contexte du projet (arguments, précisions méthodologiques, exemples) en conservant scrupuleusement le texte d'origine. Aucune invention de données non étayées.
2. **Détailler** : Développe le texte existant en sous-points, étapes et explications concrètes sans introduire de nouvelles affirmations déconnectées. Augmente la profondeur sans altérer le sens.
3. **Reformuler** : Réécrit pour une clarté maximale, une élégance syntaxique et un registre institutionnel adapté aux bailleurs de fonds (GAR / RBM), à longueur équivalente.

---

## 2. Architecture Technique

### 2.1 Modèle de Données & Persistance (EF Core 9)
- **`TextVersionHistory`** : Sauvegarde immuable de chaque modification assistée (original, proposition, texte final, fournisseur, modèle, identifiant de champ, horodatage) pour historique persistant et restauration après redémarrage.
- **`TextAssistSettings`** : Préférences mémorisées par utilisateur et par projet (longueur cible, registre/ton, langue de sortie, consigne libre).
- **`TextAssistAuditLog`** : Traçabilité des actions IA (acceptations, modifications, rejets, durée d'exécution).
- Sauvegarde automatique de la base locale SQLite avant toute migration via `BackupDatabaseBeforeMigration()`.

### 2.2 Typage Sémantique RBM & Contexte Ciblé
- **`FieldSemanticType`** : Énumération complète couvrant tous les rôles du Cadre Logique (Titre, Problème Central, Cause, Effet, Objectif Général, Objectif Spécifique, Résultat/Extrant, Activité, Indicateur SMART, Source de Vérification, Hypothèse, Risque, Justification Budgétaire, Contexte Général).
- **`FieldDescriptor`** : Métadonnées associées à un champ (type sémantique, étiquette, niveau hiérarchique, contraintes).
- **`FieldTypeRegistry`** : Règles de rédaction RBM par type de champ (ex: verbe à l'infinitif pour activité, participe passé/état atteint pour résultat, négation stricte pour problème, critères SMART pour indicateur).
- **`ProjectContextBuilder.BuildTargetedContext`** : Extraction contextuelle ciblée (uniquement les nœuds parents pertinents) pour minimiser les jetons et réduire la latence.

### 2.3 Garde-Fous de Fidélité & Sortie Structurée
- **`FidelityGuard`** :
  - Extraction et vérification systématique des invariants (nombres, dates, codes comme OS1/R1.2/A1.1, montants, noms propres).
  - Détection d'hallucinations de métriques ou sources non fondées.
  - Remplacement obligatoire des manques par le marqueur `[à compléter : …]`.
  - Contrôle post-génération : en cas de non-respect, **une tentative corrective automatique** est déclenchée avec consigne explicite sur les éléments omis. En cas de second échec, avertissement visuel clair affiché à l'utilisateur.
- **Sortie Structurée (JSON Schema)** :
  ```json
  {
    "proposal": "texte proposé",
    "context_used": ["élément de contexte 1", "élément 2"],
    "warnings": ["avertissement éventuel"],
    "placeholders": ["données manquantes à compléter"]
  }
  ```

### 2.4 Expérience Utilisateur & Intégration WPF
- **Comportement Universel (`AiTextAssistant`)** :
  - Propriétés attachées `FieldSemanticType`, `FieldLabel`, `IsExcluded`, `ExclusionReason`.
  - Hook global sur `TextBoxBase` (TextBox et RichTextBox) interceptant :
    - Menu contextuel unifié (Clic droit).
    - Raccourcis clavier directs : `Ctrl+Shift+E` (Enrichir), `Ctrl+Shift+D` (Détailler), `Ctrl+Shift+R` (Reformuler).
  - Bouton Adorner flottant discret en haut à droite des zones actives/survolées.
- **Dialogue de Comparaison Diff (`AiTextAssistPreviewDialog`)** :
  - Diff textuel mot à mot avec mise en surbrillance colorée des ajouts (vert pour ajouts, bleu pour reformulation).
  - Panneau des éléments de contexte utilisés et avertissements de fidélité.
  - Actions : *Accepter*, *Modifier puis accepter*, *Régénérer*, *Rejeter*, *Insérer à la suite*, *Copier*.
  - Sélecteur de versions antérieures avec restauration en 1 clic.
- **Mode Lot (`AiBatchAssistDialog`)** :
  - Traitement en masse d'une section ou de tout le projet (ex: tous les indicateurs, toutes les activités).
  - File d'attente avec prévisualisation écran par écran avant validation finale.

---

## 3. Plan d'Exécution par Phases

| Phase | Description | Livrables Clés | Statut |
|---|---|---|---|
| **Phase 1** | Planification architecturale & ADR | `docs/PLAN_ASSISTANT_IA.md`, `docs/DECISIONS.md` | **Terminé** (Vérifié) |
| **Phase 2** | Modèles EF Core & Migrations sécurisées | `Models/TextAssistModels.cs`, `AppDbContext.cs` | **Terminé** (Vérifié) |
| **Phase 3** | Moteur Sémantique, Contexte Ciblé & Fidélité | `FieldDescriptor.cs`, `FieldTypeRegistry.cs`, `FidelityGuard.cs`, `ProjectContextBuilder.cs`, `TextAssistService.cs` | **Terminé** (Vérifié) |
| **Phase 4** | Intégration WPF Universelle & Diff UI | `AiTextAssistant.cs`, `AiButtonAdorner.cs`, `TextDiffHelper.cs`, `AiTextEnhancementDialog.xaml` | **Terminé** (Vérifié) |
| **Phase 5** | Mode Lot & Historique de Version | `AiBatchAssistDialog.xaml`, restauration des versions | **Terminé** (Vérifié) |
| **Phase 6** | Exclusions Déclaratives & Test de Couverture | `docs/EXCLUSIONS_TEXT_ASSIST.md`, `XamlCoverageTests.cs` (148 contrôles XAML vérifiés) | **Terminé** (Vérifié) |
| **Phase 7** | Suite de Tests Unitaires & Fidélité | `TextAssistTests.cs` (97/97 tests au vert) | **Terminé** (Vérifié) |
| **Phase 8** | Consolidation & Rapport Final | `docs/REPORT_ASSISTANT_IA.md`, compilation & build 100% propre (0 erreurs, 0 avertissements) | **Terminé** (Vérifié) |

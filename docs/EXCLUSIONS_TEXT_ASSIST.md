# Registre Exhaustif des Exclusions de l'Assistant IA Rédactionnel
**Logiciel :** AFMI Montage Projet  
**Date d'audit :** 13 Octobre 2026  
**Statut :** Conforme & Validé par test unitaire automatisé (`XamlCoverageTests.cs`)

---

## 1. Principes et Règle d'Exclusion Stricte
Par conception architecturale, **toutes les zones de texte (`TextBox`, `RichTextBox`) de l'application sans exception** bénéficient automatiquement de l'Assistant IA Rédactionnel via les écouteurs globaux de classe (`EventManager.RegisterClassHandler(typeof(TextBoxBase), ...)`).

Une zone de texte ne peut être exemptée que si elle satisfait à **l'un des critères stricts suivants (conformément au cahier des charges)** :
1. **Secrets de sécurité & Authentification :** Clés d'API tierces, mots de passe en clair ou tokens d'accès (interdiction de fuite d'informations d'identification vers des modèles d'IA).
2. **Identifiants techniques & Protocoles réseau :** URLs d'API locales (ex: `http://localhost:11434`), identifiants OAuth de tiers (`GoogleClientId`, `GoogleClientSecret`), identifiants techniques de dossiers cloud.
3. **Champs d'identification de connexion :** Nom d'utilisateur ou email sur les écrans de login/inscription (non pertinent pour l'ingénierie de projet).
4. **Champs de dates & sélecteurs temporels :** Contrôles `DatePickerTextBox`, `DatePicker`, `Calendar` (ex: `/2026`) exclus automatiquement par détection d'arbre visuel.
5. **Champs purement numériques et monétaires :** Montants budgétaires, taux, scores, quantités et devises mono-lignes sans contenu rédactionnel narratif (`AcceptsReturn == false`).
6. **Chemins de fichiers et codes générés automatiquement.**

---

## 2. Inventaire Exhaustif des Contrôles Exclus

| Fichier XAML | Contrôle / Binding | Catégorie | Justification Détaillée | Déclaratif XAML |
|---|---|---|---|---|
| `Views/SettingsView.xaml` | `TextBox Text="{Binding ApiKey}"` | **Secret Sécurité** | Clé d'API chiffrée DPAPI pour les services Cloud (Gemini, OpenAI, Claude). Interdiction formelle d'envoyer un secret cryptographique à l'assistant. | `AiTextAssistant.IsExcluded="True"` |
| `Views/SettingsView.xaml` | `TextBox Text="{Binding GoogleClientId}"` | **Secret OAuth** | Identifiant client Google Drive OAuth 2.0. Donnée de protocole d'infrastructure, sans contenu rédactionnel. | `AiTextAssistant.IsExcluded="True"` |
| `Views/SettingsView.xaml` | `TextBox Text="{Binding GoogleClientSecret}"` | **Secret OAuth** | Clé secrète Google OAuth 2.0. Secret critique dont la transmission à un LLM violerait la sécurité de l'application. | `AiTextAssistant.IsExcluded="True"` |
| `Views/SettingsView.xaml` | `TextBox Text="{Binding OllamaUrl}"` | **URL Réseau** | Adresse du serveur local Ollama (`http://localhost:11434`). Configuration réseau sans valeur sémantique pour un assistant de rédaction. | `AiTextAssistant.IsExcluded="True"` |
| `Views/SettingsView.xaml` | `TextBox Text="{Binding OllamaModel}"` | **ID Modèle** | Nom technique du modèle local installé dans Ollama (ex: `llama3:8b`). Chaîne technique système. | `AiTextAssistant.IsExcluded="True"` |
| `Views/UserAccountWindow.xaml` | `TextBox Text="{Binding NewPassword}"` | **Mot de Passe** | Saisie en texte clair du nouveau mot de passe utilisateur. Confidentialité absolue requise. | `AiTextAssistant.IsExcluded="True"` |
| `Views/LoginWindow.xaml` | `TextBox Text="{Binding UsernameOrEmail}"` | **Identifiant Connexion** | Identifiant de connexion ou email du compte utilisateur. Pas de contenu d'ingénierie de projet. | `AiTextAssistant.IsExcluded="True"` |
| `Views/RegisterWindow.xaml` | `TextBox Text="{Binding Username}"` | **Identifiant Compte** | Pseudonyme utilisateur à la création du compte. | `AiTextAssistant.IsExcluded="True"` |
| `Views/RegisterWindow.xaml` | `TextBox Text="{Binding Email}"` | **Email Utilisateur** | Adresse de courriel de l'utilisateur pour l'inscription. | `AiTextAssistant.IsExcluded="True"` |
| `Views/RegisterWindow.xaml` | `TextBox Text="{Binding AccessCodeInput}"` | **Secret OAuth** | Code d'accès de l'équipe saisi à l'inscription quand le poste n'a pas l'identifiant Google (identifiant et secret client encodés). Ne doit jamais être transmis à un LLM. | `AiTextAssistant.IsExcluded="True"` |
| `Views/CloudSyncView.xaml` | `TextBox Text="{Binding Settings.Host}"` | **URL Réseau** | Adresse du serveur PostgreSQL distant. Infrastructure, aucun contenu rédactionnel. | `AiTextAssistant.IsExcluded="True"` |
| `Views/CloudSyncView.xaml` | `TextBox Text="{Binding Settings.Port}"` | **Champ numérique** | Port réseau du serveur PostgreSQL. | `AiTextAssistant.IsExcluded="True"` |
| `Views/CloudSyncView.xaml` | `TextBox Text="{Binding Settings.Database}"` | **ID Technique** | Nom technique de la base de données distante. | `AiTextAssistant.IsExcluded="True"` |
| `Views/CloudSyncView.xaml` | `TextBox Text="{Binding Settings.Username}"` | **Identifiant Connexion** | Compte de connexion au serveur PostgreSQL. | `AiTextAssistant.IsExcluded="True"` |
| `Views/TeamWorkspaceWindow.xaml` | `TextBox Text="{Binding AccessCodeInput}"` | **Secret OAuth** | Code d'accès de l'équipe (identifiant et secret client Google encodés). Ne doit jamais être transmis à un LLM. | `AiTextAssistant.IsExcluded="True"` |
| `Views/TeamWorkspaceWindow.xaml` | `TextBox Text="{Binding NewMemberEmail}"` | **Email Utilisateur** | Adresse Google du membre à inviter dans l'espace d'équipe. Donnée personnelle sans contenu rédactionnel. | `AiTextAssistant.IsExcluded="True"` |
| `Views/SimilarProjectsView.xaml` | `TextBox Text="{Binding Keywords}"` | **Requête technique** | Mots-clés anglais transmis aux bases de projets (Banque mondiale, IATI). Une reformulation IA modifierait la requête. | `AiTextAssistant.IsExcluded="True"` |
| `Views/TranslationWindow.xaml` | `TextBox x:Name="TxtTranslation"` | **Résultat IA en relecture** | Traduction produite par l'IA, en cours de relecture dans la fenêtre de traduction : un nouvel appel à l'assistant sur ce champ serait récursif. | `AiTextAssistant.IsExcluded="True"` |
| `Views/SettingsView.xaml` | `TextBox Text="{Binding GitHubRepository}"` | **ID Technique** | Identifiant du dépôt GitHub des mises à jour (propriétaire/dépôt). | `AiTextAssistant.IsExcluded="True"` |
| `Views/Radar/ProjectRadarView.xaml` | `TextBox Text="{Binding NewUpcomingLink}"` | **URL Réseau** | Lien officiel d'un appel « À venir » (base de veille). Une adresse web ne doit pas être reformulée. | `AiTextAssistant.IsExcluded="True"` |

*Note sur les contrôles `PasswordBox` :* Les contrôles natifs `PasswordBox` (`LoginWindow.xaml`, `RegisterWindow.xaml`, `CloudSyncView.xaml`) n'héritent pas de `TextBoxBase` et sont par nature physiquement étanches et exempts de tout hook d'assistance. Le mot de passe PostgreSQL de `CloudSyncView.xaml` était auparavant saisi dans un `TextBox` non exclu (donc exposé à l'assistant IA) ; il utilise désormais un `PasswordBox` et est stocké chiffré DPAPI (`PasswordProtected` dans `dbconfig.json`).

---

## 3. Garantie de Couverture Intégrale (100 %)
En dehors des 19 contrôles expressément et légitimement exclus ci-dessus pour des motifs de sécurité ou d'infrastructure, **100 % des autres zones de texte (139 champs répartis dans les 26 vues et composants du logiciel)** sont intégralement dotées de l'Assistant Rédactionnel IA :

- **Socle & Cadrage :** Titre, Justification, Problème central, Bénéficiaires, Zone d'intervention, Objectif général (`CadrageView.xaml`, `StrategicPlanningView.xaml`).
- **Analyses Stratégiques :** SWOT (Forces, Faiblesses, Opportunités, Menaces), PESTEL (6 dimensions), Évaluation des besoins, Options stratégiques (`StrategicPlanningView.xaml`).
- **Chaîne causale & Arbre des problèmes :** Problème central, causes directes/indirectes, effets, arbre des solutions (`ArbreProblemeView.xaml`, `TheoryOfChangeVisualView.xaml`).
- **Cadre Logique & Résultats :** Objectifs spécifiques (OS1, OS2...), Résultats/Extrants, Activités, Hypothèses, Risques (`CadreLogiqueView.xaml`, `StrategicPlanningView.xaml`, `RisquesView.xaml`).
- **Indicateurs & Suivi :** Indicateurs SMART, cibles intermédiaires, sources de vérification (`IndicateursView.xaml`, `CadreLogiqueView.xaml`).
- **Budget & Justifications :** Libellés de lignes, calculs de coûts, notes justificatives d'activités (`BudgetView.xaml`).
- **Rapports & Collaboration :** Rapports d'avancement narratifs, suggestions, justifications de vote, propositions d'ateliers (`ActivityReportsView.xaml`, `CollaborationView.xaml`, `IdeationInnovationWindow.xaml`).
- **Formulaires dynamiques & Canvas :** Champs personnalisés de modèles sectoriels, post-it de conception narrative (`FormulaireDynamiqueView.xaml`, `CanvasDesignerWindow.xaml`).

La conformité de cette règle est auditée à chaque compilation par le test automatique `AFMI_Montage_Projet.Tests.XamlCoverageTests.AllXamlTextBoxes_MustBeCoveredOrDocumentedAsExcluded`.

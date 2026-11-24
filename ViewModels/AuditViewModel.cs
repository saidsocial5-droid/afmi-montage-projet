using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Strategic;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class AuditViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private string _auditReport = "Cliquez sur 'Lancer l'audit' pour analyser la cohérence logique et financière de votre projet.";

        [ObservableProperty]
        private string _brainstormTheme = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Project> _availableProjects = new();

        [ObservableProperty]
        private Project? _selectedBrainstormProject;

        [ObservableProperty]
        private string _generatedTitle = string.Empty;

        [ObservableProperty]
        private string _generatedContext = string.Empty;

        [ObservableProperty]
        private string _generatedObjective = string.Empty;

        [ObservableProperty]
        private string _generatedLogicalFramework = string.Empty;

        [ObservableProperty]
        private bool _integrateTitle = false;

        [ObservableProperty]
        private bool _integrateContext = false;

        [ObservableProperty]
        private bool _integrateObjective = false;

        [ObservableProperty]
        private bool _integrateLogicalFramework = false;

        [ObservableProperty]
        private string _brainstormResult = string.Empty;

        [ObservableProperty]
        private string _textToCorrect = string.Empty;

        [ObservableProperty]
        private string _correctedText = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private bool _searchInternet = false;

        [ObservableProperty]
        private ObservableCollection<string> _availableLanguages = new(new[] { "Français", "Anglais", "Espagnol", "Arabe", "Portugais" });

        [ObservableProperty]
        private string _selectedLanguage = "Français";

        [ObservableProperty]
        private bool _generateDetailed = false;

        [ObservableProperty]
        private string _uploadedFileName = "Aucun fichier sélectionné (Word, PDF, Excel)";

        [ObservableProperty]
        private string _extractedDocumentText = string.Empty;

        [ObservableProperty]
        private string _documentPrompt = string.Empty;

        [ObservableProperty]
        private string _documentReport = "Chargez un document externe (appel à projets, canevas ou ancien projet clos) pour commencer l'analyse.";

        // ===== Diagnostic global déterministe (hors IA) =====

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAnalysis))]
        private ProjectAnalysisResult? _analysis;

        public bool HasAnalysis => Analysis != null;

        // null tant que le diagnostic n'a pas abouti (évite d'afficher « aucun constat » à tort)
        [ObservableProperty]
        private ObservableCollection<QualityFinding>? _findings;

        [ObservableProperty]
        private ObservableCollection<StageReadiness> _stages = new();

        public AuditViewModel(Project project)
        {
            _project = project;
            using var db = new AppDbContext();
            AvailableProjects = new ObservableCollection<Project>(db.Projects.ToList());
            SelectedBrainstormProject = AvailableProjects.FirstOrDefault(p => p.Id == project.Id);

            _ = LancerDiagnostic();
        }

        /// <summary>
        /// Diagnostic instantané et hors-ligne : règles RBM, cohérence entre modules et portes de maturité.
        /// </summary>
        [RelayCommand]
        public async Task LancerDiagnostic()
        {
            try
            {
                var analysis = await ProjectAnalysisService.Instance.AnalyzeAsync(_project.Id);
                Analysis = analysis;
                Findings = new ObservableCollection<QualityFinding>(analysis.Quality.Findings.OrderByDescending(f => f.Severity));
                Stages = new ObservableCollection<StageReadiness>(analysis.Stages);
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AuditViewModel), "Échec du diagnostic global du projet.", ex);
                StatusMessage = $"Erreur du diagnostic : {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task LancerAudit()
        {
            IsBusy = true;
            StatusMessage = "Diagnostic et audit IA en cours...";
            AuditReport = "L'Agent IA étudie votre dossier de projet. Veuillez patienter...";

            try
            {
                // 1. Diagnostic déterministe à jour : sert de base factuelle à l'IA
                await LancerDiagnostic();
                if (Analysis == null)
                {
                    AuditReport = "Le diagnostic du projet n'a pas pu être établi.";
                    return;
                }

                // 2. Contexte complet du projet (socle, cadre logique, chronogramme, budget, risques, documents)
                string projectContext = await Task.Run(() => ProjectContextBuilder.BuildFullContext(_project.Id));

                string prompt =
                    "Réalise un audit de cohérence de ce projet de développement selon la gestion axée sur les résultats (GAR/RBM).\n" +
                    "Un contrôle automatique a déjà établi les constats factuels ci-dessous : ne les contredis pas et ne les recalcule pas.\n" +
                    "Ton rôle : (1) expliquer en quoi ces constats affaiblissent le dossier face à un bailleur, " +
                    "(2) détecter les incohérences de fond que des règles ne peuvent pas voir (logique causale problème → objectifs → résultats → activités, " +
                    "pertinence des activités, réalisme du budget et des délais, alignement avec les priorités du bailleur), " +
                    "(3) proposer un plan d'action priorisé (du plus bloquant au moins urgent) en citant les codes des éléments (OS1, R1.1, A1.1.2...).\n" +
                    "N'invente aucun chiffre, date ou nom absent du dossier.\n\n" +
                    "=== CONSTATS DU CONTRÔLE AUTOMATIQUE ===\n" + Analysis.ToPromptSummary() + "\n" +
                    "=== DOSSIER DU PROJET ===\n" + projectContext;

                string systemInstruction = "Tu es un auditeur externe expert en montage de projets pour les bailleurs internationaux (UE, PNUD, FEM, AFD). " +
                                           "Tu es critique, constructif et factuel. Tu rédiges en français un rapport structuré : Synthèse, Points forts, Faiblesses et incohérences, Plan d'action priorisé.";

                // 3. Appel IA
                string response = await AiAssistantService.CallAiAsync(prompt, systemInstruction);
                AuditReport = response;
                StatusMessage = "Audit terminé.";
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AuditViewModel), "Échec de l'audit IA du projet.", ex);
                AuditReport = $"Erreur lors de l'audit : {ex.Message}";
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string ExtractSection(string text, string tag)
        {
            string startTag = $"<{tag}>";
            string endTag = $"</{tag}>";
            
            int start = text.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (start < 0) 
            {
                // Fallback for square brackets
                startTag = $"[{tag}]";
                endTag = $"[/{tag}]";
                start = text.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            }

            if (start < 0) 
            {
                // Fallback for French translation
                if (tag == "TITLE") return ExtractSection(text, "TITRE");
                if (tag == "CONTEXT") return ExtractSection(text, "CONTEXTE");
                if (tag == "OBJECTIVE") return ExtractSection(text, "OBJECTIF");
                if (tag == "LOGICAL_FRAMEWORK") return ExtractSection(text, "CADRE_LOGIQUE");
                if (tag == "REPORT") return ExtractSection(text, "RAPPORT");
                
                return "";
            }
            start += startTag.Length;
            
            int end = text.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) 
            {
                return text.Substring(start).Trim();
            }
            
            return text.Substring(start, end - start).Trim();
        }

        [RelayCommand]
        public async Task GenererIdees()
        {
            if (SelectedBrainstormProject == null)
            {
                StatusMessage = "Veuillez sélectionner un projet.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Génération d'idées de projets en cours...";
            BrainstormResult = "Recherche et analyse en cours...";

            try
            {
                using var db = new AppDbContext();
                var docs = await db.ProjectDocuments.Where(d => d.ProjectId == SelectedBrainstormProject.Id).ToListAsync();
                string docsSummary = string.Join("\n\n", docs.Select(d => d.AiSummary).Where(t => !string.IsNullOrWhiteSpace(t)));

                string lengthInstruction = GenerateDetailed ? "Consigne très importante : Fournis une réponse LONGUE, TRÈS DÉTAILLÉE et EXHAUSTIVE. Ne fais pas de raccourcis, développe tes arguments et explications au maximum." : "Consigne : Fournis une réponse concise et directe.";

                string prompt = $@"
Améliore et innove ce projet. Thème/Secteur souhaité : {BrainstormTheme}
Titre actuel: {SelectedBrainstormProject.Title}
Contexte actuel: {SelectedBrainstormProject.ContextJustification}
Objectif actuel: {SelectedBrainstormProject.GeneralObjective}

Documents associés (le cas échéant):
{docsSummary}

{lengthInstruction}
Langue exigée : Tu dois impérativement générer tout le contenu en {SelectedLanguage}.

Retourne UNIQUEMENT ta réponse selon ce format structuré exact (N'UTILISE PAS DE JSON, utilise ces balises XML EXACTES SANS LES TRADUIRE) :

<TITLE>
(un nouveau titre accrocheur et innovant en {SelectedLanguage})
</TITLE>

<CONTEXT>
(un contexte amélioré avec une justification solide inspirée par les recherches en {SelectedLanguage})
</CONTEXT>

<OBJECTIVE>
(un objectif général ambitieux et mesurable en {SelectedLanguage})
</OBJECTIVE>

<LOGICAL_FRAMEWORK>
(le cadre logique complet en {SelectedLanguage} contenant les Objectifs Spécifiques, Résultats et Activités.
FORMAT OBLIGATOIRE ligne par ligne :
OS: [Texte de l'objectif spécifique]
R: [Texte du résultat]
A: [Texte de l'activité]
)
</LOGICAL_FRAMEWORK>

<REPORT>
(un rapport TRÈS détaillé en {SelectedLanguage} listant les idées et sources trouvées sur Internet)
</REPORT>
";

                string sys = $"Tu es un consultant expert. Réponds IMPÉRATIVEMENT en {SelectedLanguage}. N'utilise pas de JSON. Utilise uniquement les balises XML (ne traduis pas les noms des balises).";
                
                string result = await AiAssistantService.CallAiAsync(prompt, sys, enableSearch: SearchInternet);
                
                string title = ExtractSection(result, "TITLE");
                string context = ExtractSection(result, "CONTEXT");
                string objective = ExtractSection(result, "OBJECTIVE");
                string logicalFramework = ExtractSection(result, "LOGICAL_FRAMEWORK");
                string report = ExtractSection(result, "REPORT");

                // Fallback robust: si aucune balise trouvée, afficher le brut dans le rapport
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(context) && string.IsNullOrWhiteSpace(report))
                {
                    BrainstormResult = $"L'IA n'a pas utilisé les balises demandées. Voici le texte brut :\n\n{result}";
                    StatusMessage = "Attention : Format de réponse inattendu.";
                }
                else
                {
                    GeneratedTitle = title;
                    GeneratedContext = context;
                    GeneratedObjective = objective;
                    GeneratedLogicalFramework = logicalFramework;
                    BrainstormResult = report;
                    StatusMessage = "Idées générées avec succès ! Vous pouvez maintenant choisir de les intégrer.";
                }
            }
            catch (Exception ex)
            {
                BrainstormResult = $"Erreur : {ex.Message}";
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task EnrichirIdees()
        {
            if (string.IsNullOrWhiteSpace(GeneratedContext) && string.IsNullOrWhiteSpace(GeneratedObjective))
            {
                StatusMessage = "Veuillez d'abord générer des idées avant de les enrichir.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Enrichissement des textes en cours...";
            
            try
            {
                string prompt = $@"
Prends les textes suivants et développe-les de manière EXHAUSTIVE, TRÈS DÉTAILLÉE et PROFESSIONNELLE. Ajoute des justifications profondes, des arguments en béton et des exemples. Ne sois pas bref. La langue de réponse obligatoire est : {SelectedLanguage}.

Titre actuel: {GeneratedTitle}
Contexte actuel: {GeneratedContext}
Objectif actuel: {GeneratedObjective}
Rapport actuel: {BrainstormResult}

Retourne UNIQUEMENT ta réponse selon ce format structuré exact (N'UTILISE PAS DE JSON, utilise ces balises XML EXACTES SANS LES TRADUIRE) :

<TITLE>
(Titre enrichi en {SelectedLanguage})
</TITLE>

<CONTEXT>
(Contexte très développé et argumenté en {SelectedLanguage})
</CONTEXT>

<OBJECTIVE>
(Objectif étoffé et très détaillé en {SelectedLanguage})
</OBJECTIVE>

<LOGICAL_FRAMEWORK>
(Cadre logique enrichi et très détaillé en {SelectedLanguage}. Maintiens le même format de préfixes: OS: , R: , A: )
</LOGICAL_FRAMEWORK>

<REPORT>
(Rapport enrichi et fusionné en {SelectedLanguage})
</REPORT>
";
                string sys = $"Tu es un consultant expert. Réponds IMPÉRATIVEMENT en {SelectedLanguage}. N'utilise pas de JSON. Utilise uniquement les balises XML (ne traduis pas les noms des balises).";
                
                string result = await AiAssistantService.CallAiAsync(prompt, sys, enableSearch: SearchInternet);
                
                string title = ExtractSection(result, "TITLE");
                string context = ExtractSection(result, "CONTEXT");
                string objective = ExtractSection(result, "OBJECTIVE");
                string logicalFramework = ExtractSection(result, "LOGICAL_FRAMEWORK");
                string report = ExtractSection(result, "REPORT");

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(context) && string.IsNullOrWhiteSpace(report))
                {
                    BrainstormResult += $"\n\n--- Échec de l'enrichissement (format inattendu) ---\n{result}";
                    StatusMessage = "Attention : Format inattendu pour l'enrichissement.";
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(title)) GeneratedTitle = title;
                    if (!string.IsNullOrWhiteSpace(context)) GeneratedContext = context;
                    if (!string.IsNullOrWhiteSpace(objective)) GeneratedObjective = objective;
                    if (!string.IsNullOrWhiteSpace(logicalFramework)) GeneratedLogicalFramework = logicalFramework;
                    if (!string.IsNullOrWhiteSpace(report)) BrainstormResult = report;

                    StatusMessage = "Les éléments ont été enrichis et développés avec succès !";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'enrichissement : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void IntegrerIdees()
        {
            if (SelectedBrainstormProject == null) return;
            try
            {
                using var db = new AppDbContext();
                var dbProject = db.Projects.FirstOrDefault(p => p.Id == SelectedBrainstormProject.Id);
                if (dbProject != null)
                {
                    if (IntegrateTitle && !string.IsNullOrWhiteSpace(GeneratedTitle)) dbProject.Title = GeneratedTitle;
                    if (IntegrateContext && !string.IsNullOrWhiteSpace(GeneratedContext)) dbProject.ContextJustification = GeneratedContext;
                    if (IntegrateObjective && !string.IsNullOrWhiteSpace(GeneratedObjective)) dbProject.GeneralObjective = GeneratedObjective;

                    if (IntegrateLogicalFramework && !string.IsNullOrWhiteSpace(GeneratedLogicalFramework))
                    {
                        var lines = GeneratedLogicalFramework.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        Objective? currentObj = null;
                        Result? currentRes = null;
                        
                        int objCount = db.Objectives.Count(o => o.ProjectId == dbProject.Id);
                        int resCount = 0;
                        int actCount = 0;

                        foreach (var line in lines)
                        {
                            string cleanLine = line.Trim();
                            if (cleanLine.StartsWith("- ")) cleanLine = cleanLine.Substring(2).Trim();
                            if (cleanLine.StartsWith("* ")) cleanLine = cleanLine.Substring(2).Trim();
                            
                            var osMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(?:OS|Objectif Spécifique)[^:]*:\s*(.+)");
                            var rMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(?:R|Résultat)[^:]*:\s*(.+)");
                            var aMatch = System.Text.RegularExpressions.Regex.Match(cleanLine, @"^(?:A|Activité)[^:]*:\s*(.+)");

                            if (osMatch.Success)
                            {
                                objCount++;
                                resCount = 0;
                                currentObj = new Objective { 
                                    ProjectId = dbProject.Id, 
                                    Code = $"OS {objCount}", 
                                    Description = osMatch.Groups[1].Value.Trim(),
                                    AiSuggestionBadge = "Généré par IA"
                                };
                                db.Objectives.Add(currentObj);
                                db.SaveChanges();
                            }
                            else if (rMatch.Success && currentObj != null)
                            {
                                resCount++;
                                actCount = 0;
                                currentRes = new Result {
                                    ObjectiveId = currentObj.Id,
                                    Code = $"R {objCount}.{resCount}",
                                    Description = rMatch.Groups[1].Value.Trim(),
                                    AiSuggestionBadge = "Généré par IA"
                                };
                                db.Results.Add(currentRes);
                                db.SaveChanges();
                            }
                            else if (aMatch.Success && currentRes != null)
                            {
                                actCount++;
                                var act = new Activity {
                                    ResultId = currentRes.Id,
                                    Code = $"A {objCount}.{resCount}.{actCount}",
                                    Description = aMatch.Groups[1].Value.Trim(),
                                    AiSuggestionBadge = "Généré par IA"
                                };
                                db.Activities.Add(act);
                                db.SaveChanges();
                            }
                        }
                    }

                    db.SaveChanges();

                    if (dbProject.Id == _project.Id)
                    {
                        if (IntegrateTitle && !string.IsNullOrWhiteSpace(GeneratedTitle)) _project.Title = GeneratedTitle;
                        if (IntegrateContext && !string.IsNullOrWhiteSpace(GeneratedContext)) _project.ContextJustification = GeneratedContext;
                        if (IntegrateObjective && !string.IsNullOrWhiteSpace(GeneratedObjective)) _project.GeneralObjective = GeneratedObjective;
                    }

                    StatusMessage = "Éléments intégrés avec succès !";
                    System.Windows.MessageBox.Show("Les éléments choisis ont été intégrés avec succès au projet !", "Succès", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur lors de l'intégration : " + ex.Message;
            }
        }

        [RelayCommand]
        public async Task CorrigerTexte()
        {
            if (string.IsNullOrWhiteSpace(TextToCorrect))
            {
                StatusMessage = "Veuillez coller un texte à corriger.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Correction en cours...";
            CorrectedText = "L'Agent IA corrige votre texte...";

            try
            {
                string prompt = $"Corrige et améliore le style du texte suivant pour qu'il soit professionnel et adapté à un dossier de demande de subvention :\n\n\"{TextToCorrect}\"";
                string systemInstruction = "Tu es un rédacteur professionnel expert en projets associatifs et ONG. Corrige les fautes, améliore la syntaxe, et renvoie uniquement le texte corrigé.";

                string response = await AiAssistantService.CallAiAsync(prompt, systemInstruction);
                CorrectedText = response;
                StatusMessage = "Correction terminée.";
            }
            catch (Exception ex)
            {
                CorrectedText = $"Erreur : {ex.Message}";
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void LoadDocument()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Fichiers PDF (*.pdf)|*.pdf|Documents Word (*.docx)|*.docx|Tous les fichiers (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                UploadedFileName = System.IO.Path.GetFileName(openFileDialog.FileName);
                ExtractedDocumentText = "Simulation : Texte extrait du document " + UploadedFileName + "...\n(L'extraction réelle nécessite iText7 ou DocumentFormat.OpenXml)";
                StatusMessage = "Document chargé et prêt pour l'analyse.";
                DocumentReport = "Le document est chargé. Posez une question ou demandez une analyse à l'Agent IA.";
            }
        }

        [RelayCommand]
        public async Task AnalyzeDocument()
        {
            if (string.IsNullOrWhiteSpace(ExtractedDocumentText))
            {
                StatusMessage = "Veuillez d'abord charger un document.";
                return;
            }
            if (string.IsNullOrWhiteSpace(DocumentPrompt))
            {
                StatusMessage = "Veuillez saisir une consigne (ex: 'Fais un résumé de l'appel à projets').";
                return;
            }

            IsBusy = true;
            StatusMessage = "Analyse du document en cours...";
            DocumentReport = "L'Agent IA analyse le document...";

            try
            {
                string prompt = $"Voici le contenu d'un document :\n\n{ExtractedDocumentText}\n\nConsigne : {DocumentPrompt}";
                string systemInstruction = "Tu es un analyste expert en projets de développement. Réponds de façon précise et concise à la demande basée sur le document fourni.";

                string response = await AiAssistantService.CallAiAsync(prompt, systemInstruction);
                DocumentReport = response;
                StatusMessage = "Analyse terminée.";
            }
            catch (Exception ex)
            {
                DocumentReport = $"Erreur : {ex.Message}";
                StatusMessage = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}



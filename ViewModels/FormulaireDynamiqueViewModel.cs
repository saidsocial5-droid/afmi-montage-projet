using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Views;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class FormulaireDynamiqueViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Project> _availableProjects = new();

        private Project? _selectedProject;
        public Project? SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (SetProperty(ref _selectedProject, value))
                {
                    LoadInstanceForProjectAndTemplate();
                }
            }
        }

        [ObservableProperty]
        private ObservableCollection<FormTemplate> _availableTemplates = new();

        private FormTemplate? _selectedTemplate;
        public FormTemplate? SelectedTemplate
        {
            get => _selectedTemplate;
            set
            {
                if (SetProperty(ref _selectedTemplate, value))
                {
                    LoadInstanceForProjectAndTemplate();
                }
            }
        }

        [ObservableProperty]
        private FormInstance? _currentInstance;

        [ObservableProperty]
        private bool _hasInstance = false;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        public Dictionary<string, string> Answers { get; set; } = new();

        public FormulaireDynamiqueViewModel(Project project)
        {
            using var db = new AppDbContext();
            AvailableProjects = new ObservableCollection<Project>(db.Projects.ToList());
            _selectedProject = AvailableProjects.FirstOrDefault(p => p.Id == project.Id) ?? AvailableProjects.FirstOrDefault();
            
            LoadTemplates();
            LoadInstanceForProjectAndTemplate();
        }

        private void LoadTemplates()
        {
            try
            {
                using var db = new AppDbContext();
                var templates = db.FormTemplates.ToList();
                
                if (templates.Count == 0)
                {
                    string schemaJson = @"[
                        {
                            ""Section"": ""1. Éligibilité & Cadrage Sectoriel"",
                            ""Fields"": [
                                { ""Name"": ""SectorIntervention"", ""Label"": ""Secteur prioritaire du projet"", ""Type"": ""Dropdown"", ""Options"": [""Conservation de la biodiversité"", ""Lutte contre la dégradation des terres"", ""Atténuation des changements climatiques"", ""Gestion durable des forêts""], ""Placeholder"": ""Sélectionner le domaine thématique..."" },
                                { ""Name"": ""IccaCategory"", ""Label"": ""Catégorie de territoire de vie (ICCA)"", ""Type"": ""Dropdown"", ""Options"": [""Forêt communautaire"", ""Zone côtière gérée localement"", ""Pâturages traditionnels"", ""Autre""], ""Placeholder"": ""Sélectionner la catégorie..."" },
                                { ""Name"": ""IsCommunityLed"", ""Label"": ""Le projet est-il initié et géré directement par des communautés autochtones ou locales ?"", ""Type"": ""Checkbox"", ""Placeholder"": """" }
                            ]
                        },
                        {
                            ""Section"": ""2. Analyse Narrative Avancée"",
                            ""Fields"": [
                                { ""Name"": ""CommunityParticipation"", ""Label"": ""Méthodologie de participation communautaire"", ""Type"": ""TextLong"", ""Placeholder"": ""Expliquez comment les bénéficiaires et les communautés locales sont impliqués dans les phases du projet..."" },
                                { ""Name"": ""GenderInclusionPlan"", ""Label"": ""Plan d'inclusion du genre et des jeunes"", ""Type"": ""TextLong"", ""Placeholder"": ""Précisez les mesures spécifiques pour garantir l'équité de genre et la place des jeunes..."" }
                            ]
                        },
                        {
                            ""Section"": ""3. Évaluation des Risques Sociaux & Environnementaux (Safeguards)"",
                            ""Fields"": [
                                { ""Name"": ""SafeguardBiodiversity"", ""Label"": ""Le projet présente-t-il des risques sur la biodiversité ou les zones protégées ?"", ""Type"": ""Dropdown"", ""Options"": [""Nul"", ""Faible"", ""Moyen"", ""Fort""], ""Placeholder"": ""Sélectionner le niveau de risque..."" },
                                { ""Name"": ""SafeguardIndigenous"", ""Label"": ""Le projet implique-t-il le déplacement physique ou économique de populations ?"", ""Type"": ""Dropdown"", ""Options"": [""Nul"", ""Faible"", ""Moyen"", ""Fort""], ""Placeholder"": ""Sélectionner le niveau de risque..."" },
                                { ""Name"": ""SafeguardGenderEquity"", ""Label"": ""Le projet garantit-il la conformité en matière d'égalité homme-femme ?"", ""Type"": ""Checkbox"", ""Placeholder"": """" },
                                { ""Name"": ""SafeguardMitigationPlan"", ""Label"": ""Mesures de mitigation et de gestion des sauvegardes"", ""Type"": ""TextLong"", ""Placeholder"": ""Décrivez les mesures de prévention et d'atténuation prévues pour les risques identifiés ci-dessus..."" }
                            ]
                        }
                    ]";

                    var t = new FormTemplate
                    {
                        Name = "Modèle FEM/PMF - Initiative ICCA-GSI",
                        Description = "Formulaire sectoriel avancé pour projets communautaires.",
                        JsonSchema = schemaJson,
                        Version = 1,
                        IsActive = true
                    };
                    db.FormTemplates.Add(t);
                    db.SaveChanges();
                    templates.Add(t);
                }

                AvailableTemplates = new ObservableCollection<FormTemplate>(templates);
                
                if (AvailableTemplates.Count > 0 && _selectedTemplate == null)
                {
                    _selectedTemplate = AvailableTemplates.First();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur chargement modèles : {ex.Message}";
            }
        }

        private void LoadInstanceForProjectAndTemplate()
        {
            if (SelectedProject == null || SelectedTemplate == null) 
            {
                HasInstance = false;
                CurrentInstance = null;
                Answers.Clear();
                OnPropertyChanged(nameof(Answers));
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var instance = db.FormInstances
                    .Include(i => i.FormTemplate)
                    .FirstOrDefault(i => i.ProjectId == SelectedProject.Id && i.FormTemplateId == SelectedTemplate.Id);

                if (instance != null)
                {
                    CurrentInstance = instance;
                    HasInstance = true;
                    
                    Answers = JsonSerializer.Deserialize<Dictionary<string, string>>(instance.JsonAnswers) 
                              ?? new Dictionary<string, string>();

                    StatusMessage = $"Canevas '{instance.FormTemplate?.Name}' chargé pour le projet {SelectedProject.Title}.";
                }
                else
                {
                    HasInstance = false;
                    CurrentInstance = null;
                    Answers.Clear();
                    StatusMessage = $"Canevas '{SelectedTemplate.Name}' prêt. Cliquez sur 'Associer au Projet' pour commencer.";
                }

                OnPropertyChanged(nameof(Answers));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors du chargement : {ex.Message}";
            }
        }

        [RelayCommand]
        public void InstancierFormulaire()
        {
            if (SelectedTemplate == null || SelectedProject == null)
            {
                StatusMessage = "Veuillez sélectionner un projet et un modèle de canevas.";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var existing = db.FormInstances
                    .FirstOrDefault(i => i.ProjectId == SelectedProject.Id && i.FormTemplateId == SelectedTemplate.Id);

                if (existing != null)
                {
                    CurrentInstance = existing;
                    Answers = JsonSerializer.Deserialize<Dictionary<string, string>>(existing.JsonAnswers) 
                              ?? new Dictionary<string, string>();
                    HasInstance = true;
                    OnPropertyChanged(nameof(Answers));
                    StatusMessage = $"Canevas '{SelectedTemplate.Name}' déjà associé à ce projet.";
                    return;
                }

                var newInstance = new FormInstance
                {
                    ProjectId = SelectedProject.Id,
                    FormTemplateId = SelectedTemplate.Id,
                    JsonAnswers = "{}"
                };

                db.FormInstances.Add(newInstance);
                db.SaveChanges();

                newInstance.FormTemplate = SelectedTemplate;
                CurrentInstance = newInstance;
                Answers = new Dictionary<string, string>();
                HasInstance = true;
                OnPropertyChanged(nameof(Answers));
                StatusMessage = $"Canevas '{SelectedTemplate.Name}' associé au projet. Remplissez-le manuellement ou via l'IA.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'association : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SauvegarderReponses()
        {
            if (CurrentInstance == null) return;

            try
            {
                using var db = new AppDbContext();
                string jsonAnswers = JsonSerializer.Serialize(Answers);

                var dbInstance = db.FormInstances.FirstOrDefault(i => i.Id == CurrentInstance.Id);
                if (dbInstance != null)
                {
                    dbInstance.JsonAnswers = jsonAnswers;
                    dbInstance.LastUpdatedAt = DateTime.UtcNow;
                    db.SaveChanges();

                    CurrentInstance.JsonAnswers = jsonAnswers;
                    StatusMessage = "Les réponses du canevas ont été enregistrées avec succès !";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'enregistrement : {ex.Message}";
            }
        }

        [RelayCommand]
        public void SupprimerFormulaire()
        {
            if (CurrentInstance == null) return;

            try
            {
                using var db = new AppDbContext();
                var dbInstance = db.FormInstances.FirstOrDefault(i => i.Id == CurrentInstance.Id);
                if (dbInstance != null)
                {
                    db.FormInstances.Remove(dbInstance);
                    db.SaveChanges();
                }

                CurrentInstance = null;
                Answers.Clear();
                HasInstance = false;
                OnPropertyChanged(nameof(Answers));
                StatusMessage = "Canevas dissocié du projet.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la dissociation : {ex.Message}";
            }
        }

        #region Import & Designer Commands

        [RelayCommand]
        public async Task ImporterCanvas()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Importer un canevas bailleur (Word, PDF, Excel)",
                Filter = "Tous documents supportés (*.docx;*.pdf;*.xlsx)|*.docx;*.pdf;*.xlsx|Document Word (*.docx)|*.docx|Document PDF (*.pdf)|*.pdf|Classeur Excel (*.xlsx)|*.xlsx|Tous les fichiers (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() != true) return;

            IsBusy = true;
            StatusMessage = "Analyse et importation du canevas en cours...";

            try
            {
                var newTemplate = await CanvasService.ImportCanvasFromFileAsync(ofd.FileName);
                AvailableTemplates.Add(newTemplate);
                SelectedTemplate = newTemplate;

                if (SelectedProject != null)
                {
                    InstancierFormulaire();
                }

                StatusMessage = $"Canevas '{newTemplate.Name}' importé et analysé avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'importation du canevas : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void CreerCanvasManuel()
        {
            try
            {
                var designerVm = new CanvasDesignerViewModel();
                var designerWin = new CanvasDesignerWindow(designerVm)
                {
                    Owner = Application.Current?.MainWindow
                };

                if (designerWin.ShowDialog() == true && designerVm.CreatedTemplate != null)
                {
                    AvailableTemplates.Add(designerVm.CreatedTemplate);
                    SelectedTemplate = designerVm.CreatedTemplate;

                    if (SelectedProject != null)
                    {
                        InstancierFormulaire();
                    }

                    StatusMessage = $"Nouveau canevas '{designerVm.CreatedTemplate.Name}' créé avec succès !";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la création manuelle : {ex.Message}";
            }
        }

        #endregion

        #region Export Commands (Word, PDF, Excel)

        [RelayCommand]
        public void ExporterWord()
        {
            if (SelectedTemplate == null)
            {
                StatusMessage = "Veuillez sélectionner un canevas à exporter.";
                return;
            }

            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Exporter le canevas en Word (.docx)",
                    Filter = "Document Word (*.docx)|*.docx",
                    FileName = $"{SanitizeFileName(SelectedTemplate.Name)}_{SanitizeFileName(SelectedProject?.Title ?? "Canevas")}.docx"
                };

                if (sfd.ShowDialog() == true)
                {
                    CanvasService.ExportCanvasToDocx(SelectedTemplate, Answers, SelectedProject, sfd.FileName);
                    StatusMessage = $"Canevas exporté en Word avec succès : {Path.GetFileName(sfd.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'exportation Word : {ex.Message}";
            }
        }

        [RelayCommand]
        public void ExporterPdf()
        {
            if (SelectedTemplate == null)
            {
                StatusMessage = "Veuillez sélectionner un canevas à exporter.";
                return;
            }

            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Exporter le canevas en PDF (.pdf)",
                    Filter = "Document PDF (*.pdf)|*.pdf",
                    FileName = $"{SanitizeFileName(SelectedTemplate.Name)}_{SanitizeFileName(SelectedProject?.Title ?? "Canevas")}.pdf"
                };

                if (sfd.ShowDialog() == true)
                {
                    CanvasService.ExportCanvasToPdf(SelectedTemplate, Answers, SelectedProject, sfd.FileName);
                    StatusMessage = $"Canevas exporté en PDF avec succès : {Path.GetFileName(sfd.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'exportation PDF : {ex.Message}";
            }
        }

        [RelayCommand]
        public void ExporterExcel()
        {
            if (SelectedTemplate == null)
            {
                StatusMessage = "Veuillez sélectionner un canevas à exporter.";
                return;
            }

            try
            {
                var sfd = new SaveFileDialog
                {
                    Title = "Exporter le canevas en Excel (.xlsx)",
                    Filter = "Classeur Excel (*.xlsx)|*.xlsx",
                    FileName = $"{SanitizeFileName(SelectedTemplate.Name)}_{SanitizeFileName(SelectedProject?.Title ?? "Canevas")}.xlsx"
                };

                if (sfd.ShowDialog() == true)
                {
                    CanvasService.ExportCanvasToExcel(SelectedTemplate, Answers, SelectedProject, sfd.FileName);
                    StatusMessage = $"Canevas exporté en Excel avec succès : {Path.GetFileName(sfd.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'exportation Excel : {ex.Message}";
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var clean = string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return string.IsNullOrWhiteSpace(clean) ? "Canevas" : clean;
        }

        #endregion

        #region AI Filling & Assistance

        [RelayCommand]
        public async Task RemplirAutomatiquement()
        {
            if (SelectedProject == null || CurrentInstance == null || SelectedTemplate == null) return;
            
            IsBusy = true;
            StatusMessage = "Analyse du projet et pré-remplissage complet du canevas par l'IA...";
            
            try
            {
                using var db = new AppDbContext();
                var fullProject = db.Projects
                    .Include(p => p.Objectives).ThenInclude(o => o.Results).ThenInclude(r => r.Activities)
                    .Include(p => p.BudgetItems)
                    .FirstOrDefault(p => p.Id == SelectedProject.Id);
                    
                if (fullProject == null) return;

                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine($"PROJET : {fullProject.Title}");
                if (!string.IsNullOrWhiteSpace(fullProject.LeadOrganization))
                    contextBuilder.AppendLine($"Organisation porteuse : {fullProject.LeadOrganization}");
                if (!string.IsNullOrWhiteSpace(fullProject.InterventionZone))
                    contextBuilder.AppendLine($"Zone d'intervention : {fullProject.InterventionZone}");
                if (fullProject.TotalBudget > 0)
                    contextBuilder.AppendLine($"Budget Total : {fullProject.TotalBudget:N2} {fullProject.Currency} (Local: {fullProject.LocalContribution:N2}, Bailleurs/Partenaires: {fullProject.PartnerContribution:N2})");
                
                contextBuilder.AppendLine($"\nOBJECTIF GÉNÉRAL :\n{fullProject.GeneralObjective}");
                contextBuilder.AppendLine($"\nCONTEXTE & JUSTIFICATION :\n{fullProject.Justification}");
                if (!string.IsNullOrWhiteSpace(fullProject.ProjectApproach))
                    contextBuilder.AppendLine($"\nAPPROCHE MÉTHODOLOGIQUE :\n{fullProject.ProjectApproach}");
                if (!string.IsNullOrWhiteSpace(fullProject.ProjectImpacts))
                    contextBuilder.AppendLine($"\nIMPACTS ATTENDUS :\n{fullProject.ProjectImpacts}");
                if (!string.IsNullOrWhiteSpace(fullProject.Sustainability))
                    contextBuilder.AppendLine($"\nPÉRENNITÉ :\n{fullProject.Sustainability}");
                if (!string.IsNullOrWhiteSpace(fullProject.StrategicAlignment))
                    contextBuilder.AppendLine($"\nALIGNEMENT STRATÉGIQUE :\n{fullProject.StrategicAlignment}");
                if (!string.IsNullOrWhiteSpace(fullProject.OrganizationalStrengths))
                    contextBuilder.AppendLine($"\nCAPACITÉS DE L'ORGANISATION :\n{fullProject.OrganizationalStrengths}");

                if (!string.IsNullOrWhiteSpace(fullProject.SwotStrengths) || !string.IsNullOrWhiteSpace(fullProject.SwotWeaknesses))
                {
                    contextBuilder.AppendLine("\nANALYSE SWOT / RISQUES :");
                    if (!string.IsNullOrWhiteSpace(fullProject.SwotStrengths)) contextBuilder.AppendLine($"Forces : {fullProject.SwotStrengths}");
                    if (!string.IsNullOrWhiteSpace(fullProject.SwotWeaknesses)) contextBuilder.AppendLine($"Faiblesses : {fullProject.SwotWeaknesses}");
                    if (!string.IsNullOrWhiteSpace(fullProject.SwotOpportunities)) contextBuilder.AppendLine($"Opportunités : {fullProject.SwotOpportunities}");
                    if (!string.IsNullOrWhiteSpace(fullProject.SwotThreats)) contextBuilder.AppendLine($"Menaces : {fullProject.SwotThreats}");
                }

                if (!string.IsNullOrWhiteSpace(fullProject.GlobalRequirementsSummary))
                    contextBuilder.AppendLine($"\nSYNTHÈSE DES DOCUMENTS D'APPUI :\n{fullProject.GlobalRequirementsSummary}");

                if (fullProject.Objectives.Count > 0)
                {
                    contextBuilder.AppendLine("\nCADRE LOGIQUE DÉTAILLÉ :");
                    foreach (var os in fullProject.Objectives)
                    {
                        contextBuilder.AppendLine($"- Objectif Spécifique : {os.Description}");
                        foreach (var res in os.Results)
                        {
                            contextBuilder.AppendLine($"   * Résultat Attendu : {res.Description}");
                            foreach (var act in res.Activities)
                            {
                                contextBuilder.AppendLine($"      • Activité : {act.Description}");
                            }
                        }
                    }
                }

                string context = contextBuilder.ToString();
                
                string prompt = $"Voici les informations complètes du projet :\n{context}\n\n" +
                                $"Voici le schéma JSON du formulaire/canevas bailleur à remplir :\n{SelectedTemplate.JsonSchema}\n\n" +
                                "Instructions capitales :\n" +
                                "1. Analyse attentivement les questions et exigences du canevas.\n" +
                                "2. Pour CHAQUE question du canevas (identifiée par 'Name'), formule une réponse complète, rigoureuse et professionnelle basée sur les données du projet ci-dessus.\n" +
                                "3. Pour les types 'Dropdown', sélectionne l'une des options existantes dans la liste Options.\n" +
                                "4. Pour les types 'Checkbox', utilise 'True' ou 'False'.\n" +
                                "5. Pour les types 'TextLong' et 'TextShort', rédige des réponses détaillées, de haute qualité méthodologique répondant spécifiquement aux standards des bailleurs de fonds.\n" +
                                "6. Renvoie UNIQUEMENT un objet JSON valide (SANS BALISES MARKDOWN, COMMENCE DIRECTEMENT PAR {) où les clés sont les valeurs exactes de 'Name' et les valeurs sont tes réponses rédigées.";
                                
                string result = await AiAssistantService.CallAiAsync(prompt, "Tu es un consultant international senior expert en montage de projets et dossiers de financement pour les bailleurs de fonds internationaux.");
                
                var newAnswers = ExtractAnswersFromJson(result, SelectedTemplate);
                if (newAnswers != null && newAnswers.Count > 0)
                {
                    foreach (var kvp in newAnswers)
                    {
                        Answers[kvp.Key] = kvp.Value;
                    }
                    OnPropertyChanged(nameof(Answers));
                    StatusMessage = $"✨ Canevas pré-rempli par l'IA avec succès ({newAnswers.Count} rubriques complétées) ! Vérifiez et enregistrez.";
                }
                else
                {
                    StatusMessage = "Attention : L'IA n'a retourné aucune réponse structurée exploitable. Veuillez réessayer.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors du remplissage auto : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task AideRedactionIa(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName) || SelectedProject == null || SelectedTemplate == null) return;

            Answers.TryGetValue(fieldName, out var currentText);
            currentText ??= string.Empty;

            string fieldLabel = fieldName;
            try
            {
                var sections = CanvasService.ParseSchema(SelectedTemplate.JsonSchema);
                foreach (var sec in sections)
                {
                    var f = sec.Fields.FirstOrDefault(x => x.Name == fieldName);
                    if (f != null)
                    {
                        fieldLabel = $"[Section: {sec.Section}] - Question : {f.Label}";
                        break;
                    }
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(FormulaireDynamiqueViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }

            IsBusy = true;
            StatusMessage = $"L'Agent IA rédige la réponse pour la question...";

            try
            {
                using var db = new AppDbContext();
                var fullProject = db.Projects
                    .Include(p => p.Objectives).ThenInclude(o => o.Results)
                    .FirstOrDefault(p => p.Id == SelectedProject.Id);

                string projContext = fullProject != null 
                    ? $"Projet: {fullProject.Title}\nObjectif Général: {fullProject.GeneralObjective}\nJustification: {fullProject.Justification}\nZone: {fullProject.InterventionZone}"
                    : "";

                string prompt = $"Voici le contexte du projet :\n{projContext}\n\n" +
                                $"Tu dois rédiger ou enrichir la réponse à cette exigence du bailleur :\n{fieldLabel}\n\n" +
                                (string.IsNullOrWhiteSpace(currentText) 
                                    ? "Génère une réponse complète, très détaillée, persuasive et professionnelle respectant les normes de formulation de projet bailleur." 
                                    : $"Voici l'ébauche actuelle :\n\"{currentText}\"\n\nAméliore, enrichis et professionnalise ce texte pour qu'il soit percutant et complet.") +
                                "\n\nRenvoie uniquement la réponse finale rédigée, sans formule de politesse, sans introduction et sans balises markdown.";

                string systemInstruction = "Tu es un rédacteur d'ONG senior spécialisé dans les financements climatiques, environnementaux et de développement communautaire.";

                string result = await AiAssistantService.CallAiAsync(prompt, systemInstruction);

                Answers[fieldName] = CleanTextAnswer(result);
                OnPropertyChanged(nameof(Answers));
                StatusMessage = "✨ Réponse rédigée/enrichie par l'IA ! N'oubliez pas d'enregistrer.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'assistance IA : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion

        #region Helpers d'extraction JSON & Texte IA

        public static Dictionary<string, string> ExtractAnswersFromJson(string rawText, FormTemplate? template = null)
        {
            var resultDict = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(rawText)) return resultDict;

            string cleaned = rawText.Trim();

            // 1. Extraire depuis un bloc de code markdown si présent
            var matchMarkdown = System.Text.RegularExpressions.Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matchMarkdown.Success)
            {
                cleaned = matchMarkdown.Groups[1].Value.Trim();
            }

            // 2. Extraire la zone JSON entre les délimiteurs { } ou [ ]
            int firstBrace = cleaned.IndexOf('{');
            int firstBracket = cleaned.IndexOf('[');

            int startIdx = -1;
            int endIdx = -1;

            if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
            {
                startIdx = firstBrace;
                endIdx = cleaned.LastIndexOf('}');
            }
            else if (firstBracket >= 0)
            {
                startIdx = firstBracket;
                endIdx = cleaned.LastIndexOf(']');
            }

            if (startIdx >= 0 && endIdx > startIdx)
            {
                cleaned = cleaned.Substring(startIdx, endIdx - startIdx + 1);
            }

            // 3. Préparer le dictionnaire de mapping (Name <-> Label)
            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var labelToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (template != null && !string.IsNullOrWhiteSpace(template.JsonSchema))
            {
                try
                {
                    var sections = CanvasService.ParseSchema(template.JsonSchema);
                    foreach (var sec in sections)
                    {
                        foreach (var f in sec.Fields)
                        {
                            fieldNames.Add(f.Name);
                            if (!string.IsNullOrWhiteSpace(f.Label) && !labelToName.ContainsKey(f.Label))
                            {
                                labelToName[f.Label] = f.Name;
                            }
                        }
                    }
                }
                catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(FormulaireDynamiqueViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
            }

            // 4. Parser avec JsonDocument flexible (autorise virgules de fin, commentaires, types variés)
            try
            {
                var docOptions = new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };

                using var doc = JsonDocument.Parse(cleaned, docOptions);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string val = ElementToString(prop.Value);
                        string targetKey = ResolveFieldName(prop.Name, fieldNames, labelToName);
                        resultDict[targetKey] = val;
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            string key = "";
                            string val = "";

                            foreach (var prop in item.EnumerateObject())
                            {
                                string pName = prop.Name.ToLowerInvariant();
                                if (pName is "name" or "field" or "id" or "key" or "question" or "champ")
                                {
                                    key = ElementToString(prop.Value);
                                }
                                else if (pName is "value" or "answer" or "reponse" or "valeur" or "texte")
                                {
                                    val = ElementToString(prop.Value);
                                }
                            }

                            if (!string.IsNullOrEmpty(key))
                            {
                                string targetKey = ResolveFieldName(key, fieldNames, labelToName);
                                resultDict[targetKey] = val;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback par expressions régulières si l'IA produit un JSON avec guillemets non échappés
                var regex = new System.Text.RegularExpressions.Regex(@"""(?<key>[^""]+)""\s*:\s*""(?<val>(?:\\.|[^""\\])*)""");
                var matches = regex.Matches(cleaned);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string key = m.Groups["key"].Value;
                    string val = System.Text.RegularExpressions.Regex.Unescape(m.Groups["val"].Value);
                    string targetKey = ResolveFieldName(key, fieldNames, labelToName);
                    resultDict[targetKey] = val;
                }
            }

            return resultDict;
        }

        private static string ResolveFieldName(string key, HashSet<string> fieldNames, Dictionary<string, string> labelToName)
        {
            if (fieldNames.Contains(key)) return key;
            if (labelToName.TryGetValue(key, out var mappedName)) return mappedName;

            foreach (var kvp in labelToName)
            {
                if (kvp.Key.Contains(key, StringComparison.OrdinalIgnoreCase) || key.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return key;
        }

        private static string ElementToString(JsonElement elem)
        {
            return elem.ValueKind switch
            {
                JsonValueKind.String => elem.GetString() ?? "",
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                JsonValueKind.Number => elem.GetRawText(),
                JsonValueKind.Array => string.Join(", ", elem.EnumerateArray().Select(ElementToString)),
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                _ => elem.GetRawText()
            };
        }

        private static string CleanTextAnswer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string t = raw.Trim();
            if (t.StartsWith("```") && t.EndsWith("```"))
            {
                var lines = t.Split('\n');
                if (lines.Length >= 2)
                {
                    t = string.Join("\n", lines.Skip(1).Take(lines.Length - 2));
                }
            }
            if (t.StartsWith("\"") && t.EndsWith("\"") && t.Length >= 2)
            {
                t = t.Substring(1, t.Length - 2);
            }
            return t.Trim();
        }

        #endregion
    }
}

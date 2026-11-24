using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Radar;
using AFMI_Montage_projet_2.Views;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class OrganizationProfileViewModel : ObservableObject
    {
        private readonly Project? _activeProject;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private string _selectedDonor = "Union Européenne";

        public List<string> AvailableDonors { get; } = new()
        {
            "Union Européenne",
            "AFD (Agence Française de Développement)",
            "PNUD / FEM (GEF)",
            "Banque Mondiale",
            "USAID",
            "CEPF / UICN",
            "FAO",
            "Général / Multilatéral"
        };

        #region Form Properties

        // 1. Identité légale
        [ObservableProperty] private string _organizationName = string.Empty;
        [ObservableProperty] private string _acronym = string.Empty;
        [ObservableProperty] private string _creationDate = string.Empty;
        [ObservableProperty] private string _legalStatus = string.Empty;
        [ObservableProperty] private string _registrationNumber = string.Empty;
        [ObservableProperty] private string _headquarters = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _website = string.Empty;
        [ObservableProperty] private string _facebookUrl = string.Empty;
        [ObservableProperty] private string _linkedInUrl = string.Empty;
        [ObservableProperty] private string _twitterUrl = string.Empty;
        [ObservableProperty] private string _instagramUrl = string.Empty;
        [ObservableProperty] private string _youTubeUrl = string.Empty;
        [ObservableProperty] private string _notebookLmUrl = NotebookLmService.DefaultNotebookLmUrl;
        [ObservableProperty] private string _legalRepresentative = string.Empty;

        // Historique des sources analysées (fichiers, URLs)
        [ObservableProperty] private ObservableCollection<string> _analyzedSourcesHistory = new();

        // 2. Vision & Mission
        [ObservableProperty] private string _mission = string.Empty;
        [ObservableProperty] private string _vision = string.Empty;
        [ObservableProperty] private string _coreValues = string.Empty;
        [ObservableProperty] private string _strategicAxes = string.Empty;
        [ObservableProperty] private string _strategicObjectives = string.Empty;
        [ObservableProperty] private string _geographicZonesText = string.Empty;
        [ObservableProperty] private string _preferredThemesText = string.Empty;

        // 3. Trajectoire & Historique
        [ObservableProperty] private string _historyTrajectory = string.Empty;

        // 4. Réalisations & Bilan d'impact
        [ObservableProperty] private string _keyAchievements = string.Empty;
        [ObservableProperty] private string _impactFigures = string.Empty;
        [ObservableProperty] private string _recognitionsAwards = string.Empty;

        // 5. Capacités organisationnelles
        [ObservableProperty] private string _humanResourcesCapacity = string.Empty;
        [ObservableProperty] private string _financialManagementCapacity = string.Empty;
        [ObservableProperty] private string _logisticalMaterialCapacity = string.Empty;

        // 6. Expérience Bailleurs
        [ObservableProperty] private string _preferredDonorsText = string.Empty;
        [ObservableProperty] private string _pastProjectsReferences = string.Empty;
        [ObservableProperty] private string _strategicPartnerships = string.Empty;
        [ObservableProperty] private string _technicalExpertiseSummary = string.Empty;

        // 7. Déclaration de capacité
        [ObservableProperty] private string _donorCapacityStatement = string.Empty;

        #endregion

        public bool HasActiveProject => _activeProject != null;
        public string ActiveProjectTitle => _activeProject?.Title ?? string.Empty;

        public OrganizationProfileViewModel(Project? activeProject = null)
        {
            _activeProject = activeProject;
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            try
            {
                var profile = ProjectRadarConfigService.LoadProfile();
                ApplyProfileToProperties(profile);
                StatusMessage = "Profil institutionnel chargé avec succès.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors du chargement : {ex.Message}";
            }
        }

        private void ApplyProfileToProperties(AfmiProfile p)
        {
            OrganizationName = p.OrganizationName ?? string.Empty;
            Acronym = p.Acronym ?? string.Empty;
            CreationDate = p.CreationDate ?? string.Empty;
            LegalStatus = p.LegalStatus ?? string.Empty;
            RegistrationNumber = p.RegistrationNumber ?? string.Empty;
            Headquarters = p.Headquarters ?? string.Empty;
            Address = p.Address ?? string.Empty;
            Phone = p.Phone ?? string.Empty;
            Email = p.Email ?? string.Empty;
            Website = p.Website ?? string.Empty;
            FacebookUrl = p.FacebookUrl ?? string.Empty;
            LinkedInUrl = p.LinkedInUrl ?? string.Empty;
            TwitterUrl = p.TwitterUrl ?? string.Empty;
            InstagramUrl = p.InstagramUrl ?? string.Empty;
            YouTubeUrl = p.YouTubeUrl ?? string.Empty;
            NotebookLmUrl = string.IsNullOrWhiteSpace(p.NotebookLmUrl) ? NotebookLmService.DefaultNotebookLmUrl : p.NotebookLmUrl;
            AnalyzedSourcesHistory = new ObservableCollection<string>(p.AnalyzedSourcesHistory ?? new List<string>());
            LegalRepresentative = p.LegalRepresentative ?? string.Empty;

            Mission = p.Mission ?? string.Empty;
            Vision = p.Vision ?? string.Empty;
            CoreValues = p.CoreValues ?? string.Empty;
            StrategicAxes = string.IsNullOrWhiteSpace(p.StrategicAxes) ? new AfmiProfile().StrategicAxes : p.StrategicAxes;
            StrategicObjectives = p.StrategicObjectives ?? string.Empty;
            GeographicZonesText = string.Join("\n", p.GeographicZones ?? new List<string>());
            PreferredThemesText = string.Join("\n", p.PreferredThemes ?? new List<string>());

            HistoryTrajectory = p.HistoryTrajectory ?? string.Empty;
            KeyAchievements = p.KeyAchievements ?? string.Empty;
            ImpactFigures = p.ImpactFigures ?? string.Empty;
            RecognitionsAwards = p.RecognitionsAwards ?? string.Empty;

            HumanResourcesCapacity = p.HumanResourcesCapacity ?? string.Empty;
            FinancialManagementCapacity = p.FinancialManagementCapacity ?? string.Empty;
            LogisticalMaterialCapacity = p.LogisticalMaterialCapacity ?? string.Empty;

            PreferredDonorsText = string.Join("\n", p.PreferredDonors ?? new List<string>());
            PastProjectsReferences = p.PastProjectsReferences ?? string.Empty;
            StrategicPartnerships = p.StrategicPartnerships ?? string.Empty;
            TechnicalExpertiseSummary = p.TechnicalExpertiseSummary ?? string.Empty;

            DonorCapacityStatement = p.DonorCapacityStatement ?? string.Empty;
        }

        public AfmiProfile BuildProfileFromProperties()
        {
            return new AfmiProfile
            {
                OrganizationName = OrganizationName.Trim(),
                Acronym = Acronym.Trim(),
                CreationDate = CreationDate.Trim(),
                LegalStatus = LegalStatus.Trim(),
                RegistrationNumber = RegistrationNumber.Trim(),
                Headquarters = Headquarters.Trim(),
                Address = Address.Trim(),
                Phone = Phone.Trim(),
                Email = Email.Trim(),
                Website = Website.Trim(),
                FacebookUrl = FacebookUrl.Trim(),
                LinkedInUrl = LinkedInUrl.Trim(),
                TwitterUrl = TwitterUrl.Trim(),
                InstagramUrl = InstagramUrl.Trim(),
                YouTubeUrl = YouTubeUrl.Trim(),
                NotebookLmUrl = string.IsNullOrWhiteSpace(NotebookLmUrl) ? NotebookLmService.DefaultNotebookLmUrl : NotebookLmUrl.Trim(),
                AnalyzedSourcesHistory = AnalyzedSourcesHistory.ToList(),
                LegalRepresentative = LegalRepresentative.Trim(),

                Mission = Mission.Trim(),
                Vision = Vision.Trim(),
                CoreValues = CoreValues.Trim(),
                StrategicAxes = StrategicAxes.Trim(),
                StrategicObjectives = StrategicObjectives.Trim(),
                GeographicZones = GeographicZonesText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList(),
                PreferredThemes = PreferredThemesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList(),

                HistoryTrajectory = HistoryTrajectory.Trim(),
                KeyAchievements = KeyAchievements.Trim(),
                ImpactFigures = ImpactFigures.Trim(),
                RecognitionsAwards = RecognitionsAwards.Trim(),

                HumanResourcesCapacity = HumanResourcesCapacity.Trim(),
                FinancialManagementCapacity = FinancialManagementCapacity.Trim(),
                LogisticalMaterialCapacity = LogisticalMaterialCapacity.Trim(),

                PreferredDonors = PreferredDonorsText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList(),
                PastProjectsReferences = PastProjectsReferences.Trim(),
                StrategicPartnerships = StrategicPartnerships.Trim(),
                TechnicalExpertiseSummary = TechnicalExpertiseSummary.Trim(),

                DonorCapacityStatement = DonorCapacityStatement.Trim()
            };
        }

        private static void ShowUserMessage(string message, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            if (Application.Current != null)
            {
                MessageBox.Show(message, caption, button, image);
            }
        }

        [RelayCommand]
        public void Save()
        {
            try
            {
                var profile = BuildProfileFromProperties();
                ProjectRadarConfigService.SaveProfile(profile);
                StatusMessage = "✅ Profil de l'organisation sauvegardé avec succès dans afmi_profile.json.";
                ShowUserMessage("Les informations et capacités de l'organisation ont été enregistrées avec succès.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Erreur lors de l'enregistrement : {ex.Message}";
                ShowUserMessage($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void Reload()
        {
            LoadFromConfig();
        }

        [RelayCommand]
        public void ResetDefault()
        {
            var res = MessageBox.Show("Voulez-vous réinitialiser le profil aux valeurs modèles recommandées pour AFMI ?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                var defaultProfile = new AfmiProfile();
                ApplyProfileToProperties(defaultProfile);
                StatusMessage = "Profil réinitialisé aux valeurs par défaut d'AFMI.";
            }
        }

        [RelayCommand]
        public async Task AuditProfileIaAsync()
        {
            IsBusy = true;
            StatusMessage = "Diagnostic et audit institutionnel en cours par l'IA...";
            try
            {
                var profile = BuildProfileFromProperties();
                string auditResult = await OrganizationAiService.AuditProfileAsync(profile);
                StatusMessage = "Audit institutionnel terminé.";

                var auditDialog = new AiRiskAuditDialog(auditResult, $"Rapport de Diagnostic Institutionnel — {profile.OrganizationName}");
                auditDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'audit : {ex.Message}";
                ShowUserMessage($"Erreur lors de l'audit IA : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task GenerateCapacityStatementIaAsync()
        {
            IsBusy = true;
            StatusMessage = $"Génération de la Déclaration de Capacité IA pour '{SelectedDonor}'...";
            try
            {
                var profile = BuildProfileFromProperties();
                string statement = await OrganizationAiService.GenerateDonorCapacityStatementAsync(profile, SelectedDonor);
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    DonorCapacityStatement = statement;
                    StatusMessage = $"✅ Déclaration de capacité générée avec succès pour '{SelectedDonor}'.";
                    SelectedTabIndex = 6; // Passer sur l'onglet Déclaration de capacité
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur de génération : {ex.Message}";
                ShowUserMessage($"Erreur lors de la génération IA : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task EnrichSectionIaAsync(string sectionName)
        {
            IsBusy = true;
            StatusMessage = $"Enrichissement IA de la section '{sectionName}'...";
            try
            {
                var profile = BuildProfileFromProperties();
                string currentContent = sectionName switch
                {
                    "Mission" => Mission,
                    "Vision" => Vision,
                    "Valeurs" => CoreValues,
                    "Axes" => StrategicAxes,
                    "StrategicAxes" => StrategicAxes,
                    "Objectifs" => StrategicObjectives,
                    "Historique" => HistoryTrajectory,
                    "Réalisations" => KeyAchievements,
                    "RessourcesHumaines" => HumanResourcesCapacity,
                    "GestionFinanciere" => FinancialManagementCapacity,
                    "MoyensMateriels" => LogisticalMaterialCapacity,
                    "Partenariats" => StrategicPartnerships,
                    "Expertise" => TechnicalExpertiseSummary,
                    _ => string.Empty
                };

                string enriched = await OrganizationAiService.EnrichSectionAsync(sectionName, currentContent, profile);
                if (!string.IsNullOrWhiteSpace(enriched))
                {
                    switch (sectionName)
                    {
                        case "Mission": Mission = enriched; break;
                        case "Vision": Vision = enriched; break;
                        case "Valeurs": CoreValues = enriched; break;
                        case "Axes":
                        case "StrategicAxes": StrategicAxes = enriched; break;
                        case "Objectifs": StrategicObjectives = enriched; break;
                        case "Historique": HistoryTrajectory = enriched; break;
                        case "Réalisations": KeyAchievements = enriched; break;
                        case "RessourcesHumaines": HumanResourcesCapacity = enriched; break;
                        case "GestionFinanciere": FinancialManagementCapacity = enriched; break;
                        case "MoyensMateriels": LogisticalMaterialCapacity = enriched; break;
                        case "Partenariats": StrategicPartnerships = enriched; break;
                        case "Expertise": TechnicalExpertiseSummary = enriched; break;
                    }
                    StatusMessage = $"✅ Section '{sectionName}' enrichie par l'IA.";
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
        public void CopyCapacityStatement()
        {
            if (!string.IsNullOrWhiteSpace(DonorCapacityStatement))
            {
                Clipboard.SetText(DonorCapacityStatement);
                StatusMessage = "Déclaration de capacité copiée dans le presse-papier.";
            }
        }

        [RelayCommand]
        public void SyncToActiveProject()
        {
            if (_activeProject == null)
            {
                ShowUserMessage("Aucun projet actif sélectionné. Ouvrez un projet pour y injecter ces informations.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var proj = db.Projects.FirstOrDefault(p => p.Id == _activeProject.Id);
                if (proj != null)
                {
                    proj.LeadOrganization = OrganizationName;
                    var zones = GeographicZonesText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(z => z.Trim()).Where(z => !string.IsNullOrEmpty(z)).ToList();
                    if (zones.Any())
                    {
                        proj.InterventionZone = string.Join(", ", zones);
                    }

                    // Compiler les forces organisationnelles
                    proj.OrganizationalStrengths = $"{OrganizationName} ({Acronym}) — Créée en {CreationDate}.\n\n" +
                        $"• Statut & Gouvernance : {LegalStatus} ({RegistrationNumber}).\n\n" +
                        $"• Mission & Ancrage : {Mission}\n\n" +
                        $"• Réalisations & Bilan : {KeyAchievements}\n\n" +
                        $"• Capacité financière & fiducière : {FinancialManagementCapacity}\n\n" +
                        $"• Équipe & Moyens : {HumanResourcesCapacity} | {LogisticalMaterialCapacity}\n\n" +
                        $"• Expérience Bailleurs : {PastProjectsReferences}";

                    db.SaveChanges();

                    // Mettre à jour l'instance locale
                    _activeProject.LeadOrganization = proj.LeadOrganization;
                    _activeProject.InterventionZone = proj.InterventionZone;
                    _activeProject.OrganizationalStrengths = proj.OrganizationalStrengths;

                    StatusMessage = $"✅ Données institutionnelles injectées avec succès dans le projet '{_activeProject.Title}'.";
                    ShowUserMessage($"Les informations de l'organisation ont été injectées dans le projet '{_activeProject.Title}'.\n- Porteur de projet : {proj.LeadOrganization}\n- Zone d'intervention : {proj.InterventionZone}\n- Forces organisationnelles actualisées.", "Synchronisation Réussie", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la synchronisation : {ex.Message}";
                ShowUserMessage($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ExportWord()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Document Word (*.docx)|*.docx",
                    FileName = $"Fiche_Institutionnelle_{Acronym.Replace(" ", "_")}.docx",
                    Title = "Exporter la Fiche Institutionnelle en Word"
                };

                if (sfd.ShowDialog() == true)
                {
                    var profile = BuildProfileFromProperties();
                    DocumentExportService.ExportOrganizationProfileToWord(sfd.FileName, profile);
                    StatusMessage = $"✅ Fiche Word exportée avec succès : {Path.GetFileName(sfd.FileName)}";
                    ShowUserMessage("Fiche institutionnelle exportée en Word avec succès.", "Export Réussi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'export Word : {ex.Message}";
                ShowUserMessage($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void ExportPdf()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Document PDF (*.pdf)|*.pdf",
                    FileName = $"Fiche_Institutionnelle_{Acronym.Replace(" ", "_")}.pdf",
                    Title = "Exporter la Fiche Institutionnelle en PDF"
                };

                if (sfd.ShowDialog() == true)
                {
                    var profile = BuildProfileFromProperties();
                    DocumentExportService.ExportOrganizationProfileToPdf(sfd.FileName, profile);
                    StatusMessage = $"✅ Fiche PDF exportée avec succès : {Path.GetFileName(sfd.FileName)}";
                    ShowUserMessage("Fiche institutionnelle exportée en PDF avec succès.", "Export Réussi", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de l'export PDF : {ex.Message}";
                ShowUserMessage($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void OpenAxesConfigDialog()
        {
            var dialog = new Views.Radar.AfmiAxesConfigDialog
            {
                Owner = Application.Current?.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                StatusMessage = "✅ 5 Axes Stratégiques AFMI synchronisés pour le Radar IA.";
            }
        }

        #region Analyse de Documents & Liens Web / Réseaux Sociaux

        [RelayCommand]
        public void OpenSourceAnalysis()
        {
            var dialog = new ProfileSourceAnalysisDialog(this)
            {
                Owner = Application.Current?.MainWindow
            };
            dialog.ShowDialog();
        }

        public void ApplyProposal(ProfileEnrichmentProposal p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.SuggestedValue)) return;

            string Combine(string current, string addition, EnrichmentMergeMode mode)
            {
                if (mode == EnrichmentMergeMode.Replace || string.IsNullOrWhiteSpace(current))
                    return addition.Trim();
                if (mode == EnrichmentMergeMode.Prepend)
                    return $"{addition.Trim()}\n{current.Trim()}";
                return $"{current.Trim()}\n{addition.Trim()}";
            }

            switch (p.FieldName)
            {
                case "OrganizationName": OrganizationName = p.SuggestedValue.Trim(); break;
                case "Acronym": Acronym = p.SuggestedValue.Trim(); break;
                case "CreationDate": CreationDate = p.SuggestedValue.Trim(); break;
                case "LegalStatus": LegalStatus = p.SuggestedValue.Trim(); break;
                case "RegistrationNumber": RegistrationNumber = p.SuggestedValue.Trim(); break;
                case "Headquarters": Headquarters = p.SuggestedValue.Trim(); break;
                case "Address": Address = p.SuggestedValue.Trim(); break;
                case "Phone": Phone = p.SuggestedValue.Trim(); break;
                case "Email": Email = p.SuggestedValue.Trim(); break;
                case "Website": Website = p.SuggestedValue.Trim(); break;
                case "FacebookUrl": FacebookUrl = p.SuggestedValue.Trim(); break;
                case "LinkedInUrl": LinkedInUrl = p.SuggestedValue.Trim(); break;
                case "TwitterUrl": TwitterUrl = p.SuggestedValue.Trim(); break;
                case "InstagramUrl": InstagramUrl = p.SuggestedValue.Trim(); break;
                case "YouTubeUrl": YouTubeUrl = p.SuggestedValue.Trim(); break;
                case "LegalRepresentative": LegalRepresentative = p.SuggestedValue.Trim(); break;

                case "Mission": Mission = Combine(Mission, p.SuggestedValue, p.MergeMode); break;
                case "Vision": Vision = Combine(Vision, p.SuggestedValue, p.MergeMode); break;
                case "CoreValues": CoreValues = Combine(CoreValues, p.SuggestedValue, p.MergeMode); break;
                case "StrategicAxes": StrategicAxes = Combine(StrategicAxes, p.SuggestedValue, p.MergeMode); break;
                case "StrategicObjectives": StrategicObjectives = Combine(StrategicObjectives, p.SuggestedValue, p.MergeMode); break;

                case "HistoryTrajectory": HistoryTrajectory = Combine(HistoryTrajectory, p.SuggestedValue, p.MergeMode); break;
                case "KeyAchievements": KeyAchievements = Combine(KeyAchievements, p.SuggestedValue, p.MergeMode); break;
                case "ImpactFigures": ImpactFigures = Combine(ImpactFigures, p.SuggestedValue, p.MergeMode); break;
                case "RecognitionsAwards": RecognitionsAwards = Combine(RecognitionsAwards, p.SuggestedValue, p.MergeMode); break;

                case "HumanResourcesCapacity": HumanResourcesCapacity = Combine(HumanResourcesCapacity, p.SuggestedValue, p.MergeMode); break;
                case "FinancialManagementCapacity": FinancialManagementCapacity = Combine(FinancialManagementCapacity, p.SuggestedValue, p.MergeMode); break;
                case "LogisticalMaterialCapacity": LogisticalMaterialCapacity = Combine(LogisticalMaterialCapacity, p.SuggestedValue, p.MergeMode); break;

                case "PastProjectsReferences": PastProjectsReferences = Combine(PastProjectsReferences, p.SuggestedValue, p.MergeMode); break;
                case "StrategicPartnerships": StrategicPartnerships = Combine(StrategicPartnerships, p.SuggestedValue, p.MergeMode); break;
                case "TechnicalExpertiseSummary": TechnicalExpertiseSummary = Combine(TechnicalExpertiseSummary, p.SuggestedValue, p.MergeMode); break;
                case "DonorCapacityStatement": DonorCapacityStatement = Combine(DonorCapacityStatement, p.SuggestedValue, p.MergeMode); break;
            }
        }

        public void AddAnalyzedSource(string entry)
        {
            if (!string.IsNullOrWhiteSpace(entry) && !AnalyzedSourcesHistory.Contains(entry))
            {
                AnalyzedSourcesHistory.Insert(0, entry);
            }
        }

        #endregion

        #region Liaison Google NotebookLM

        [RelayCommand]
        public void OpenNotebookLmDialog()
        {
            var dialog = new NotebookLmDialog(this)
            {
                Owner = Application.Current?.MainWindow
            };
            dialog.ShowDialog();
        }

        [RelayCommand]
        public void OpenNotebookLmUrl()
        {
            NotebookLmService.OpenNotebookLm(NotebookLmUrl);
        }

        [RelayCommand]
        public void CopyNotebookLmSource()
        {
            var profile = BuildProfileFromProperties();
            string markdown = NotebookLmService.GenerateKnowledgeBaseMarkdown(profile);
            Clipboard.SetText(markdown);
            StatusMessage = "📋 Base de connaissances copiée pour NotebookLM (Texte copié).";
            ShowUserMessage("Le document source complet pour NotebookLM a été copié dans le presse-papier.\n\nDans NotebookLM : Cliquez sur '+ Ajouter des sources' > 'Texte copié' et collez !", "Copié avec succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public void ExportNotebookLmSource()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Exporter le Pack Source pour NotebookLM",
                Filter = "Fichier Markdown (*.md)|*.md|Fichier Texte (*.txt)|*.txt|Fichier PDF (*.pdf)|*.pdf",
                FileName = $"AFMI_Dossier_NotebookLM_{DateTime.Now:yyyyMMdd}.md"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var profile = BuildProfileFromProperties();
                    NotebookLmService.ExportKnowledgeBase(sfd.FileName, profile);
                    StatusMessage = $"✅ Pack NotebookLM exporté : {Path.GetFileName(sfd.FileName)}";
                    ShowUserMessage($"Dossier source exporté avec succès dans :\n{sfd.FileName}", "Export NotebookLM", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur d'export : {ex.Message}";
                    ShowUserMessage($"Erreur lors de l'export : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}

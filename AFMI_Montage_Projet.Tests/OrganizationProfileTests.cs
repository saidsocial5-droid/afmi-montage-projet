using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Radar;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_Projet.Tests
{
    public class OrganizationProfileTests
    {
        [Fact]
        public void AfmiProfile_DefaultProperties_ContainRequiredDonorFields()
        {
            var profile = new AfmiProfile();

            // Identité
            Assert.Equal("Association Forêt Modèle Ifrane (AFMI)", profile.OrganizationName);
            Assert.Equal("AFMI", profile.Acronym);
            Assert.Equal("2010", profile.CreationDate);
            Assert.Contains("dahir", profile.LegalStatus, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(profile.RegistrationNumber);
            Assert.Contains("Ifrane", profile.Headquarters);
            Assert.NotEmpty(profile.Email);

            // Vision & Objectifs
            Assert.Contains("Moyen Atlas", profile.Mission);
            Assert.NotEmpty(profile.Vision);
            Assert.NotEmpty(profile.CoreValues);
            Assert.NotEmpty(profile.StrategicAxes);
            Assert.Contains("Gouvernance", profile.StrategicAxes, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Ressources naturelles", profile.StrategicAxes, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Énergie", profile.StrategicAxes, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Revenus", profile.StrategicAxes, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Réseau", profile.StrategicAxes, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(profile.StrategicObjectives);
            Assert.Contains(profile.GeographicZones, z => z.Contains("Ifrane"));
            Assert.Contains(profile.PreferredThemes, t => t.Contains("Cédraies"));

            // Historique & Réalisations
            Assert.NotEmpty(profile.HistoryTrajectory);
            Assert.NotEmpty(profile.KeyAchievements);
            Assert.NotEmpty(profile.ImpactFigures);

            // Capacités
            Assert.NotEmpty(profile.HumanResourcesCapacity);
            Assert.NotEmpty(profile.FinancialManagementCapacity);
            Assert.NotEmpty(profile.LogisticalMaterialCapacity);

            // Bailleurs & Déclaration
            Assert.Contains("Union Européenne", profile.PreferredDonors);
            Assert.NotEmpty(profile.PastProjectsReferences);
            Assert.NotEmpty(profile.DonorCapacityStatement);
        }

        [Fact]
        public void ProjectRadarConfigService_SaveAndLoadProfile_PreservesAllEnrichedFields()
        {
            var original = new AfmiProfile
            {
                OrganizationName = "Test NGO Maroc",
                Acronym = "TNM",
                CreationDate = "2015",
                LegalStatus = "Association d'utilité publique",
                RegistrationNumber = "REC-9999",
                Headquarters = "Meknès",
                Address = "Boulevard Mohammed V",
                Phone = "+212 5 00 00 00 00",
                Email = "test@ngo.org",
                Website = "www.testngo.org",
                LegalRepresentative = "Présidente Directrice",
                Mission = "Protection des ressources hydriques et écologiques.",
                Vision = "Un écosystème restauré.",
                CoreValues = "Transparence et Intégrité.",
                StrategicObjectives = "Objectif 1, 2, 3",
                HistoryTrajectory = "Créée en 2015 par un groupe d'universitaires.",
                KeyAchievements = "10 projets réussis.",
                ImpactFigures = "50 000 bénéficiaires.",
                RecognitionsAwards = "Prix de l'écologie 2022.",
                HumanResourcesCapacity = "10 experts permanents.",
                FinancialManagementCapacity = "Audit financier annuel KPMG sans réserve.",
                LogisticalMaterialCapacity = "Bureaux et 3 véhicules 4x4.",
                PastProjectsReferences = "Projet UE Eau Durable 500k€.",
                StrategicPartnerships = "Ministère de l'Environnement.",
                TechnicalExpertiseSummary = "Hydrologie et SIG.",
                DonorCapacityStatement = "Déclaration de capacité exemplaire pour les bailleurs internationaux."
            };

            ProjectRadarConfigService.SaveProfile(original);
            var loaded = ProjectRadarConfigService.LoadProfile();

            Assert.NotNull(loaded);
            Assert.Equal("Test NGO Maroc", loaded.OrganizationName);
            Assert.Equal("TNM", loaded.Acronym);
            Assert.Equal("2015", loaded.CreationDate);
            Assert.Equal("Audit financier annuel KPMG sans réserve.", loaded.FinancialManagementCapacity);
            Assert.Equal("50 000 bénéficiaires.", loaded.ImpactFigures);
            Assert.Equal("Déclaration de capacité exemplaire pour les bailleurs internationaux.", loaded.DonorCapacityStatement);

            // Restaurer les valeurs par défaut pour ne pas altérer les autres tests
            ProjectRadarConfigService.SaveProfile(new AfmiProfile());
        }

        [Fact]
        public void ProjectRadarConfigService_LoadAxes_ReturnsFiveOfficialAfmiAxes()
        {
            var axes = ProjectRadarConfigService.LoadAxes();

            Assert.NotNull(axes);
            Assert.Equal(5, axes.Count);
            Assert.All(axes, a => Assert.True(a.IsCompleted));
            Assert.Contains(axes, a => a.Id == 1 && a.Title.Contains("Gouvernance", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 2 && a.Title.Contains("Ressources naturelles", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 3 && a.Title.Contains("Énergie", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 4 && a.Title.Contains("Revenus", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(axes, a => a.Id == 5 && a.Title.Contains("Réseau", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task OrganizationAiService_AuditProfile_ReturnsStructuredReportWithScore()
        {
            var profile = new AfmiProfile();
            string audit = await OrganizationAiService.AuditProfileAsync(profile);

            Assert.False(string.IsNullOrWhiteSpace(audit));
            Assert.True(audit.Length > 50);
            Assert.True(audit.Contains("audit", StringComparison.OrdinalIgnoreCase) || audit.Contains("institutionnel", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task OrganizationAiService_GenerateDonorCapacityStatement_ReturnsTailoredStatement()
        {
            var profile = new AfmiProfile();
            string statement = await OrganizationAiService.GenerateDonorCapacityStatementAsync(profile, "Union Européenne");

            Assert.False(string.IsNullOrWhiteSpace(statement));
            Assert.Contains(profile.OrganizationName, statement);
            Assert.True(statement.Length > 100);
        }

        [Fact]
        public void OrganizationProfileViewModel_SyncToActiveProject_UpdatesProjectProperties()
        {
            // Initialiser un projet en base
            using (var db = new AppDbContext())
            {
                db.Database.EnsureCreated();
                var proj = new Project
                {
                    Title = "Projet Test Sync Profil",
                    LeadOrganization = "Ancien Nom",
                    InterventionZone = "Ancienne Zone",
                    OrganizationalStrengths = "Anciennes forces"
                };
                db.Projects.Add(proj);
                db.SaveChanges();

                var vm = new OrganizationProfileViewModel(proj)
                {
                    OrganizationName = "AFMI Nouvelle Version",
                    Acronym = "AFMI",
                    CreationDate = "2010",
                    GeographicZonesText = "Zone A\nZone B",
                    KeyAchievements = "Restauration de 1500 ha de forêts.",
                    FinancialManagementCapacity = "Audit sans réserve.",
                    HumanResourcesCapacity = "5 ingénieurs permanents."
                };

                // Exécuter la commande
                vm.SyncToActiveProjectCommand.Execute(null);

                // Vérifier la mise à jour dans l'objet
                Assert.Equal("AFMI Nouvelle Version", proj.LeadOrganization);
                Assert.Contains("Zone A", proj.InterventionZone);
                Assert.Contains("Restauration de 1500 ha", proj.OrganizationalStrengths);
                Assert.Contains("Audit sans réserve", proj.OrganizationalStrengths);

                // Vérifier la persistance en base
                var dbProj = db.Projects.First(p => p.Id == proj.Id);
                Assert.Equal("AFMI Nouvelle Version", dbProj.LeadOrganization);
                Assert.Contains("Zone A", dbProj.InterventionZone);

                // Nettoyage
                db.Projects.Remove(dbProj);
                db.SaveChanges();
            }
        }

        [Fact]
        public void DocumentExportService_ExportProfileWordAndPdf_CreatesValidFiles()
        {
            var profile = new AfmiProfile();
            string tempWord = Path.Combine(Path.GetTempPath(), $"TestProfile_{Guid.NewGuid():N}.docx");
            string tempPdf = Path.Combine(Path.GetTempPath(), $"TestProfile_{Guid.NewGuid():N}.pdf");

            try
            {
                DocumentExportService.ExportOrganizationProfileToWord(tempWord, profile);
                Assert.True(File.Exists(tempWord));
                var wordFileInfo = new FileInfo(tempWord);
                Assert.True(wordFileInfo.Length > 1000);

                DocumentExportService.ExportOrganizationProfileToPdf(tempPdf, profile);
                Assert.True(File.Exists(tempPdf));
                var pdfFileInfo = new FileInfo(tempPdf);
                Assert.True(pdfFileInfo.Length > 1000);
            }
            finally
            {
                if (File.Exists(tempWord)) try { File.Delete(tempWord); } catch { }
                if (File.Exists(tempPdf)) try { File.Delete(tempPdf); } catch { }
            }
        }

        [Fact]
        public void NotebookLmService_GenerateKnowledgeBaseMarkdown_ContainsStructuredData()
        {
            var profile = new AfmiProfile
            {
                OrganizationName = "Association Forêt Modèle Ifrane (AFMI)",
                Acronym = "AFMI",
                FacebookUrl = "https://facebook.com/afmi",
                LinkedInUrl = "https://linkedin.com/company/afmi",
                NotebookLmUrl = "https://notebooklm.google.com/notebook/test-123"
            };

            string markdown = NotebookLmService.GenerateKnowledgeBaseMarkdown(profile);

            Assert.False(string.IsNullOrWhiteSpace(markdown));
            Assert.Contains("DOSSIER INSTITUTIONNEL", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AFMI", markdown);
            Assert.Contains("https://facebook.com/afmi", markdown);
            Assert.Contains("https://linkedin.com/company/afmi", markdown);
            Assert.Contains("CAPACITY STATEMENT", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ANCRAGE TERRITORIAL", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Gestion Financière", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("EXPÉRIENCE BAILLEURS", markdown, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void NotebookLmService_GetRecommendedPrompts_ReturnsQualityDonorPresets()
        {
            var prompts = NotebookLmService.GetRecommendedPrompts();

            Assert.NotNull(prompts);
            Assert.True(prompts.Count >= 5);
            Assert.Contains(prompts, p => p.Category.Contains("UE", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(prompts, p => p.Category.Contains("Audit", StringComparison.OrdinalIgnoreCase) || p.Title.Contains("Fiduciaire", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(prompts, p => p.PromptText.Contains("AFMI") || p.PromptText.Contains("organisation"));
        }

        [Fact]
        public void NotebookLmService_ExportKnowledgeBase_ExportsMarkdownAndPdfFiles()
        {
            var profile = new AfmiProfile();
            string tempMd = Path.Combine(Path.GetTempPath(), $"NotebookLm_{Guid.NewGuid():N}.md");
            string tempPdf = Path.Combine(Path.GetTempPath(), $"NotebookLm_{Guid.NewGuid():N}.pdf");

            try
            {
                NotebookLmService.ExportKnowledgeBase(tempMd, profile);
                Assert.True(File.Exists(tempMd));
                string mdContent = File.ReadAllText(tempMd);
                Assert.Contains("Google NotebookLM", mdContent);

                NotebookLmService.ExportKnowledgeBase(tempPdf, profile);
                Assert.True(File.Exists(tempPdf));
                var pdfInfo = new FileInfo(tempPdf);
                Assert.True(pdfInfo.Length > 1000);
            }
            finally
            {
                if (File.Exists(tempMd)) try { File.Delete(tempMd); } catch { }
                if (File.Exists(tempPdf)) try { File.Delete(tempPdf); } catch { }
            }
        }

        [Fact]
        public async Task ProfileSourceAnalysisService_HeuristicAnalysis_DetectsMetricsAndPartners()
        {
            var service = new ProfileSourceAnalysisService();
            var profile = new AfmiProfile();

            string sampleReport = @"Rapport Annuel 2024 de l'Association Forêt Modèle Ifrane.
            Dans le cadre de nos activités avec l'Union Européenne et le PNUD, l'AFMI a restauré 3 500 hectares de cédraies.
            Plus de 25 000 bénéficiaires ont été formés, dont 45 coopératives féminines.
            Consultez notre communauté officielle sur https://www.facebook.com/foretmodeleifrane et notre réseau https://www.linkedin.com/company/afmi-ifrane.
            Budget total mobilisé : 850 000 EUR.";

            var sources = new System.Collections.Generic.List<(string, string)>
            {
                ("Rapport_Activites_2024.pdf", sampleReport)
            };

            var proposals = await service.AnalyzeCorpusAsync(sources, profile);

            Assert.NotNull(proposals);
            Assert.True(proposals.Count > 0);

            // Doit détecter les chiffres clés (hectares / bénéficiaires / coopératives)
            Assert.Contains(proposals, p => p.FieldName == "ImpactFigures" || p.FieldName == "KeyAchievements");
            // Doit détecter les partenaires ou bailleurs (Union Européenne, PNUD)
            Assert.Contains(proposals, p => p.FieldName == "StrategicPartnerships" || p.FieldName == "PastProjectsReferences" || p.SuggestedValue.Contains("Union Européenne") || p.SuggestedValue.Contains("PNUD"));
        }

        [Fact]
        public void OrganizationProfileViewModel_ApplyProposal_CorrectlyUpdatesProfileFields()
        {
            var vm = new OrganizationProfileViewModel
            {
                KeyAchievements = "Réalisation existante 1.",
                ImpactFigures = "1 000 bénéficiaires",
                FacebookUrl = ""
            };

            // Test 1: Remplacement
            var fbProposal = new ProfileEnrichmentProposal
            {
                FieldName = "FacebookUrl",
                SuggestedValue = "https://facebook.com/afmi-official",
                MergeMode = EnrichmentMergeMode.Replace
            };
            vm.ApplyProposal(fbProposal);
            Assert.Equal("https://facebook.com/afmi-official", vm.FacebookUrl);

            // Test 2: Ajout (Append)
            var achProposal = new ProfileEnrichmentProposal
            {
                FieldName = "KeyAchievements",
                SuggestedValue = "• Restauration de 2 000 ha supplémentaires en 2024.",
                MergeMode = EnrichmentMergeMode.Append
            };
            vm.ApplyProposal(achProposal);
            Assert.Contains("Réalisation existante 1.", vm.KeyAchievements);
            Assert.Contains("Restauration de 2 000 ha supplémentaires", vm.KeyAchievements);

            // Test 3: Historique des sources
            vm.AddAnalyzedSource("Rapport_Final_UE_2024.pdf");
            Assert.Contains("Rapport_Final_UE_2024.pdf", vm.AnalyzedSourcesHistory);
        }

        [Fact]
        public async Task ProfileSourceAnalysisViewModel_AnalyzeSingleDocument_GeneratesTargetedProposals()
        {
            var parentVm = new OrganizationProfileViewModel();
            var analysisVm = new ProfileSourceAnalysisViewModel(parentVm);

            // Créer un document temporaire contenant des données précises
            string tempDoc = Path.Combine(Path.GetTempPath(), $"Rapport_AFMI_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(tempDoc, "Rapport de mission 2024 AFMI : 1 800 hectares restaurés dans la province d'Ifrane. Partenariat avec l'Union Européenne confirmé.");

                var docItem = new AnalysisSourceItem
                {
                    Title = Path.GetFileName(tempDoc),
                    PathOrUrl = tempDoc,
                    Type = SourceType.Document
                };
                analysisVm.Documents.Add(docItem);

                // Exécuter l'analyse unitaire de ce document
                await analysisVm.AnalyzeSingleDocumentAsync(docItem);

                Assert.Equal(SourceAnalysisStatus.Success, docItem.Status);
                Assert.True(docItem.ProposalsCount > 0);
                Assert.Contains(analysisVm.AllProposals, p => p.SourceOrigin == docItem.Title);

                // Tester le filtrage par source
                Assert.True(analysisVm.SourceFilterOptions.Count >= 2);
                analysisVm.SelectedSourceFilter = analysisVm.SourceFilterOptions.First(o => o.Contains(docItem.Title));
                Assert.True(analysisVm.FilteredProposals.Count > 0);
                Assert.All(analysisVm.FilteredProposals, p => Assert.Equal(docItem.Title, p.SourceOrigin));
            }
            finally
            {
                if (File.Exists(tempDoc)) try { File.Delete(tempDoc); } catch { }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_Projet.Tests
{
    public class TextAssistTests
    {
        #region 1. Fidelity Guard Tests

        [Fact]
        public void FidelityGuard_ExtractInvariants_ExtractsCodesNumbersAndDates()
        {
            string sourceText = "Dans le cadre de OS1 et R1.2, l'activité A1.1.3 alloue 12 500 DZD pour former 45 bénéficiaires d'ici 2026 avec un taux de 30%.";

            var invariants = FidelityGuard.ExtractInvariants(sourceText);

            Assert.Contains("OS1", invariants);
            Assert.Contains("R1.2", invariants);
            Assert.Contains("A1.1.3", invariants);
            Assert.Contains("45", invariants);
            Assert.Contains("30%", invariants);
            Assert.Contains("2026", invariants);
        }

        [Fact]
        public void FidelityGuard_Verify_SucceedsWhenAllInvariantsPreserved()
        {
            string source = "Objectif OS1 : Former 50 jeunes d'ici 2026 avec un budget de 500 000 DZD.";
            string proposal = "Au titre de l'objectif stratégique OS1, il est prévu de former 50 jeunes d'ici 2026 en mobilisant un montant de 500 000 DZD selon les normes établies.";

            var result = FidelityGuard.Verify(source, proposal);

            Assert.True(result.IsValid);
            Assert.Empty(result.MissingInvariants);
        }

        [Fact]
        public void FidelityGuard_Verify_FailsWhenCodesOrNumbersAreMissing()
        {
            string source = "Le résultat R2.1 prévoit 15 ateliers régionaux avant 2027.";
            // La proposition a oublié le code R2.1 et le chiffre 15
            string proposal = "Le résultat prévoit des ateliers régionaux avant 2027.";

            var result = FidelityGuard.Verify(source, proposal);

            Assert.False(result.IsValid);
            Assert.Contains(result.MissingInvariants, inv => inv.Contains("R2.1"));
            Assert.Contains(result.MissingInvariants, inv => inv.Contains("15"));
            Assert.NotEmpty(result.Warnings);
        }

        [Fact]
        public void FidelityGuard_Verify_DetectsPlaceholdersProperly()
        {
            string source = "Titre du projet de santé";
            string proposal = "Titre du projet de santé visant à couvrir [à compléter : nombre de centres de santé] dans la wilaya de [à compléter : nom de la localité].";

            var result = FidelityGuard.Verify(source, proposal);

            Assert.True(result.IsValid);
            Assert.Equal(2, result.PlaceholdersFound.Count);
            Assert.Contains(result.PlaceholdersFound, p => p.Contains("nombre de centres"));
            Assert.Contains(result.PlaceholdersFound, p => p.Contains("nom de la localité"));
        }

        [Fact]
        public void FidelityGuard_BuildCorrectionPrompt_GeneratesExplicitCorrectiveInstructions()
        {
            var missing = new List<string> { "OS2", "75%", "2025" };
            string retryPrompt = FidelityGuard.BuildCorrectionPrompt("Ébauche incomplète", missing);

            Assert.Contains("VÉRIFICATION DE FIDÉLITÉ ÉCHOUÉE", retryPrompt);
            Assert.Contains("OS2", retryPrompt);
            Assert.Contains("75%", retryPrompt);
            Assert.Contains("2025", retryPrompt);
            Assert.Contains("[à compléter : description]", retryPrompt);
        }

        #endregion

        #region 2. Field Type Registry Tests

        [Theory]
        [InlineData(FieldSemanticType.Indicateur, "SMART")]
        [InlineData(FieldSemanticType.Activite, "infinitif")]
        [InlineData(FieldSemanticType.Resultat, "participe passé")]
        [InlineData(FieldSemanticType.ProblemeCentral, "négative")]
        public void FieldTypeRegistry_GetSemanticGuidelines_ContainsRbmMethodologyRules(FieldSemanticType type, string expectedKeyword)
        {
            string guidance = FieldTypeRegistry.GetSemanticGuidelines(type);

            Assert.Contains(expectedKeyword, guidance, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Objectif spécifique 1", "", FieldSemanticType.ObjectifSpecifique)]
        [InlineData("Résultat attendu R1.1", "", FieldSemanticType.Resultat)]
        [InlineData("Activité A1.2.1", "", FieldSemanticType.Activite)]
        [InlineData("Indicateur de suivi", "", FieldSemanticType.Indicateur)]
        [InlineData("Problème central identifié", "", FieldSemanticType.ProblemeCentral)]
        [InlineData("Budget prévisionnel", "", FieldSemanticType.JustificationBudgetaire)]
        [InlineData("Hypothèse critique", "", FieldSemanticType.Hypothese)]
        [InlineData("Risque opérationnel", "", FieldSemanticType.Risque)]
        public void FieldTypeRegistry_DetectSemanticType_InfersCorrectTypeFromLabels(string label, string text, FieldSemanticType expected)
        {
            var detected = FieldTypeRegistry.DetectSemanticType(label, text);
            Assert.Equal(expected, detected);
        }

        #endregion

        #region 3. Text Diff Helper Tests

        [Fact]
        public void TextDiffHelper_ComputeWordDiff_DetectsAdditionsAndPreservations()
        {
            string original = "Projet agricole durable";
            string proposal = "Projet agricole biologique et durable";

            var diffs = TextDiffHelper.ComputeWordDiff(original, proposal, TextAssistActionType.Enrichir);

            Assert.NotEmpty(diffs);
            Assert.Contains(diffs, d => d.Type == DiffChunkType.Unchanged && d.Text.Trim() == "Projet");
            Assert.Contains(diffs, d => d.Type == DiffChunkType.Added && d.Text.Contains("biologique"));
        }

        [Fact]
        public void TextDiffHelper_ComputeWordDiff_HandlesEmptyOrIdenticalTexts()
        {
            // Identiques
            var identicalDiffs = TextDiffHelper.ComputeWordDiff("Texte identique", "Texte identique", TextAssistActionType.Reformuler);
            Assert.All(identicalDiffs, d => Assert.Equal(DiffChunkType.Unchanged, d.Type));

            // Source vide -> Tout est ajout
            var emptySourceDiffs = TextDiffHelper.ComputeWordDiff("", "Nouveau contenu", TextAssistActionType.Enrichir);
            Assert.All(emptySourceDiffs, d => Assert.Equal(DiffChunkType.Added, d.Type));
        }

        [Fact]
        public void TextDiffHelper_CreateHighlightedDiffDocument_CreatesFlowDocumentWithoutException()
        {
            string original = "Atelier de sensibilisation";
            string proposal = "Atelier technique de sensibilisation [à compléter : date]";

            var flowDoc = TextDiffHelper.CreateHighlightedDiffDocument(original, proposal, TextAssistActionType.Enrichir);

            Assert.NotNull(flowDoc);
            Assert.NotEmpty(flowDoc.Blocks);
        }

        #endregion

        #region 4. Text Assist Service Actions & Offline Fallback Tests

        [Fact]
        public async Task TextAssistService_ProcessActionAsync_Enrichir_PreservesSourceAndEnriches()
        {
            var service = new TextAssistService();
            var descriptor = new FieldDescriptor("test_desc", "Description du projet", FieldSemanticType.ContexteGeneral);
            var settings = new TextAssistSettings
            {
                TargetLength = nameof(TargetLengthOption.Equivalent),
                ToneRegister = nameof(ToneRegisterOption.InstitutionnelBailleur)
            };

            var response = await service.ProcessActionAsync(
                TextAssistActionType.Enrichir,
                "Renforcer les capacités des coopératives locales.",
                descriptor,
                settings);

            Assert.True(response.IsSuccess);
            Assert.NotEmpty(response.Proposal);
            Assert.Contains("coopératives locales", response.Proposal, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("capacités", response.Proposal, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TextAssistService_ProcessActionAsync_Detailler_ProducesStructuredSteps()
        {
            var service = new TextAssistService();
            var descriptor = new FieldDescriptor("test_act", "Activité de formation", FieldSemanticType.Activite);
            var settings = new TextAssistSettings
            {
                TargetLength = nameof(TargetLengthOption.PlusLong)
            };

            var source = "Organiser des sessions de formation professionnelle pour les femmes rurales.";
            var response = await service.ProcessActionAsync(
                TextAssistActionType.Detailler,
                source,
                descriptor,
                settings);

            Assert.True(response.IsSuccess);
            Assert.NotEmpty(response.Proposal);
            Assert.True(response.Proposal.Length > source.Length);
        }

        [Fact]
        public async Task TextAssistService_ProcessActionAsync_Reformuler_RefinesToneToRbmStandards()
        {
            var service = new TextAssistService();
            var descriptor = new FieldDescriptor("test_res", "Résultat attendu", FieldSemanticType.Resultat);
            var settings = new TextAssistSettings
            {
                TargetLength = nameof(TargetLengthOption.Equivalent),
                ToneRegister = nameof(ToneRegisterOption.InstitutionnelBailleur)
            };

            var source = "5 centres créés pour aider les populations.";
            var response = await service.ProcessActionAsync(
                TextAssistActionType.Reformuler,
                source,
                descriptor,
                settings);

            Assert.True(response.IsSuccess);
            Assert.NotEmpty(response.Proposal);
            // Reformuler doit maintenir les invariants chiffrés (5)
            Assert.Contains("5", response.Proposal);
        }

        #endregion

        #region 5. Database Persistence & Audit Log Tests

        [Fact]
        public void TextAssist_DatabasePersistence_SavesAndRetrievesHistoryAndSettings()
        {
            using var db = new AppDbContext();

            // 1. Sauvegarde d'un historique de version
            var history = new TextVersionHistory
            {
                FieldIdentifier = "txt_test_unit_persistance",
                FieldSemanticType = nameof(FieldSemanticType.ContexteGeneral),
                ActionType = nameof(TextAssistActionType.Enrichir),
                OriginalText = "Texte v1",
                ProposedText = "Texte v1 enrichi par l'assistant",
                FinalText = "Texte v1 enrichi par l'assistant",
                ProviderUsed = "LocalHeuristic",
                ModelUsed = "RuleEngine",
                CreatedAt = DateTime.UtcNow
            };

            db.TextVersionHistories.Add(history);
            db.SaveChanges();

            // Vérification de la lecture
            var retrieved = db.TextVersionHistories
                .Where(h => h.FieldIdentifier == "txt_test_unit_persistance")
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            Assert.NotNull(retrieved);
            Assert.Equal(nameof(TextAssistActionType.Enrichir), retrieved.ActionType);
            Assert.Equal("Texte v1 enrichi par l'assistant", retrieved.FinalText);

            // 2. Sauvegarde d'un journal d'audit
            var audit = new TextAssistAuditLog
            {
                FieldIdentifier = "txt_test_unit_persistance",
                ActionType = nameof(TextAssistActionType.Enrichir),
                ProviderUsed = "LocalHeuristic",
                ModelUsed = "RuleEngine",
                WasAccepted = true,
                WasModified = false,
                OriginalLength = 8,
                FinalLength = 32,
                ElapsedMilliseconds = 45,
                CreatedAt = DateTime.UtcNow
            };

            db.TextAssistAuditLogs.Add(audit);
            db.SaveChanges();

            var retrievedAudit = db.TextAssistAuditLogs
                .FirstOrDefault(a => a.FieldIdentifier == "txt_test_unit_persistance");

            Assert.NotNull(retrievedAudit);
            Assert.True(retrievedAudit.WasAccepted);
            Assert.Equal(45, retrievedAudit.ElapsedMilliseconds);

            // Nettoyage de la base de test
            db.TextVersionHistories.Remove(history);
            db.TextAssistAuditLogs.Remove(audit);
            db.SaveChanges();
        }

        [Fact]
        public async Task AiTextEnhancementDialog_CanBeInstantiated_WithoutNullReference()
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    var descriptor = new FieldDescriptor("Cadrage.Title", "Titre du projet", FieldSemanticType.Titre);
                    var dialog = new AFMI_Montage_projet_2.Views.AiTextEnhancementDialog(
                        descriptor: descriptor,
                        initialText: "Projet d'autonomisation économique",
                        isSelection: false,
                        initialAction: TextAssistActionType.Reformuler,
                        projectId: 1);

                    Assert.NotNull(dialog);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            var result = await tcs.Task;
            Assert.True(result);
        }

        [Fact]
        public async Task AutoExclusion_Dates_Numbers_Passwords_AreExcluded()
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    // 1. DatePickerTextBox
                    var dpTb = new System.Windows.Controls.Primitives.DatePickerTextBox();
                    Assert.True(AiTextAssistant.GetIsExcluded(dpTb));

                    // 2. TextBox avec nom de Date
                    var dateBox = new System.Windows.Controls.TextBox { Name = "StartDate" };
                    Assert.True(AiTextAssistant.GetIsExcluded(dateBox));

                    // 3. TextBox avec devise / budget
                    var budgetBox = new System.Windows.Controls.TextBox { Name = "TotalBudget" };
                    Assert.True(AiTextAssistant.GetIsExcluded(budgetBox));

                    // 4. TextBox très étroite (< 120px) mono-ligne (ex: Devise EUR)
                    var currencyBox = new System.Windows.Controls.TextBox { Width = 70, AcceptsReturn = false };
                    Assert.True(AiTextAssistant.GetIsExcluded(currencyBox));

                    // 5. TextBox d'API key / password
                    var keyBox = new System.Windows.Controls.TextBox { Name = "TxtGeminiApiKey" };
                    Assert.True(AiTextAssistant.GetIsExcluded(keyBox));

                    // 6. En revanche, un champ normal de rédaction (titre, justification, objectif) n'est PAS exclu
                    var titleBox = new System.Windows.Controls.TextBox { Name = "TxtProjectTitle", Width = 400 };
                    AiTextAssistant.SetFieldLabel(titleBox, "Titre officiel du projet");
                    Assert.False(AiTextAssistant.GetIsExcluded(titleBox));

                    var justifBox = new System.Windows.Controls.TextBox { Name = "Justification", AcceptsReturn = true, Width = 500 };
                    AiTextAssistant.SetFieldLabel(justifBox, "Justification du projet");
                    Assert.False(AiTextAssistant.GetIsExcluded(justifBox));

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            var result = await tcs.Task;
            Assert.True(result);
        }

        #endregion
    }
}

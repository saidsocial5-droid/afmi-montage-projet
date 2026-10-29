using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.ViewModels;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class CanvasServiceTests
    {
        private const string SampleSchema = @"[
            {
                ""Section"": ""1. Identification du Projet"",
                ""Fields"": [
                    { ""Name"": ""TitreProjet"", ""Label"": ""Intitulé exact du projet"", ""Type"": ""TextShort"", ""Placeholder"": ""Titre..."" },
                    { ""Name"": ""DescriptionLongue"", ""Label"": ""Description détaillée et justification"", ""Type"": ""TextLong"", ""Placeholder"": ""Description..."" },
                    { ""Name"": ""ZoneIntervention"", ""Label"": ""Zone géographique ciblée"", ""Type"": ""Dropdown"", ""Options"": [""Zone Nord"", ""Zone Sud"", ""National""], ""Placeholder"": ""Zone..."" },
                    { ""Name"": ""EstConforme"", ""Label"": ""Conformité aux critères d'éligibilité"", ""Type"": ""Checkbox"", ""Placeholder"": """" }
                ]
            },
            {
                ""Section"": ""2. Budget et Durabilité"",
                ""Fields"": [
                    { ""Name"": ""BudgetGlobal"", ""Label"": ""Montant total estimé"", ""Type"": ""TextShort"", ""Placeholder"": ""Montant..."" }
                ]
            }
        ]";

        [Fact]
        public void ParseSchema_ShouldExtractSectionsAndFieldsCorrectly()
        {
            var sections = CanvasService.ParseSchema(SampleSchema);

            Assert.NotNull(sections);
            Assert.Equal(2, sections.Count);
            Assert.Equal("1. Identification du Projet", sections[0].Section);
            Assert.Equal(4, sections[0].Fields.Count);

            var firstField = sections[0].Fields[0];
            Assert.Equal("TitreProjet", firstField.Name);
            Assert.Equal("Intitulé exact du projet", firstField.Label);
            Assert.Equal("TextShort", firstField.Type);

            var dropdownField = sections[0].Fields[2];
            Assert.Equal("Dropdown", dropdownField.Type);
            Assert.Equal(3, dropdownField.Options.Count);
            Assert.Contains("Zone Nord", dropdownField.Options);

            Assert.Equal("2. Budget et Durabilité", sections[1].Section);
            Assert.Single(sections[1].Fields);
        }

        [Fact]
        public void ExportCanvasToExcel_ShouldCreateValidExcelFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"CanvasTest_{Guid.NewGuid():N}.xlsx");
            var template = new FormTemplate
            {
                Name = "Canevas Test Bailleur",
                JsonSchema = SampleSchema
            };
            var project = new Project
            {
                Title = "Projet Démonstration AFMI",
                LeadOrganization = "ONG Partenaire"
            };
            var answers = new Dictionary<string, string>
            {
                ["TitreProjet"] = "Projet Démonstration AFMI",
                ["DescriptionLongue"] = "Voici la description détaillée des actions prévues.",
                ["ZoneIntervention"] = "Zone Nord",
                ["EstConforme"] = "True",
                ["BudgetGlobal"] = "50 000 EUR"
            };

            try
            {
                CanvasService.ExportCanvasToExcel(template, answers, project, tempFile);

                Assert.True(File.Exists(tempFile));
                var fileInfo = new FileInfo(tempFile);
                Assert.True(fileInfo.Length > 0, "Le fichier Excel exporté ne doit pas être vide.");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportCanvasToDocx_ShouldCreateValidWordFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"CanvasTest_{Guid.NewGuid():N}.docx");
            var template = new FormTemplate
            {
                Name = "Canevas Test Bailleur Word",
                JsonSchema = SampleSchema
            };
            var project = new Project
            {
                Title = "Projet Démonstration AFMI",
                LeadOrganization = "ONG Partenaire"
            };
            var answers = new Dictionary<string, string>
            {
                ["TitreProjet"] = "Projet Démonstration AFMI",
                ["DescriptionLongue"] = "Voici la description détaillée des actions.",
                ["ZoneIntervention"] = "Zone Nord"
            };

            try
            {
                CanvasService.ExportCanvasToDocx(template, answers, project, tempFile);

                Assert.True(File.Exists(tempFile));
                var fileInfo = new FileInfo(tempFile);
                Assert.True(fileInfo.Length > 0, "Le fichier Word exporté ne doit pas être vide.");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportCanvasToPdf_ShouldCreateValidPdfFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"CanvasTest_{Guid.NewGuid():N}.pdf");
            var template = new FormTemplate
            {
                Name = "Canevas Test Bailleur PDF",
                JsonSchema = SampleSchema
            };
            var project = new Project
            {
                Title = "Projet Démonstration AFMI",
                LeadOrganization = "ONG Partenaire"
            };
            var answers = new Dictionary<string, string>
            {
                ["TitreProjet"] = "Projet Démonstration AFMI",
                ["DescriptionLongue"] = "Justification et méthodologie.",
                ["BudgetGlobal"] = "50 000 EUR"
            };

            try
            {
                CanvasService.ExportCanvasToPdf(template, answers, project, tempFile);

                Assert.True(File.Exists(tempFile));
                var fileInfo = new FileInfo(tempFile);
                Assert.True(fileInfo.Length > 0, "Le fichier PDF exporté ne doit pas être vide.");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void CanvasDesignerViewModel_ShouldManageSectionsAndQuestions()
        {
            var vm = new CanvasDesignerViewModel();

            Assert.NotEmpty(vm.Sections);
            Assert.NotEmpty(vm.Sections[0].Questions);

            int initialSections = vm.Sections.Count;
            vm.AddSection();
            Assert.Equal(initialSections + 1, vm.Sections.Count);

            var lastSection = vm.Sections.Last();
            int initialQuestions = lastSection.Questions.Count;
            lastSection.AddQuestion();
            Assert.Equal(initialQuestions + 1, lastSection.Questions.Count);

            var addedQuestion = lastSection.Questions.Last();
            vm.RemoveQuestion(addedQuestion);
            Assert.Equal(initialQuestions, lastSection.Questions.Count);

            vm.RemoveSection(lastSection);
            Assert.Equal(initialSections, vm.Sections.Count);
        }

        [Fact]
        public void ExtractAnswersFromJson_ShouldHandleConversationalAiResponseStartingWithB()
        {
            // Reproduit exactement le cas de l'utilisateur : réponse de l'IA débutant par 'Bien sûr...'
            string aiResponse = @"Bien sûr ! Voici le formulaire pré-rempli pour votre projet :
            {
                ""TitreProjet"": ""Conservation du massif forestier"",
                ""DescriptionLongue"": ""Le projet vise à protéger 5000 ha de forêts communautaires."",
                ""EstConforme"": true,
                ""BudgetGlobal"": 75000
            }
            En espérant que ces réponses vous conviennent.";

            var template = new FormTemplate { JsonSchema = SampleSchema };
            var answers = FormulaireDynamiqueViewModel.ExtractAnswersFromJson(aiResponse, template);

            Assert.NotNull(answers);
            Assert.Equal(4, answers.Count);
            Assert.Equal("Conservation du massif forestier", answers["TitreProjet"]);
            Assert.Equal("Le projet vise à protéger 5000 ha de forêts communautaires.", answers["DescriptionLongue"]);
            Assert.Equal("True", answers["EstConforme"]);
            Assert.Equal("75000", answers["BudgetGlobal"]);
        }

        [Fact]
        public void ExtractAnswersFromJson_ShouldHandleMarkdownCodeFencesAndTrailingCommas()
        {
            string aiResponse = @"Voici le JSON demandé :
```json
{
    ""TitreProjet"": ""Projet Énergie Solaire"",
    ""ZoneIntervention"": ""Zone Nord"",
}
```
Bonne continuation !";

            var answers = FormulaireDynamiqueViewModel.ExtractAnswersFromJson(aiResponse);

            Assert.NotNull(answers);
            Assert.Equal(2, answers.Count);
            Assert.Equal("Projet Énergie Solaire", answers["TitreProjet"]);
            Assert.Equal("Zone Nord", answers["ZoneIntervention"]);
        }
    }
}

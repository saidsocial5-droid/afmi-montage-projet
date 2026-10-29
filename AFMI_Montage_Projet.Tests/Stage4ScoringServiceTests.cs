using System;
using System.Collections.Generic;
using Xunit;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_Projet.Tests
{
    public class Stage4ScoringServiceTests
    {
        [Fact]
        public void OpportunityScoringService_ParseValidJson_ExtractsAllFieldsAccurately()
        {
            var service = new OpportunityScoringService();
            string validJson = @"{
  ""relevanceScore"": 9.3,
  ""matchedAxes"": [""Axe 1 - Forêts"", ""Axe 4 - Climat""],
  ""donor"": ""Union Européenne"",
  ""estimatedBudget"": 120000.0,
  ""currency"": ""EUR"",
  ""deadline"": ""2026-12-15"",
  ""summaryAr"": ""مشروع للحفاظ على غابات الأرز ومكافحة التصحر"",
  ""summaryFr"": ""Projet de restauration des cédraies et adaptation climatique"",
  ""rationale"": ""Excellente adéquation territoriale avec le Moyen Atlas et cibles conformes.""
}";

            var item = new RssItemCache
            {
                Title = "Appel Test",
                CleanDescription = "Description test",
                Donor = "Union Européenne"
            };

            var result = service.ParseAndValidateAiJson(validJson, item);

            Assert.Equal(9.3, result.RelevanceScore);
            Assert.Equal(2, result.MatchedAxes.Count);
            Assert.Equal("Union Européenne", result.Donor);
            Assert.Equal(120000m, result.EstimatedBudget);
            Assert.Equal("EUR", result.Currency);
            Assert.NotNull(result.Deadline);
            Assert.Equal(2026, result.Deadline!.Value.Year);
            Assert.Equal(12, result.Deadline!.Value.Month);
            Assert.Contains("الأرز", result.SummaryAr);
            Assert.Contains("cédraies", result.SummaryFr);
            Assert.True(result.IsAiGenerated);
        }

        [Fact]
        public void OpportunityScoringService_HonestyRule_NullBudgetAndDeadlineWhenNotExplicit()
        {
            var service = new OpportunityScoringService();
            // JSON returning null for budget and deadline
            string honestJson = @"{
  ""relevanceScore"": 7.0,
  ""matchedAxes"": [""Axe 2""],
  ""donor"": ""GEF"",
  ""estimatedBudget"": null,
  ""currency"": null,
  ""deadline"": null,
  ""summaryAr"": ""ملخص عربي بدون ميزانية"",
  ""summaryFr"": ""Résumé français sans budget"",
  ""rationale"": ""Aucun budget ni date limite mentionnés dans le texte source.""
}";

            var item = new RssItemCache { Title = "Appel Sans Chiffres", CleanDescription = "Texte général", Donor = "GEF" };
            var result = service.ParseAndValidateAiJson(honestJson, item);

            Assert.Null(result.EstimatedBudget);
            Assert.Null(result.Currency);
            Assert.Null(result.Deadline);
            Assert.Equal(7.0, result.RelevanceScore);
        }

        [Fact]
        public void OpportunityScoringService_MarkdownFencesStripping_RepairsAndParsesSuccessfully()
        {
            var service = new OpportunityScoringService();
            string fencedJson = @"Voici mon analyse détaillée :
```json
{
  ""relevanceScore"": 8.0,
  ""matchedAxes"": [""Axe 1""],
  ""donor"": ""AFD"",
  ""estimatedBudget"": 50000,
  ""currency"": ""EUR"",
  ""deadline"": ""2026-10-30"",
  ""summaryAr"": ""ملخص"",
  ""summaryFr"": ""Résumé"",
  ""rationale"": ""Alignement solide.""
}
```
En espérant que cette analyse vous soit utile.";

            var item = new RssItemCache { Title = "Appel AFD", CleanDescription = "Texte", Donor = "AFD" };
            var result = service.ParseAndValidateAiJson(fencedJson, item);

            Assert.Equal(8.0, result.RelevanceScore);
            Assert.Equal(50000m, result.EstimatedBudget);
        }

        [Fact]
        public void OpportunityScoringService_HeuristicFallback_ScoresCorrectlyWithoutAi()
        {
            var service = new OpportunityScoringService();

            var profile = new AfmiProfile();
            var axes = new List<AfmiAxis> { new AfmiAxis { Id = 1, Title = "Axe 1: Forêts" } };

            var relevantItem = new RssItemCache
            {
                Title = "Appel d'urgence pour la sauvegarde du Cèdre de l'Atlas dans le Moyen Atlas (Ifrane)",
                CleanDescription = "Programme visant la restauration de la biodiversité forestière et la gestion de l'eau avec les coopératives sylvopastorales. Budget jusqu'à 80000 EUR avant le 15/11/2026.",
                Donor = "GEF SGP"
            };

            var result = service.ScoreViaHeuristics(relevantItem, profile, axes, "Pas de connexion");

            // Le score doit être élevé car il contient cèdre, moyen atlas, ifrane, biodiversité, eau, coopérative, etc.
            Assert.True(result.RelevanceScore >= 8.0);
            Assert.False(result.IsAiGenerated);
            Assert.Contains("تقدير بدون ذكاء اصطناعي", result.Rationale);
            Assert.Equal(80000m, result.EstimatedBudget);
            Assert.Equal("EUR", result.Currency);
            Assert.NotNull(result.Deadline);
            Assert.NotEmpty(result.SummaryAr);
            Assert.NotEmpty(result.SummaryFr);
        }

        [Fact]
        public void OpportunityScoringService_HeuristicFallback_LowScoreForIrrelevantContent()
        {
            var service = new OpportunityScoringService();
            var profile = new AfmiProfile();
            var axes = new List<AfmiAxis>();

            var irrelevantItem = new RssItemCache
            {
                Title = "Financement d'équipements informatiques pour les écoles urbaines de Tokyo",
                CleanDescription = "Achat d'ordinateurs et de serveurs pour les salles multimédias.",
                Donor = "Autre"
            };

            var result = service.ScoreViaHeuristics(irrelevantItem, profile, axes);

            // Score bas (proche du score de base 3.0) car aucun mot-clé AFMI
            Assert.True(result.RelevanceScore <= 4.0);
            Assert.False(result.IsAiGenerated);
        }
    }
}

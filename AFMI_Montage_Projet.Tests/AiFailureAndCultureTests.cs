using System;
using System.Globalization;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>
    /// ADR 27 : un moteur IA indisponible est signalé comme un échec (jamais un message d'erreur rendu comme réponse),
    /// le consentement cloud s'applique aux envois hors routeur, et le formatage ne dépend pas de la langue de Windows.
    /// </summary>
    public class AiFailureAndCultureTests
    {
        [Fact]
        public void TestEnvironment_PointsOllamaToClosedPort()
        {
            Assert.Equal(TestEnvironment.UnreachableOllamaUrl, LocalSettingsService.LoadSettings().OllamaUrl);
        }

        [Fact]
        public async Task LocalAiEngine_Unreachable_Throws()
        {
            await Assert.ThrowsAsync<LocalAiUnavailableException>(() => new LocalAiEngine().GenerateAsync("Bonjour"));
        }

        [Fact]
        public async Task Router_EngineUnavailable_ReturnsFailureWithEmptyTextAndCause()
        {
            var result = await new AiRouterService().GenerateResponseAsync("Rédige une phrase sur les parcours forestiers.");

            Assert.False(result.IsSuccess);
            Assert.Equal(string.Empty, result.ResponseText);
            Assert.Contains("Ollama", result.ErrorMessage);
        }

        [Fact]
        public async Task CallAiAsync_EngineUnavailable_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => AiAssistantService.CallAiAsync("Bonjour"));
        }

        [Fact]
        public void IsCloudUseAllowed_RequiresCloudPreferenceAndConsent()
        {
            Assert.False(AiRouterService.IsCloudUseAllowed(null, out _));
            Assert.False(AiRouterService.IsCloudUseAllowed(new AppConfig { UseLocalByDefault = true, CloudOptInGranted = true }, out _));
            Assert.False(AiRouterService.IsCloudUseAllowed(new AppConfig { UseLocalByDefault = false, CloudOptInGranted = false }, out string reason));
            Assert.Contains("autorisé", reason);
            Assert.False(AiRouterService.IsCloudUseAllowed(new AppConfig { UseLocalByDefault = false, CloudOptInGranted = true, MonthlyRequestLimit = 0 }, out string quota));
            Assert.Contains("Limite mensuelle", quota);
            Assert.True(AiRouterService.IsCloudUseAllowed(new AppConfig { UseLocalByDefault = false, CloudOptInGranted = true, MonthlyRequestLimit = 1_000_000 }, out _));
        }

        [Fact]
        public void AppCulture_FormatsNumbersTheFrenchWay()
        {
            Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
            string formatted = 1200.ToString("N0").Replace(' ', ' ').Replace(' ', ' ');
            Assert.Equal("1 200", formatted);
        }

        [Fact]
        public void Project_CurrencyOf_FallsBackToDefault()
        {
            Assert.Equal(Project.DefaultCurrency, Project.CurrencyOf(null));
            Assert.Equal(Project.DefaultCurrency, Project.CurrencyOf(new Project { Currency = " " }));
            Assert.Equal("MAD", Project.CurrencyOf(new Project { Currency = "MAD" }));
        }
    }
}

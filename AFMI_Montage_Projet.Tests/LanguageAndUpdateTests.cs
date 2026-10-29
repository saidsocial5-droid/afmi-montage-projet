using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Language;
using AFMI_Montage_projet_2.Services.Updates;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    /// <summary>ADR 26 : détection et contrôle de la langue des réponses IA, traduction, mises à jour GitHub.</summary>
    public class LanguageAndUpdateTests
    {
        private const string French = "Le projet vise à renforcer la gestion durable des parcours forestiers avec les associations locales et les femmes rurales de la province.";
        private const string Arabic = "يهدف المشروع إلى تعزيز التدبير المستدام للمراعي الغابوية بشراكة مع الجمعيات المحلية والنساء القرويات بالإقليم.";
        private const string English = "The project aims to strengthen the sustainable management of forest rangelands with local associations and rural women of the province.";

        [Theory]
        [InlineData(French, "fr")]
        [InlineData(Arabic, "ar")]
        [InlineData(English, "en")]
        public void Detect_IdentifiesTheThreeLanguages(string text, string expected)
        {
            var detected = LanguageGuard.Detect(text);
            Assert.Equal(expected, detected.Language);
            Assert.True(detected.IsReliable);
        }

        [Fact]
        public void Detect_IgnoresTooShortOrStructuredText()
        {
            Assert.False(LanguageGuard.Detect("OK").IsReliable);
            Assert.False(LanguageGuard.IsWrongLanguage("{\"summary\": \"" + English + "\"}", "fr"));
            Assert.True(LanguageGuard.IsWrongLanguage(English, "fr"));
            Assert.False(LanguageGuard.IsWrongLanguage(French, "fr"));
        }

        [Theory]
        [InlineData("Rédige le résumé en arabe.", "ar")]
        [InlineData("Réponds en anglais s'il te plaît.", "en")]
        [InlineData("Réponds en français.", "fr")]
        [InlineData("اكتب الملخص بالعربية", "ar")]
        [InlineData("Donne un résumé bilingue français et arabe.", "multi")]
        [InlineData("Traduis ce texte.", "multi")]
        [InlineData("Analyse le projet suivant.", null)]
        public void ExplicitLanguageRequests_AreRecognized(string prompt, string? expected)
        {
            Assert.Equal(expected, LanguageGuard.ExplicitRequestedLanguage(prompt, ""));
        }

        [Fact]
        public void ResponseLanguage_FollowsCallerThenRequestThenPrompt()
        {
            Assert.Equal("ar", AiRouterService.ResolveResponseLanguage(French, "", "Standard", "ar"));
            Assert.Null(AiRouterService.ResolveResponseLanguage(French, "", TranslationService.TaskType, null));
            Assert.Equal("en", AiRouterService.ResolveResponseLanguage("Analyse et réponds en anglais : " + French, "", "Standard", null));
            Assert.Null(AiRouterService.ResolveResponseLanguage("Résumé bilingue : " + French, "", "Standard", null));
            Assert.Equal("fr", AiRouterService.ResolveResponseLanguage(French, "Tu es un expert en montage de projets.", "Standard", null)); // réglage « auto » des tests
        }

        [Fact]
        public void TextAssist_OutputLanguage_IsSourceLanguageInAutoMode()
        {
            var auto = new TextAssistSettings { OutputLanguage = nameof(OutputLanguageOption.Auto) };
            Assert.Equal("ar", AFMI_Montage_projet_2.Services.TextAssistService.ResolveOutputLanguage(auto, Arabic));
            Assert.Equal("fr", AFMI_Montage_projet_2.Services.TextAssistService.ResolveOutputLanguage(auto, French));
            var forced = new TextAssistSettings { OutputLanguage = "English" };
            Assert.Equal("en", AFMI_Montage_projet_2.Services.TextAssistService.ResolveOutputLanguage(forced, Arabic));
        }

        // ---------- Traduction ----------

        private static RoutingResult Ok(string text) => new() { IsSuccess = true, ResponseText = text, EngineName = "Test" };

        [Fact]
        public async Task Translation_ChecksNumbers_AndRetriesWhenWrongLanguage()
        {
            int calls = 0;
            var service = new TranslationService((prompt, system) =>
            {
                calls++;
                Assert.Contains("arabe", system);
                // 1re réponse : encore en français ; 2e : arabe mais sans le chiffre 250
                return Task.FromResult(calls == 1 ? Ok(French) : Ok("يهدف المشروع إلى تكوين النساء القرويات في 12 جماعة بالإقليم."));
            });

            var result = await service.TranslateAsync("Former 250 femmes rurales dans 12 communes de la province d'Ifrane.", AppLanguages.Arabic);

            Assert.Equal(2, calls);
            Assert.True(result.IsSuccess);
            Assert.StartsWith("يهدف", result.Text);
            Assert.Contains(result.Warnings, w => w.Contains("250"));
            Assert.DoesNotContain(result.Warnings, w => w.Contains("12,"));
        }

        [Fact]
        public async Task Translation_SkipsTextAlreadyInTargetLanguage()
        {
            var service = new TranslationService((_, _) => throw new InvalidOperationException("ne doit pas être appelé"));
            var result = await service.TranslateAsync(Arabic, AppLanguages.Arabic);
            Assert.True(result.IsSuccess);
            Assert.Equal(Arabic, result.Text);
        }

        [Fact]
        public async Task Translation_LongTexts_AreSplitIntoChunks_AndReassembled()
        {
            var paragraphs = Enumerable.Range(1, 6).Select(i => $"Paragraphe {i} : " + string.Concat(Enumerable.Repeat(French + " ", 6))).ToList();
            string text = string.Join("\n\n", paragraphs);
            int calls = 0;
            var service = new TranslationService((prompt, _) => { calls++; return Task.FromResult(Ok(English + $" Part {calls}.")); });

            var result = await service.TranslateAsync(text, AppLanguages.English);

            Assert.True(calls >= 2);
            Assert.All(TranslationService.SplitIntoChunks(text, TranslationService.MaxChunkLength), c => Assert.True(c.Length <= TranslationService.MaxChunkLength));
            Assert.Contains("Part 1.", result.Text);
            Assert.Contains($"Part {calls}.", result.Text);
        }

        [Fact]
        public void Numbers_InArabicIndicDigits_AreRecognized()
        {
            Assert.Empty(TranslationService.MissingNumbers("Budget : 120 000 MAD pour 36 mois", "الميزانية ١٢٠٠٠٠ درهم لمدة ٣٦ شهرا"));
            Assert.Equal(new[] { "36" }, TranslationService.MissingNumbers("Budget : 120 000 MAD pour 36 mois", "الميزانية 120000 درهم"));
        }

        // ---------- Mises à jour GitHub ----------

        [Theory]
        [InlineData("afmi-ifrane/afmi-montage-projet", "afmi-ifrane/afmi-montage-projet")]
        [InlineData("https://github.com/afmi-ifrane/afmi-montage-projet.git", "afmi-ifrane/afmi-montage-projet")]
        [InlineData("https://github.com/afmi-ifrane/afmi-montage-projet/", "afmi-ifrane/afmi-montage-projet")]
        public void Repository_IsNormalized(string input, string expected)
        {
            Assert.True(GitHubUpdateService.IsValidRepository(input));
            Assert.Equal(expected, GitHubUpdateService.NormalizeRepository(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("pas un dépôt")]
        [InlineData("../../etc/passwd")]
        public void Repository_InvalidValuesAreRejected(string input) => Assert.False(GitHubUpdateService.IsValidRepository(input));

        [Fact]
        public void Release_IsParsed_WithInstallerAndDigest()
        {
            const string json = @"{
  ""tag_name"": ""v1.9.21"", ""name"": ""AFMI Montage Projet 1.9.21"", ""body"": ""Traduction et mises à jour"",
  ""html_url"": ""https://github.com/afmi/app/releases/tag/v1.9.21"", ""published_at"": ""2026-11-02T10:00:00Z"",
  ""assets"": [
    { ""name"": ""AFMIMontageProjet_Setup_v1.9.21.exe"", ""size"": 123456789, ""digest"": ""sha256:ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789"",
      ""browser_download_url"": ""https://github.com/afmi/app/releases/download/v1.9.21/AFMIMontageProjet_Setup_v1.9.21.exe"" },
    { ""name"": ""AFMIMontageProjet_Setup_v1.9.21.exe.sha256"", ""size"": 100, ""browser_download_url"": ""https://github.com/afmi/app/releases/download/v1.9.21/x.sha256"" },
    { ""name"": ""notes.pdf"", ""size"": 10, ""browser_download_url"": ""https://github.com/afmi/app/releases/download/v1.9.21/notes.pdf"" }
  ]
}";
            var release = GitHubUpdateService.ParseRelease(json);

            Assert.Equal(new Version(1, 9, 21), release.Version);
            Assert.NotNull(release.Installer);
            Assert.Equal("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", release.Installer!.Sha256);
            Assert.NotNull(release.ChecksumFile);
            Assert.True(release.Version > Version.Parse(GitHubUpdateService.CurrentVersion));
        }

        [Fact]
        public void Sha256_OfDownloadedFile_IsComputed()
        {
            string path = Path.Combine(TestEnvironment.DataDirectory, "setup_test.exe");
            File.WriteAllText(path, "abc");
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", GitHubUpdateService.ComputeSha256(path));
        }

        [Fact]
        public void AppVersion_IsAlignedWithInstallerScript()
        {
            string root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "setup.iss"))) root = Path.GetDirectoryName(root)!;
            Assert.NotNull(root);
            string iss = File.ReadAllText(Path.Combine(root!, "setup.iss"));
            Assert.Contains($"#define MyAppVersion \"{GitHubUpdateService.CurrentVersion}\"", iss);
            Assert.Contains("AppMutex=AFMI_Montage_Projet_SingleInstance_Mutex", iss);
            var bytes = File.ReadAllBytes(Path.Combine(root!, "setup.iss"));
            Assert.True(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "setup.iss doit être en UTF-8 avec BOM pour Inno Setup (accents de l'éditeur).");
        }
    }
}

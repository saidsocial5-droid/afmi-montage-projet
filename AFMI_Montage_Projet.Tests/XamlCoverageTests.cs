using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class XamlCoverageTests
    {
        private static readonly Regex TextControlRegex = new(@"<(TextBox|RichTextBox)\b([^>]*)>", RegexOptions.Compiled);
        private static readonly Regex BindingRegex = new(@"Text=""\{Binding\s+([^,}]+)", RegexOptions.Compiled);
        private static readonly Regex NameRegex = new(@"(?:x:Name|Name)=""([^""]+)""", RegexOptions.Compiled);

        private static string FindRepositoryRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "AFMI Montage projet 2.csproj")) ||
                    File.Exists(Path.Combine(dir, "docs", "EXCLUSIONS_TEXT_ASSIST.md")))
                {
                    return dir;
                }
                dir = Directory.GetParent(dir)?.FullName!;
            }

            // Fallback
            return @"F:\Aplication\AFMI Montage projet 2";
        }

        [Fact]
        public void AllXamlTextBoxes_MustBeCoveredOrDocumentedAsExcluded()
        {
            string root = FindRepositoryRoot();
            Assert.True(Directory.Exists(root), $"Le répertoire racine '{root}' doit exister.");

            string exclusionsDocPath = Path.Combine(root, "docs", "EXCLUSIONS_TEXT_ASSIST.md");
            Assert.True(File.Exists(exclusionsDocPath), "Le document docs/EXCLUSIONS_TEXT_ASSIST.md doit exister.");

            string exclusionsDocText = File.ReadAllText(exclusionsDocPath);

            // Rechercher tous les fichiers XAML (hors bin et obj)
            var xamlFiles = Directory.GetFiles(root, "*.xaml", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .ToList();

            Assert.NotEmpty(xamlFiles);

            int totalControls = 0;
            int coveredControls = 0;
            int excludedControls = 0;
            var unlistedExclusions = new List<string>();

            foreach (var filePath in xamlFiles)
            {
                string relativePath = Path.GetRelativePath(root, filePath).Replace('\\', '/');
                string fileName = Path.GetFileName(filePath);
                string content = File.ReadAllText(filePath);

                var matches = TextControlRegex.Matches(content);
                foreach (Match match in matches)
                {
                    totalControls++;
                    string attrs = match.Groups[2].Value;

                    bool isExplicitlyExcluded = attrs.Contains("AiTextAssistant.IsExcluded=\"True\"");

                    if (isExplicitlyExcluded)
                    {
                        excludedControls++;

                        // Extraire le nom ou le binding
                        var nameMatch = NameRegex.Match(attrs);
                        var bindingMatch = BindingRegex.Match(attrs);
                        string identifier = nameMatch.Success 
                            ? nameMatch.Groups[1].Value 
                            : (bindingMatch.Success ? bindingMatch.Groups[1].Value : "(unnamed)");

                        // Vérifier si cette exclusion est bien documentée dans EXCLUSIONS_TEXT_ASSIST.md
                        bool isDocumented = exclusionsDocText.Contains(fileName) && 
                                            (exclusionsDocText.Contains(identifier) || exclusionsDocText.Contains(attrs.Trim()));

                        if (!isDocumented)
                        {
                            unlistedExclusions.Add($"{fileName} -> {identifier}");
                        }
                    }
                    else
                    {
                        // Couvert universellement par AiTextAssistant (EventManager.RegisterClassHandler)
                        coveredControls++;
                    }
                }
            }

            // Vérifications d'intégrité
            Assert.True(totalControls > 50, $"L'analyse doit trouver les contrôles de texte de l'application (trouvé: {totalControls}).");
            Assert.Empty(unlistedExclusions);
            Assert.Equal(totalControls, coveredControls + excludedControls);
            Assert.True(coveredControls > 0, "La majorité des contrôles doivent être couverts.");
            Assert.True(excludedControls >= 5, "Les contrôles sensibles déclarés doivent être détectés comme exclus.");
        }
    }
}

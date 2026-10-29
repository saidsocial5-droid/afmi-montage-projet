using System;
using Xunit;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_Projet.Tests
{
    public class SwotParsingTests
    {
        [Fact]
        public void ParseSwotResponse_StandardJson_ExtractsAllQuadrants()
        {
            string raw = @"
{
  ""strengths"": ""- Expertise de l'équipe\n- Bon réseau local"",
  ""weaknesses"": ""- Dépendance financière"",
  ""opportunities"": ""- Nouveaux bailleurs"",
  ""threats"": ""- Instabilité économique""
}";

            CadrageViewModel.ParseSwotResponse(raw, out string s, out string w, out string o, out string t);

            Assert.Contains("Expertise", s);
            Assert.Contains("Dépendance", w);
            Assert.Contains("bailleurs", o);
            Assert.Contains("Instabilité", t);
        }

        [Fact]
        public void ParseSwotResponse_PreambleStartingWithH_DoesNotThrowAndParsesSuccessfully()
        {
            // Reproduces the exact user bug where response starts with 'H' ("Here is...")
            string raw = @"Here is the complete SWOT analysis for your local development project in strict JSON format:
{
  ""strengths"": ""- Forte mobilisation communautaire\n- Équipe expérimentée"",
  ""weaknesses"": ""- Ressources matérielles limitées"",
  ""opportunities"": ""- Soutien des collectivités territoriales"",
  ""threats"": ""- Changement climatique et sécheresse""
}
I hope this SWOT analysis meets your requirements!";

            CadrageViewModel.ParseSwotResponse(raw, out string s, out string w, out string o, out string t);

            Assert.Contains("Forte mobilisation", s);
            Assert.Contains("Ressources matérielles", w);
            Assert.Contains("collectivités", o);
            Assert.Contains("Changement climatique", t);
        }

        [Fact]
        public void ParseSwotResponse_MarkdownCodeFenceWithPreamble_ExtractsCorrectly()
        {
            string raw = @"Voici la matrice SWOT générée :
```json
{
  ""strengths"": ""- Point fort 1"",
  ""weaknesses"": ""- Point faible 1"",
  ""opportunities"": ""- Opportunité 1"",
  ""threats"": ""- Menace 1""
}
```
Bonne continuation !";

            CadrageViewModel.ParseSwotResponse(raw, out string s, out string w, out string o, out string t);

            Assert.Contains("Point fort 1", s);
            Assert.Contains("Point faible 1", w);
            Assert.Contains("Opportunité 1", o);
            Assert.Contains("Menace 1", t);
        }

        [Fact]
        public void ParseSwotResponse_JsonArraysInsteadOfStrings_FormatsAsBullets()
        {
            string raw = @"
{
  ""strengths"": [""Ancrage local solide"", ""Partenariats actifs""],
  ""weaknesses"": [""Budget de départ restreint""],
  ""opportunities"": [""Appels à projets ouverts""],
  ""threats"": [""Inflation""]
}";

            CadrageViewModel.ParseSwotResponse(raw, out string s, out string w, out string o, out string t);

            Assert.Contains("- Ancrage local solide", s);
            Assert.Contains("- Partenariats actifs", s);
            Assert.Contains("- Budget de départ restreint", w);
            Assert.Contains("- Appels à projets ouverts", o);
            Assert.Contains("- Inflation", t);
        }

        [Fact]
        public void ParseSwotResponse_FrenchKeysAndPascalCase_ExtractsCorrectly()
        {
            string raw = @"
{
  ""Forces"": ""- Notoriété associative"",
  ""Faiblesses"": ""- Communication digitale faible"",
  ""Opportunités"": ""- Digitalisation accrue"",
  ""Menaces"": ""- Désengagement de certains partenaires""
}";

            CadrageViewModel.ParseSwotResponse(raw, out string s, out string w, out string o, out string t);

            Assert.Contains("Notoriété associative", s);
            Assert.Contains("Communication digitale", w);
            Assert.Contains("Digitalisation", o);
            Assert.Contains("Désengagement", t);
        }

        [Fact]
        public void ParseSwotResponse_PlainTextMarkdownFallback_ExtractsAllSections()
        {
            string raw = @"Voici l'analyse SWOT du projet :

### Forces et atouts :
- Equipe locale engagée
- Reconnaissance sur le terrain

### Faiblesses internes :
- Outils de gestion vieillissants

### Opportunités externes :
- Cadre réglementaire favorable

### Menaces et risques :
- Hausse des coûts logistiques";

            CadrageViewModel.ParseSwotResponse(raw, out string s, out string w, out string o, out string t);

            Assert.Contains("Equipe locale engagée", s);
            Assert.Contains("Outils de gestion vieillissants", w);
            Assert.Contains("Cadre réglementaire favorable", o);
            Assert.Contains("Hausse des coûts logistiques", t);
        }
    }
}

using System;
using System.Threading.Tasks;
using Xunit;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Helpers;

namespace AFMI_Montage_Projet.Tests
{
    public class TextEnhancementTests
    {
        [Fact]
        public void TextEnhancementMode_ContainsAllRequiredModes()
        {
            Assert.True(Enum.IsDefined(typeof(TextEnhancementMode), TextEnhancementMode.Ameliorer));
            Assert.True(Enum.IsDefined(typeof(TextEnhancementMode), TextEnhancementMode.Enrichir));
            Assert.True(Enum.IsDefined(typeof(TextEnhancementMode), TextEnhancementMode.Reformuler));
            Assert.True(Enum.IsDefined(typeof(TextEnhancementMode), TextEnhancementMode.Generer));
            Assert.True(Enum.IsDefined(typeof(TextEnhancementMode), TextEnhancementMode.Corriger));
        }

        [Fact]
        public void AiEnhancementAction_ContainsReplaceAndAppend()
        {
            Assert.True(Enum.IsDefined(typeof(AiEnhancementAction), AiEnhancementAction.Replace));
            Assert.True(Enum.IsDefined(typeof(AiEnhancementAction), AiEnhancementAction.Append));
            Assert.True(Enum.IsDefined(typeof(AiEnhancementAction), AiEnhancementAction.None));
        }

        [Theory]
        [InlineData("```markdown\nTexte amélioré avec succès.\n```", "Texte amélioré avec succès.")]
        [InlineData("```text\nTexte enrichi pour le projet.\n```", "Texte enrichi pour le projet.")]
        [InlineData("\"\"\"Proposition reformulée.\"\"\"", "Proposition reformulée.")]
        [InlineData("Voici la formulation proposée : Le projet renforce les capacités locales.", "Le projet renforce les capacités locales.")]
        public void TextEnhancementService_CleanAiResponse_RemovesArtifacts(string input, string expected)
        {
            var service = new TextEnhancementService();
            
            // On utilise la réflexion pour tester la méthode privée de nettoyage
            var method = typeof(TextEnhancementService).GetMethod("CleanAiResponse", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(method);
            var result = method.Invoke(service, new object[] { input }) as string;
            
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuildSystemInstruction_ProducesSpecializedPromptPerMode()
        {
            var service = new TextEnhancementService();
            var method = typeof(TextEnhancementService).GetMethod("BuildSystemInstruction", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var instrAmeliorer = method.Invoke(service, new object[] { TextEnhancementMode.Ameliorer }) as string;
            Assert.Contains("AMÉLIORER", instrAmeliorer);
            Assert.Contains("GAR / RBM", instrAmeliorer);

            var instrEnrichir = method.Invoke(service, new object[] { TextEnhancementMode.Enrichir }) as string;
            Assert.Contains("ENRICHIR", instrEnrichir);

            var instrReformuler = method.Invoke(service, new object[] { TextEnhancementMode.Reformuler }) as string;
            Assert.Contains("REFORMULER", instrReformuler);

            var instrGenerer = method.Invoke(service, new object[] { TextEnhancementMode.Generer }) as string;
            Assert.Contains("PROPOSER", instrGenerer);

            var instrCorriger = method.Invoke(service, new object[] { TextEnhancementMode.Corriger }) as string;
            Assert.Contains("CORRIGER", instrCorriger);
        }

        [Fact]
        public void BuildPrompt_IncludesFieldLabelAndCustomInstructions()
        {
            var service = new TextEnhancementService();
            var method = typeof(TextEnhancementService).GetMethod("BuildPrompt", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var prompt = method.Invoke(service, new object[] { 
                "Phrase test", 
                TextEnhancementMode.Ameliorer, 
                "Intitulé de l'idée de projet", 
                "Contexte du projet AFMI", 
                "Consigne: concis", 
                "Normes Bailleurs", 
                true 
            }) as string;

            Assert.Contains("[CHAMP DU FORMULAIRE : Intitulé de l'idée de projet]", prompt);
            Assert.Contains("[TONALITÉ ET STYLE DEMANDÉS : Normes Bailleurs]", prompt);
            Assert.Contains("[CONSIGNE PARTICULIÈRE DE L'UTILISATEUR : Consigne: concis]", prompt);
            Assert.Contains("Contexte du projet AFMI", prompt);
            Assert.Contains("Phrase test", prompt);
            Assert.Contains("sélectionné une phrase ou un passage spécifique", prompt);
        }
    }
}

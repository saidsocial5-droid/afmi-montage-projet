using System;
using Xunit;
using AFMI_Montage_projet_2.Services;

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
    }
}

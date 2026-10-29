using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_Projet.Tests
{
    public class RiskAiServiceTests
    {
        [Fact]
        public void RiskProposal_CriticalityScoreAndColor_CalculateCorrectly()
        {
            var pHigh = new RiskProposal { Probability = 4, Impact = 4 };
            Assert.Equal(16, pHigh.RiskScore);
            Assert.Equal("#EF4444", pHigh.ScoreColor);
            Assert.Equal("Critique", pHigh.ScoreLabel);

            var pMedium = new RiskProposal { Probability = 3, Impact = 3 };
            Assert.Equal(9, pMedium.RiskScore);
            Assert.Equal("#F59E0B", pMedium.ScoreColor);
            Assert.Equal("Modéré", pMedium.ScoreLabel);

            var pLow = new RiskProposal { Probability = 1, Impact = 2 };
            Assert.Equal(2, pLow.RiskScore);
            Assert.Equal("#10B981", pLow.ScoreColor);
            Assert.Equal("Faible", pLow.ScoreLabel);
        }

        [Fact]
        public async Task RiskAiService_GenerateRisksAsync_ReturnsValidProposals()
        {
            var service = new RiskAiService();
            var list = await service.GenerateRisksAsync(999);

            Assert.NotNull(list);
            Assert.NotEmpty(list);
            Assert.All(list, r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Category));
                Assert.False(string.IsNullOrWhiteSpace(r.Description));
                Assert.InRange(r.Probability, 1, 5);
                Assert.InRange(r.Impact, 1, 5);
                Assert.False(string.IsNullOrWhiteSpace(r.MitigationMeasure));
            });
        }

        [Fact]
        public async Task RiskAiService_AuditRiskMatrixAsync_ProducesReport()
        {
            var service = new RiskAiService();
            var dummyRisks = new List<Risk>
            {
                new Risk { Category = "Environnemental", Description = "Sécheresse", Probability = 4, Impact = 4, MitigationMeasure = "Irrigation économe" },
                new Risk { Category = "Financier", Description = "Retard subvention", Probability = 2, Impact = 3, MitigationMeasure = "Fonds d'urgence" }
            };

            string report = await service.AuditRiskMatrixAsync(dummyRisks, 999);
            Assert.False(string.IsNullOrWhiteSpace(report));
            Assert.Contains("Risques", report, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RisquesViewModel_CriticalityProperties_UpdateDynamically()
        {
            var project = new Project { Id = 999, Title = "Projet Test" };
            var vm = new RisquesViewModel(project);

            vm.Probability = 5;
            vm.Impact = 4;
            Assert.Equal(20, vm.CriticalityScore);
            Assert.Contains("Critique", vm.CriticalityText);
            Assert.Equal("#EF4444", vm.CriticalityBrush);

            vm.Probability = 2;
            vm.Impact = 2;
            Assert.Equal(4, vm.CriticalityScore);
            Assert.Contains("Faible", vm.CriticalityText);
            Assert.Equal("#10B981", vm.CriticalityBrush);

            Assert.NotNull(vm.AutoGenerateRisksCommand);
            Assert.NotNull(vm.AuditRisksCommand);
            Assert.NotNull(vm.ProposerMitigationIaCommand);
            Assert.NotNull(vm.EstimerScoreIaCommand);
        }
    }
}

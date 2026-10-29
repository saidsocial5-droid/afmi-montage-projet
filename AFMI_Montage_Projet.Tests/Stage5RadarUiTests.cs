using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;
using AFMI_Montage_projet_2.ViewModels.Radar;

namespace AFMI_Montage_Projet.Tests
{
    public class Stage5RadarUiTests
    {
        [Fact]
        public void OpportunityItemViewModel_LanguageToggle_SwitchesFrAndAr()
        {
            var item = new RssItemCache
            {
                ExternalId = "test-lang-1",
                Title = "Titre Test",
                CleanDescription = "Description française par défaut",
                AiSummaryFr = "Résumé en français pour l'AFMI",
                AiSummaryAr = "ملخص بالعربية لجمعية أفمي الأطلس المتوسط",
                Donor = "GEF"
            };

            var vm = new OpportunityItemViewModel(item);

            // Par défaut: Français (LTR)
            Assert.False(vm.IsArabicView);
            Assert.Equal("Résumé en français pour l'AFMI", vm.DisplaySummary);
            Assert.Equal(FlowDirection.LeftToRight, vm.DisplaySummaryDirection);

            // Basculement vers l'Arabe (RTL)
            vm.ToggleSummaryLanguage();
            Assert.True(vm.IsArabicView);
            Assert.Equal("ملخص بالعربية لجمعية أفمي الأطلس المتوسط", vm.DisplaySummary);
            Assert.Equal(FlowDirection.RightToLeft, vm.DisplaySummaryDirection);

            // Re-basculement vers le Français
            vm.ToggleSummaryLanguage();
            Assert.False(vm.IsArabicView);
            Assert.Equal("Résumé en français pour l'AFMI", vm.DisplaySummary);
        }

        [Fact]
        public void OpportunityItemViewModel_DeadlineWarning_TriggersForShortDeadlines()
        {
            var nearItem = new RssItemCache
            {
                ExternalId = "near-1",
                Title = "Appel urgent",
                AiDeadline = DateTime.UtcNow.AddDays(7), // dans 7 jours (< 15)
                AiEstimatedBudget = 50000,
                AiCurrency = "EUR"
            };

            var farItem = new RssItemCache
            {
                ExternalId = "far-1",
                Title = "Appel lointain",
                AiDeadline = DateTime.UtcNow.AddDays(45), // dans 45 jours (> 15)
                AiEstimatedBudget = null
            };

            var nearVm = new OpportunityItemViewModel(nearItem);
            var farVm = new OpportunityItemViewModel(farItem);

            Assert.True(nearVm.IsDeadlineNear);
            Assert.Contains("50", nearVm.FormattedBudget);
            Assert.Contains("EUR", nearVm.FormattedBudget);

            Assert.False(farVm.IsDeadlineNear);
            Assert.Contains("Non spécifié", farVm.FormattedBudget);
        }

        [Fact]
        public void OpportunityItemViewModel_ScoreBadgeColoring_MatchesThresholds()
        {
            var highItem = new OpportunityItemViewModel(new RssItemCache { AiScore = 8.5 });
            var midItem = new OpportunityItemViewModel(new RssItemCache { AiScore = 6.2 });
            var lowItem = new OpportunityItemViewModel(new RssItemCache { AiScore = 3.0 });
            var unscoredItem = new OpportunityItemViewModel(new RssItemCache { AiScore = null });

            Assert.Equal("#10B981", highItem.ScoreColor); // Vert (≥ 8)
            Assert.Equal("#F59E0B", midItem.ScoreColor);  // Ambre (5-7)
            Assert.Equal("#94A3B8", lowItem.ScoreColor);  // Gris (< 5)
            Assert.Equal("#94A3B8", unscoredItem.ScoreColor); // Gris (non noté)
        }

        [Fact]
        public void ProjectRadarViewModel_ThematicChips_SetsAndClearsQuery()
        {
            var vm = new ProjectRadarViewModel();

            // Sélectionner le chip Cèdre
            vm.SelectThematicChip("🌲 Cèdre & Forêts");
            Assert.Equal("🌲 Cèdre & Forêts", vm.SelectedThematicChip);
            Assert.Equal("Cèdre", vm.SearchQuery);

            // Re-cliquer pour désélectionner
            vm.SelectThematicChip("🌲 Cèdre & Forêts");
            Assert.Equal(string.Empty, vm.SelectedThematicChip);
            Assert.Equal(string.Empty, vm.SearchQuery);
        }

        [Fact]
        public void ProjectRadarViewModel_MultiCriteriaFilters_ConjunctionAndClear()
        {
            var vm = new ProjectRadarViewModel();
            vm.AllOpportunities.Clear();

            // Ajouter 3 opportunités variées
            vm.AllOpportunities.Add(new OpportunityItemViewModel(new RssItemCache
            {
                ExternalId = "1",
                Title = "Préservation de la cédraie du Moyen Atlas",
                CleanDescription = "Subvention pour la conservation forestière",
                Donor = "GEF",
                FeedName = "GEF SGP",
                AiScore = 9.2,
                AiMatchedAxes = "Forêt et Cèdre"
            }));

            vm.AllOpportunities.Add(new OpportunityItemViewModel(new RssItemCache
            {
                ExternalId = "2",
                Title = "Projet Énergies Renouvelables Urbaines",
                CleanDescription = "Panneaux solaires urbains",
                Donor = "Union Européenne",
                FeedName = "UE Greendeal",
                AiScore = 6.0,
                AiMatchedAxes = "Climat"
            }));

            vm.AllOpportunities.Add(new OpportunityItemViewModel(new RssItemCache
            {
                ExternalId = "3",
                Title = "Gestion des zones humides et de l'eau",
                CleanDescription = "Restauration des lacs de montagne",
                Donor = "GEF",
                FeedName = "GEF SGP",
                AiScore = 7.5,
                AiMatchedAxes = "Eau"
            }));

            // Test 1: Filtrer par donateur GEF -> 2 résultats (1 et 3)
            vm.SelectedDonorFilter = "GEF";
            Assert.Equal(2, vm.FilteredOpportunities.Count);

            // Test 2: Ajouter filtre de note ≥ 8/10 -> 1 seul résultat (1)
            vm.SelectedScoreFilter = "≥ 8/10 (Très forte)";
            Assert.Single(vm.FilteredOpportunities);
            Assert.Equal("1", vm.FilteredOpportunities[0].ExternalId);

            // Test 3: ClearFilters réinitialise tout -> 3 résultats
            vm.ClearFilters();
            Assert.Equal(3, vm.FilteredOpportunities.Count);
            Assert.Equal("Tous", vm.SelectedDonorFilter);
            Assert.Equal("Tous", vm.SelectedScoreFilter);
        }

        [Fact]
        public void ProjectRadarViewModel_FeatureFlag_TogglesCleanly()
        {
            bool initial = ProjectRadarConfigService.IsEnabled;

            try
            {
                ProjectRadarConfigService.SetEnabled(false);
                Assert.False(ProjectRadarConfigService.IsEnabled);

                ProjectRadarConfigService.SetEnabled(true);
                Assert.True(ProjectRadarConfigService.IsEnabled);
            }
            finally
            {
                ProjectRadarConfigService.SetEnabled(initial);
            }
        }

        [Fact]
        public void ProjectRadarViewModel_RtlToggle_SwitchesFlowDirection()
        {
            var vm = new ProjectRadarViewModel();

            Assert.Equal(FlowDirection.LeftToRight, vm.FlowDirection);
            Assert.Equal("fr", vm.CurrentLanguage);

            vm.ToggleRtlDirection();
            Assert.Equal(FlowDirection.RightToLeft, vm.FlowDirection);
            Assert.Equal("ar", vm.CurrentLanguage);

            vm.ToggleRtlDirection();
            Assert.Equal(FlowDirection.LeftToRight, vm.FlowDirection);
            Assert.Equal("fr", vm.CurrentLanguage);
        }
    }
}

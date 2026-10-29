using System.Windows;
using System.Windows.Controls;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.Views.Strategic
{
    public partial class StrategicPlanningView : UserControl
    {
        public StrategicPlanningView()
        {
            InitializeComponent();
        }

        // --- Onglet 1 : Fiche d'idée & Contexte ---

        private void BtnEnrichTitle_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtIdeaTitle, TextAssistActionType.Enrichir);
        }

        private void BtnReformulateTitle_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtIdeaTitle, TextAssistActionType.Reformuler);
        }

        private void BtnEnhanceTitle_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtIdeaTitle, TextEnhancementMode.Ameliorer);
        }

        private void BtnSuggestContext_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtIdeaTitle, TextEnhancementMode.Generer);
        }

        private void BtnEnhanceDonor_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtDonorPriorities, TextEnhancementMode.Ameliorer);
        }

        private void BtnEnrichDonor_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtDonorPriorities, TextEnhancementMode.Enrichir);
        }

        private void BtnReformulateDonor_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtDonorPriorities, TextEnhancementMode.Reformuler);
        }

        // --- Onglet 5 : Problème central & Options ---

        private void BtnEnhanceProblem_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtCoreProblem, TextEnhancementMode.Ameliorer);
        }

        private void BtnEnrichProblem_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtCoreProblem, TextEnhancementMode.Enrichir);
        }

        private void BtnReformulateProblem_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtCoreProblem, TextEnhancementMode.Reformuler);
        }

        private void BtnEnhanceRationale_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtOptionRationale, TextEnhancementMode.Ameliorer);
        }

        private void BtnEnrichRationale_Click(object sender, RoutedEventArgs e)
        {
            AiTextAssistant.OpenEnhancementDialog(TxtOptionRationale, TextEnhancementMode.Enrichir);
        }
    }
}

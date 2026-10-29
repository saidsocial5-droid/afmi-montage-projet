using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.Views
{
    public partial class AiRiskGenerationDialog : Window
    {
        public ObservableCollection<RiskProposal> Proposals { get; }

        public List<RiskProposal> SelectedProposals => Proposals.Where(p => p.IsSelected).ToList();

        public AiRiskGenerationDialog(List<RiskProposal> proposals)
        {
            InitializeComponent();
            Proposals = new ObservableCollection<RiskProposal>(proposals);
            DgRisks.ItemsSource = Proposals;
            UpdateSummary();

            foreach (var p in Proposals)
            {
                p.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(RiskProposal.IsSelected))
                    {
                        UpdateSummary();
                    }
                };
            }
        }

        private void UpdateSummary()
        {
            int total = Proposals.Count;
            int selected = Proposals.Count(p => p.IsSelected);
            TxtCountSummary.Text = $"{selected} sur {total} sélectionné(s)";
            BtnImport.Content = $"📥 Importer les risques sélectionnés ({selected})";
            BtnImport.IsEnabled = selected > 0;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in Proposals) p.IsSelected = true;
            UpdateSummary();
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in Proposals) p.IsSelected = false;
            UpdateSummary();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

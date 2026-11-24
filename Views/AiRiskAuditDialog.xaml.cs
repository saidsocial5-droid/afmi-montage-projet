using System;
using System.Windows;

namespace AFMI_Montage_projet_2.Views
{
    public partial class AiRiskAuditDialog : Window
    {
        public AiRiskAuditDialog(string auditReportText, string? title = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(title))
            {
                Title = title;
            }
            TxtAuditContent.Text = auditReportText;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtAuditContent.Text);
                TxtCopyNotice.Text = "✅ Rapport copié avec succès dans le presse-papiers !";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de copier : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

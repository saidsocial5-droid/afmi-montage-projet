using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.Views.Radar
{
    public partial class AddEditFeedDialog : Window
    {
        public RssFeedConfig? CreatedFeed { get; private set; }

        public AddEditFeedDialog()
        {
            InitializeComponent();
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowTestResult(false, "Veuillez d'abord saisir une URL.");
                return;
            }

            // 1. SSRF Check
            var (validUrl, ssrfReason) = SsrfValidator.ValidateUrl(url);
            if (!validUrl)
            {
                ShowTestResult(false, $"URL bloquée par la sécurité (SSRF) : {ssrfReason}");
                return;
            }

            ShowTestResult(null, "Connexion et analyse du flux en cours... (une page web est aussi acceptée : ses flux sont recherchés automatiquement)");
            var testButton = sender as Button;
            if (testButton != null) testButton.IsEnabled = false;

            try
            {
                // Même chemin que l'actualisation : protection SSRF à chaque redirection, taille limitée, découverte automatique
                var result = await new RssFeedService().TestFeedAsync(url);
                if (result.Success && result.UrlChanged) UrlTextBox.Text = result.ResolvedUrl;
                if (result.Success && string.IsNullOrWhiteSpace(NameTextBox.Text))
                    NameTextBox.Text = string.IsNullOrWhiteSpace(result.FeedTitle) ? "Flux personnalisé" : result.FeedTitle;
                ShowTestResult(result.Success, result.Message);
            }
            catch (Exception ex)
            {
                ShowTestResult(false, $"Erreur lors de la connexion : {ex.Message}");
            }
            finally
            {
                if (testButton != null) testButton.IsEnabled = true;
            }
        }

        private void ShowTestResult(bool? success, string message)
        {
            TestResultBorder.Visibility = Visibility.Visible;
            TestResultText.Text = message;

            if (success == null)
            {
                TestResultBorder.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            }
            else if (success.Value)
            {
                TestResultBorder.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229));
                TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(6, 95, 70));
            }
            else
            {
                TestResultBorder.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text?.Trim() ?? string.Empty;
            string url = UrlTextBox.Text?.Trim() ?? string.Empty;
            string category = CategoryTextBox.Text?.Trim() ?? string.Empty;
            string topic = TopicTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Le nom du flux est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("L'URL du flux est obligatoire.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (validUrl, ssrfReason) = SsrfValidator.ValidateUrl(url);
            if (!validUrl)
            {
                MessageBox.Show($"URL invalide ou non sécurisée : {ssrfReason}", "Sécurité SSRF", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string lang = "fr";
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Content.ToString() ?? "";
                if (tag.Contains("(ar)")) lang = "ar";
                else if (tag.Contains("(en)")) lang = "en";
            }

            CreatedFeed = new RssFeedConfig
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = name,
                Url = url,
                Category = string.IsNullOrWhiteSpace(category) ? "Environnement & Climat" : category,
                Topic = string.IsNullOrWhiteSpace(topic) ? "Subventions & Appels à projets" : topic,
                Language = lang,
                Enabled = true,
                IsDefault = false,
                Verified = false
            };

            ProjectRadarConfigService.AddCustomFeed(CreatedFeed);
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

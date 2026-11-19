using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Updates;

namespace AFMI_Montage_projet_2.Views
{
    /// <summary>
    /// Invitation à activer les mises à jour automatiques sur ce poste (ADR 29) : le dépôt est privé, chaque poste
    /// a besoin de sa clé d'accès GitHub en lecture seule. Affichée au démarrage tant que la clé manque ou est refusée,
    /// sauf si l'utilisateur coche « Ne plus me le demander ». La clé est saisie dans un PasswordBox, chiffrée (DPAPI)
    /// et testée immédiatement auprès de GitHub.
    /// </summary>
    public class GitHubTokenWindow : Window
    {
        public const string CreateTokenUrl = "https://github.com/settings/personal-access-tokens/new";

        private readonly PasswordBox _tokenBox = new() { Height = 30, Padding = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), FontSize = 12 };
        private readonly CheckBox _dontAsk = new() { Content = "Ne plus me le demander sur ce poste", Margin = new Thickness(0, 12, 0, 0), FontSize = 12 };
        private readonly Button _save = new() { Content = "Enregistrer et vérifier", Padding = new Thickness(14, 6, 14, 6), IsDefault = true };

        /// <summary>Résultat de la dernière vérification réussie (null si la clé n'a pas été validée).</summary>
        public UpdateCheckResult? ValidatedCheck { get; private set; }

        public GitHubTokenWindow(string repository, bool hadToken)
        {
            Title = "Activer les mises à jour automatiques";
            Width = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = hadToken ? "🔑 La clé d'accès de ce poste n'est plus acceptée par GitHub" : "🔄 Activez les mises à jour automatiques",
                FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), Margin = new Thickness(0, 0, 0, 10)
            });
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                Text = $"Les nouvelles versions sont publiées sur le dépôt privé GitHub « {repository} ». " +
                       "Pour que ce poste les reçoive et les installe tout seul, il lui faut une clé d'accès en lecture seule :\n\n" +
                       "1. Cliquez sur « Créer la clé sur GitHub » (connectez-vous avec le compte propriétaire du dépôt).\n" +
                       "2. Repository access : « Only select repositories », puis ce dépôt.\n" +
                       "3. Permissions › Repository permissions › Contents : « Read-only ». Choisissez une longue durée de validité.\n" +
                       "4. Générez la clé, copiez-la (elle commence par github_pat_) et collez-la ci-dessous.\n\n" +
                       "La clé est chiffrée sur ce poste uniquement et n'est envoyée qu'à api.github.com."
            });

            var link = new Button { Content = "Créer la clé sur GitHub ↗", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 12, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
            link.Click += (_, _) => OpenCreateTokenPage();
            panel.Children.Add(link);

            panel.Children.Add(new TextBlock { Text = "Clé d'accès GitHub", FontSize = 11, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 4) });
            panel.Children.Add(_tokenBox);
            panel.Children.Add(_status);
            panel.Children.Add(_dontAsk);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var later = new Button { Content = "Plus tard", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0) };
            later.Click += (_, _) => Close();
            // Fermeture par « Plus tard » ou la croix : on retient seulement le choix « Ne plus me le demander »
            Closing += (_, _) =>
            {
                if (ValidatedCheck == null && _dontAsk.IsChecked == true) AutoUpdateCoordinator.TokenPromptEnabled = false;
            };
            _save.Click += async (_, _) => await SaveAndVerifyAsync();
            buttons.Children.Add(later);
            buttons.Children.Add(_save);
            panel.Children.Add(buttons);

            Content = panel;
        }

        /// <summary>
        /// Affiche l'invitation si elle est permise sur ce poste. Renvoie le résultat de vérification obtenu avec la
        /// nouvelle clé (pour enchaîner sur la mise à jour), ou null si l'utilisateur a reporté.
        /// </summary>
        public static Task<UpdateCheckResult?> PromptAsync(Window? owner, bool force = false)
        {
            if (!force && !AutoUpdateCoordinator.TokenPromptEnabled) return Task.FromResult<UpdateCheckResult?>(null);
            var window = new GitHubTokenWindow(GitHubUpdateService.Repository, GitHubUpdateService.HasAccessToken);
            if (owner != null && owner.IsLoaded) window.Owner = owner;
            window.ShowDialog();
            return Task.FromResult(window.ValidatedCheck);
        }

        private async Task SaveAndVerifyAsync()
        {
            string token = _tokenBox.Password.Trim();
            if (!GitHubUpdateService.LooksLikeToken(token))
            {
                ShowStatus("Clé non reconnue : elle commence par github_pat_ (ou ghp_). Copiez-la en entier depuis GitHub.", error: true);
                return;
            }

            _save.IsEnabled = false;
            ShowStatus("Vérification auprès de GitHub…", error: false);
            GitHubUpdateService.SaveAccessToken(token);
            _tokenBox.Clear();
            try
            {
                var check = await new GitHubUpdateService().CheckAsync();
                if (check.NeedsAccessToken)
                {
                    ShowStatus("GitHub refuse cette clé pour ce dépôt. Vérifiez qu'elle donne accès au dépôt avec Contents en lecture, puis recollez-la.", error: true);
                    return;
                }
                ValidatedCheck = check;
                AutoUpdateCoordinator.TokenPromptEnabled = true;
                ShowStatus("✅ Clé acceptée : les mises à jour sont activées sur ce poste. " + check.Message, error: false);
                await Task.Delay(1500);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(GitHubTokenWindow), "Vérification de la clé GitHub impossible.", ex);
                ShowStatus($"Clé enregistrée, mais la vérification a échoué ({ex.Message}). Elle sera retentée au prochain démarrage.", error: true);
            }
            finally
            {
                _save.IsEnabled = true;
            }
        }

        private void ShowStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Foreground = error ? new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)) : new SolidColorBrush(Color.FromRgb(0x04, 0x78, 0x57));
        }

        private static void OpenCreateTokenPage()
        {
            try { Process.Start(new ProcessStartInfo(CreateTokenUrl) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                AppLogger.Warn(nameof(GitHubTokenWindow), "Ouverture de la page GitHub impossible.", ex);
            }
        }
    }
}

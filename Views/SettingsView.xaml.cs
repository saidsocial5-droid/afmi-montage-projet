using System.Windows;
using System.Windows.Controls;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        // PasswordBox non liable : la clé d'accès GitHub est transmise au ViewModel puis effacée du champ
        private void SaveToken_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm) vm.SaveGitHubToken(TokenBox.Password);
            TokenBox.Clear();
        }
    }
}

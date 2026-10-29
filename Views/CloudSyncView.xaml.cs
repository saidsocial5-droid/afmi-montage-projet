using System.Windows;
using System.Windows.Controls;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class CloudSyncView : UserControl
    {
        public CloudSyncView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => LoadPasswordFromViewModel();
            Loaded += (_, _) => LoadPasswordFromViewModel();
        }

        // PasswordBox.Password n'est pas une DependencyProperty : synchronisation manuelle avec le ViewModel.
        private void LoadPasswordFromViewModel()
        {
            if (DataContext is CloudSyncViewModel vm && DbPasswordBox.Password != vm.Settings.Password)
            {
                DbPasswordBox.Password = vm.Settings.Password;
            }
        }

        private void DbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is CloudSyncViewModel vm)
            {
                vm.Settings.Password = DbPasswordBox.Password;
            }
        }
    }
}

using System.Windows;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class PairingWindow : Window
    {
        public PairingWindow(PairingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.OnCloseRequested = () => this.Close();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is PairingViewModel vm)
            {
                await vm.StartPairingAsync();
            }
        }
    }
}

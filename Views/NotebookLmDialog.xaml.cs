using System.Windows;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class NotebookLmDialog : Window
    {
        public NotebookLmDialog(OrganizationProfileViewModel parentViewModel)
        {
            InitializeComponent();
            var vm = new NotebookLmViewModel(parentViewModel);
            vm.RequestClose += () => Close();
            DataContext = vm;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

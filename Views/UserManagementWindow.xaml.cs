using System.Windows;
using System.Windows.Controls;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class UserManagementWindow : Window
    {
        public UserManagementWindow()
        {
            InitializeComponent();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is UserManagementViewModel vm && sender is ListView lv)
            {
                vm.SelectUserCommand.Execute(lv.SelectedItem as User);
            }
        }
    }
}

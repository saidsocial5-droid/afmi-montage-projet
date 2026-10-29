using System.Windows;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
            var vm = (RegisterViewModel)DataContext;
            vm.OnRegisterSuccess = () => 
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            };
            vm.OnCancel = () =>
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            };
        }
    }
}

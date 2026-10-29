using System.Windows;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            
            if (DataContext is LoginViewModel vm)
            {
                vm.OnLoginSuccess = () =>
                {
                    var mainWindow = new MainWindow();
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                    this.Close();
                };
                vm.OnNavigateRegister = () =>
                {
                    var registerWindow = new RegisterWindow();
                    registerWindow.Show();
                    this.Close();
                };
            }
        }
    }
}

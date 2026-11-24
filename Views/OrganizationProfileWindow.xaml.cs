using System.Windows;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class OrganizationProfileWindow : Window
    {
        public OrganizationProfileWindow(Project? activeProject = null)
        {
            InitializeComponent();
            DataContext = new OrganizationProfileViewModel(activeProject);
        }
    }
}

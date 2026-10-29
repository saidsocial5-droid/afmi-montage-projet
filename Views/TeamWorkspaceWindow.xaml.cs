using System;
using System.Windows;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class TeamWorkspaceWindow : Window
    {
        public TeamWorkspaceWindow(Action? onProjectsChanged = null)
        {
            InitializeComponent();
            var vm = new TeamWorkspaceViewModel(onProjectsChanged);
            DataContext = vm;
            Closed += (s, e) => vm.Dispose();
        }
    }
}

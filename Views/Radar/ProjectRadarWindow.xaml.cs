using System;
using System.Windows;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.ViewModels.Radar;

namespace AFMI_Montage_projet_2.Views.Radar
{
    public partial class ProjectRadarWindow : Window
    {
        public ProjectRadarWindow() : this(null)
        {
        }

        public ProjectRadarWindow(Action<Project>? onOpenProjectRequested)
        {
            InitializeComponent();
            DataContext = new ProjectRadarViewModel(onOpenProjectRequested: onOpenProjectRequested);
        }
    }
}

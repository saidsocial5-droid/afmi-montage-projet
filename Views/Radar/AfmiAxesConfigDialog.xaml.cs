using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AFMI_Montage_projet_2.Models.Radar;
using AFMI_Montage_projet_2.Services.Radar;

namespace AFMI_Montage_projet_2.Views.Radar
{
    public partial class AfmiAxesConfigDialog : Window
    {
        public ObservableCollection<AfmiAxis> Items { get; } = new();

        public AfmiAxesConfigDialog()
        {
            InitializeComponent();
            LoadData();
            AxesDataGrid.ItemsSource = Items;
        }

        private void LoadData()
        {
            var axes = ProjectRadarConfigService.LoadAxes();
            Items.Clear();
            foreach (var a in axes)
            {
                Items.Add(new AfmiAxis
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    IsCompleted = a.IsCompleted
                });
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var list = Items.ToList();
                ProjectRadarConfigService.SaveAxes(list);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la sauvegarde : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

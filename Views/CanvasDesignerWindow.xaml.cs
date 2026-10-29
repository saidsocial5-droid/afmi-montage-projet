using System.Windows;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class CanvasDesignerWindow : Window
    {
        public CanvasDesignerViewModel ViewModel { get; }

        public CanvasDesignerWindow(CanvasDesignerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            viewModel.RequestClose += () =>
            {
                DialogResult = true;
                Close();
            };
        }

        public CanvasDesignerWindow() : this(new CanvasDesignerViewModel())
        {
        }

        private void Annuler_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

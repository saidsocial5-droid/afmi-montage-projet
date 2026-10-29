using System.Windows;

namespace AFMI_Montage_projet_2.Views
{
    public partial class IdeationInnovationWindow : Window
    {
        public IdeationInnovationWindow()
        {
            InitializeComponent();
        }

        public IdeationInnovationWindow(string initialTitle, string initialContext, string initialObjective = "") : this()
        {
            DataContext = new ViewModels.IdeationInnovationViewModel(initialTitle, initialContext, initialObjective);
        }
    }
}

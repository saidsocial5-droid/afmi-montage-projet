using System.Windows.Controls;
using System.Windows.Threading;
using AFMI_Montage_projet_2.ViewModels.Strategic;

namespace AFMI_Montage_projet_2.Views.Strategic
{
    public partial class TerritoryView : UserControl
    {
        public TerritoryView()
        {
            InitializeComponent();
        }

        /// <summary>Totaux (effectifs, part des femmes) recalculés après la saisie d'une cellule.</summary>
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is not TerritoryViewModel vm) return;
            vm.HasUnsavedChanges = true;
            Dispatcher.BeginInvoke(vm.RefreshTotals, DispatcherPriority.Background);
        }
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AFMI_Montage_projet_2.Converters
{
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                // Si count == 0, on retourne Visible (pour afficher le message d'absence de projet)
                // Sinon, on retourne Collapsed.
                // Si le paramètre est "Inverse", on fait le contraire.
                bool isInverse = parameter?.ToString() == "Inverse";
                
                if (isInverse)
                {
                    return count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

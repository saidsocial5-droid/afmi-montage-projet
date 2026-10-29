using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AFMI_Montage_projet_2.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNotEmpty = value != null && !string.IsNullOrWhiteSpace(value.ToString());
            
            if (parameter is string param && (param.Equals("Inverse", StringComparison.OrdinalIgnoreCase) || param.Equals("Invert", StringComparison.OrdinalIgnoreCase)))
            {
                return isNotEmpty ? Visibility.Collapsed : Visibility.Visible;
            }

            return isNotEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

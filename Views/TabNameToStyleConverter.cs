using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AFMI_Montage_projet_2.Views
{
    public class TabNameToStyleConverter : IValueConverter
    {
        public string ActiveTabName { get; set; } = string.Empty;
        public Style? ActiveStyle { get; set; }
        public Style? InactiveStyle { get; set; }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentTab && currentTab == ActiveTabName)
            {
                return ActiveStyle;
            }
            return InactiveStyle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

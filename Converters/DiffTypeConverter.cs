using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AFMI_Montage_projet_2.Services.Collab;

namespace AFMI_Montage_projet_2.Converters
{
    public class DiffTypeToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DiffType type)
            {
                return type switch
                {
                    DiffType.Added => new SolidColorBrush(Color.FromRgb(22, 163, 74)),    // #16A34A Green
                    DiffType.Deleted => new SolidColorBrush(Color.FromRgb(220, 38, 38)),  // #DC2626 Red
                    _ => new SolidColorBrush(Color.FromRgb(51, 65, 85))                   // #334155 Slate
                };
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DiffTypeToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DiffType type)
            {
                return type switch
                {
                    DiffType.Added => new SolidColorBrush(Color.FromArgb(50, 34, 197, 94)),   // Light Green
                    DiffType.Deleted => new SolidColorBrush(Color.FromArgb(50, 239, 68, 68)), // Light Red
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DiffTypeToTextDecorationConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DiffType type && type == DiffType.Deleted)
            {
                return TextDecorations.Strikethrough;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

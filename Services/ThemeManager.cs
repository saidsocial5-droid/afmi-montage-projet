using System;
using System.Windows;
using System.Windows.Media;

namespace AFMI_Montage_projet_2.Services
{
    public static class ThemeManager
    {
        public static void Initialize()
        {
            ApplyTypography();
            SettingsService.OnSettingChanged += (key, value) => 
            {
                if (key.StartsWith("Font_")) ApplyTypography();
            };
        }

        public static void ApplyTypography()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Retrieve settings or defaults
                string baseFont = SettingsService.GetSetting("Font_Base", "Segoe UI");
                string headingFont = SettingsService.GetSetting("Font_Heading", "Segoe UI");
                string arabicFont = SettingsService.GetSetting("Font_Arabic", "Tahoma");
                
                string fontSizeSetting = SettingsService.GetSetting("Font_Size", "Normal");
                double baseSize = 14.0;
                if (fontSizeSetting == "Petit") baseSize = 12.0;
                else if (fontSizeSetting == "Grand") baseSize = 16.0;
                else if (fontSizeSetting == "Très grand") baseSize = 18.0;

                // Update Application Resources
                Application.Current.Resources["BaseFontFamily"] = new FontFamily(baseFont);
                Application.Current.Resources["HeadingFontFamily"] = new FontFamily(headingFont);
                Application.Current.Resources["ArabicFontFamily"] = new FontFamily(arabicFont);
                
                Application.Current.Resources["BaseFontSize"] = baseSize;
                Application.Current.Resources["HeadingFontSize"] = baseSize + 4.0;
                Application.Current.Resources["SmallFontSize"] = baseSize - 2.0;
            });
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Helpers
{
    public static class CollaborativeUI
    {
        public static readonly DependencyProperty IsCollaborativeTextProperty =
            DependencyProperty.RegisterAttached("IsCollaborativeText", typeof(bool), typeof(CollaborativeUI), new PropertyMetadata(false, OnIsCollaborativeTextChanged));

        public static void SetIsCollaborativeText(DependencyObject element, bool value) => element.SetValue(IsCollaborativeTextProperty, value);
        public static bool GetIsCollaborativeText(DependencyObject element) => (bool)element.GetValue(IsCollaborativeTextProperty);

        public static readonly DependencyProperty TextPropertyNameProperty =
            DependencyProperty.RegisterAttached("TextPropertyName", typeof(string), typeof(CollaborativeUI), new PropertyMetadata("Description", OnIsCollaborativeTextChanged));

        public static void SetTextPropertyName(DependencyObject element, string value) => element.SetValue(TextPropertyNameProperty, value);
        public static string GetTextPropertyName(DependencyObject element) => (string)element.GetValue(TextPropertyNameProperty);

        private static void OnIsCollaborativeTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                textBlock.DataContextChanged -= TextBlock_DataContextChanged;
                textBlock.DataContextChanged += TextBlock_DataContextChanged;
                UpdateTextBlock(textBlock);
            }
        }

        private static void TextBlock_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateTextBlock((TextBlock)sender);
        }

        private static void UpdateTextBlock(TextBlock textBlock)
        {
            if (GetIsCollaborativeText(textBlock) && textBlock.DataContext is BaseEntity entity)
            {
                string propName = GetTextPropertyName(textBlock);
                var propInfo = entity.GetType().GetProperty(propName);
                if (propInfo != null)
                {
                    string text = propInfo.GetValue(entity) as string ?? string.Empty;
                    
                    if (entity.ContributionStatus == "Pending")
                    {
                        string author = string.IsNullOrWhiteSpace(entity.AuthorName) ? "Membre" : entity.AuthorName;
                        textBlock.Text = $"'{text}' ({author})";
                        textBlock.Text = textBlock.Text.Replace("'", ""); // Fix quotes safely

                        try
                        {
                            if (!string.IsNullOrEmpty(entity.AuthorColor))
                            {
                                var converter = new BrushConverter();
                                var brush = (Brush)converter.ConvertFromString(entity.AuthorColor);
                                textBlock.Foreground = brush;
                            }
                            else
                            {
                                textBlock.Foreground = Brushes.Orange;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        textBlock.Text = text;
                        // Reset to original color
                        textBlock.Foreground = (Brush)new BrushConverter().ConvertFromString("#334155");
                    }
                }
            }
        }
    }
}

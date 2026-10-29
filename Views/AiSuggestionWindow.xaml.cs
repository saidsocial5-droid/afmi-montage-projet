using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AFMI_Montage_projet_2.Views
{
    public partial class AiSuggestionWindow : Window
    {
        public string SelectedText { get; private set; } = string.Empty;
        private List<AFMI_Montage_projet_2.Services.AiSuggestion> _suggestions;

        public AiSuggestionWindow(string title, List<AFMI_Montage_projet_2.Services.AiSuggestion> suggestions)
        {
            InitializeComponent();
            Title = title;
            _suggestions = suggestions;

            foreach (var suggestion in suggestions)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
                
                var cb = new CheckBox
                {
                    Content = new TextBlock { Text = suggestion.Text, TextWrapping = TextWrapping.Wrap },
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                    FontSize = 13,
                    IsChecked = false,
                    Tag = suggestion
                };
                
                sp.Children.Add(cb);

                if (suggestion.NeedsInput)
                {
                    var tb = new TextBox
                    {
                        Margin = new Thickness(20, 5, 0, 0),
                        Padding = new Thickness(5),
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        MinHeight = 40,
                        Tag = suggestion
                    };
                    
                    // Simple placeholder simulation (pour WPF standard)
                    tb.Text = suggestion.Placeholder;
                    tb.Foreground = Brushes.Gray;
                    tb.GotFocus += (s, e) => { if (tb.Text == suggestion.Placeholder) { tb.Text = ""; tb.Foreground = Brushes.Black; } };
                    tb.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(tb.Text)) { tb.Text = suggestion.Placeholder; tb.Foreground = Brushes.Gray; } };
                    
                    sp.Children.Add(tb);
                }

                SpSuggestions.Children.Add(sp);
            }
        }

        private void BtnIntegrate_Click(object sender, RoutedEventArgs e)
        {
            var paragraphParts = new List<string>();

            foreach (var child in SpSuggestions.Children)
            {
                if (child is StackPanel sp)
                {
                    var cb = sp.Children.OfType<CheckBox>().FirstOrDefault();
                    var tb = sp.Children.OfType<TextBox>().FirstOrDefault();

                    if (cb != null && cb.IsChecked == true)
                    {
                        var suggestion = cb.Tag as AFMI_Montage_projet_2.Services.AiSuggestion;
                        if (suggestion != null)
                        {
                            if (suggestion.NeedsInput && tb != null && tb.Text != suggestion.Placeholder && !string.IsNullOrWhiteSpace(tb.Text))
                            {
                                paragraphParts.Add($"{suggestion.Text} : {tb.Text.Trim()}");
                            }
                            else if (!suggestion.NeedsInput)
                            {
                                paragraphParts.Add(suggestion.Text);
                            }
                        }
                    }
                }
            }

            if (paragraphParts.Any())
            {
                SelectedText = "- " + string.Join("\n- ", paragraphParts);
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

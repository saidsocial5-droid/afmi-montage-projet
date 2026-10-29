using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_projet_2.Views
{
    public partial class FormulaireDynamiqueView : UserControl
    {
        private FormulaireDynamiqueViewModel? _currentVm;

        public FormulaireDynamiqueView()
        {
            InitializeComponent();
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_currentVm != null)
            {
                _currentVm.PropertyChanged -= Vm_PropertyChanged;
            }

            if (e.NewValue is FormulaireDynamiqueViewModel vm)
            {
                _currentVm = vm;
                vm.PropertyChanged += Vm_PropertyChanged;
            }
            else
            {
                _currentVm = null;
            }

            GenererFormulaireDynamique();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FormulaireDynamiqueViewModel.CurrentInstance) ||
                e.PropertyName == nameof(FormulaireDynamiqueViewModel.HasInstance) ||
                e.PropertyName == nameof(FormulaireDynamiqueViewModel.Answers))
            {
                Dispatcher.Invoke(GenererFormulaireDynamique);
            }
        }

        private void GenererFormulaireDynamique()
        {
            DynamicFormContainer.Children.Clear();

            if (DataContext is not FormulaireDynamiqueViewModel vm || vm.CurrentInstance == null || vm.SelectedTemplate == null)
            {
                return;
            }

            try
            {
                string schemaJson = vm.SelectedTemplate.JsonSchema ?? vm.CurrentInstance.FormTemplate?.JsonSchema ?? "[]";
                using var doc = JsonDocument.Parse(schemaJson);
                var sections = doc.RootElement;

                if (sections.ValueKind != JsonValueKind.Array) return;

                int sectionNum = 1;
                foreach (var section in sections.EnumerateArray())
                {
                    string sectionTitle = section.TryGetProperty("Section", out var secProp) 
                        ? secProp.GetString() ?? $"Section {sectionNum}" 
                        : $"Section {sectionNum}";

                    // 1. Bandeau de titre de section
                    var sectionBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 8, 12, 8),
                        Margin = new Thickness(0, sectionNum > 1 ? 20 : 5, 0, 12)
                    };

                    var titleBlock = new TextBlock
                    {
                        Text = $"📁  {sectionTitle.ToUpperInvariant()}",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
                    };
                    sectionBorder.Child = titleBlock;
                    DynamicFormContainer.Children.Add(sectionBorder);

                    // 2. Questions de la section
                    if (section.TryGetProperty("Fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
                    {
                        int qNum = 1;
                        foreach (var field in fields.EnumerateArray())
                        {
                            string fieldName = field.TryGetProperty("Name", out var n) ? n.GetString() ?? $"Field_{sectionNum}_{qNum}" : $"Field_{sectionNum}_{qNum}";
                            string labelText = field.TryGetProperty("Label", out var l) ? l.GetString() ?? $"Question {qNum}" : $"Question {qNum}";
                            string fieldType = field.TryGetProperty("Type", out var t) ? t.GetString() ?? "TextLong" : "TextLong";
                            string placeholder = field.TryGetProperty("Placeholder", out var p) ? p.GetString() ?? "" : "";

                            if (!vm.Answers.ContainsKey(fieldName))
                            {
                                vm.Answers[fieldName] = string.Empty;
                            }

                            string currentAnswer = vm.Answers.TryGetValue(fieldName, out var ansVal) ? ansVal : string.Empty;

                            // Carte individuelle pour chaque Question + Réponse
                            var card = new Border
                            {
                                Background = Brushes.White,
                                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(6),
                                Padding = new Thickness(14, 12, 14, 12),
                                Margin = new Thickness(0, 0, 0, 10)
                            };

                            var cardStack = new StackPanel();

                            // Ligne Question + Bouton IA
                            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                            var labelBlock = new TextBlock
                            {
                                Text = $"❓ {labelText}",
                                FontWeight = FontWeights.SemiBold,
                                FontSize = 12.5,
                                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                                TextWrapping = TextWrapping.Wrap,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            Grid.SetColumn(labelBlock, 0);
                            headerGrid.Children.Add(labelBlock);

                            // Bouton d'assistance IA pour cette question
                            if (fieldType != "Checkbox")
                            {
                                var iaBtn = new Button
                                {
                                    Content = "✨ Assister / Rédiger (IA)",
                                    Margin = new Thickness(10, 0, 0, 0),
                                    Padding = new Thickness(10, 4, 10, 4),
                                    Height = 28,
                                    Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
                                    Foreground = Brushes.White,
                                    FontSize = 11,
                                    FontWeight = FontWeights.SemiBold,
                                    BorderThickness = new Thickness(0),
                                    Cursor = System.Windows.Input.Cursors.Hand,
                                    ToolTip = "Demander à l'IA de rédiger ou perfectionner la réponse à cette question"
                                };
                                iaBtn.Resources.Add(typeof(Border), new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(4)) } });
                                iaBtn.Command = vm.AideRedactionIaCommand;
                                iaBtn.CommandParameter = fieldName;

                                Grid.SetColumn(iaBtn, 1);
                                headerGrid.Children.Add(iaBtn);
                            }

                            cardStack.Children.Add(headerGrid);

                            // Zone de réponse directement sous la question
                            if (fieldType == "Dropdown")
                            {
                                var combo = new ComboBox
                                {
                                    Height = 34,
                                    VerticalContentAlignment = VerticalAlignment.Center,
                                    Margin = new Thickness(0, 2, 0, 0)
                                };

                                if (field.TryGetProperty("Options", out var options) && options.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var opt in options.EnumerateArray())
                                    {
                                        combo.Items.Add(opt.GetString());
                                    }
                                }

                                if (!string.IsNullOrEmpty(currentAnswer))
                                {
                                    combo.SelectedItem = currentAnswer;
                                }

                                combo.SelectionChanged += (s, ev) =>
                                {
                                    if (combo.SelectedItem != null)
                                    {
                                        vm.Answers[fieldName] = combo.SelectedItem.ToString() ?? "";
                                    }
                                };

                                cardStack.Children.Add(combo);
                            }
                            else if (fieldType == "Checkbox")
                            {
                                var check = new CheckBox
                                {
                                    Content = "Cocher si cette exigence est satisfaite / applicable",
                                    FontWeight = FontWeights.Medium,
                                    Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                                    Margin = new Thickness(0, 4, 0, 0)
                                };

                                bool.TryParse(currentAnswer, out bool isChecked);
                                check.IsChecked = isChecked;

                                check.Checked += (s, ev) => vm.Answers[fieldName] = "True";
                                check.Unchecked += (s, ev) => vm.Answers[fieldName] = "False";

                                cardStack.Children.Add(check);
                            }
                            else if (fieldType == "TextLong")
                            {
                                var txtBox = new TextBox
                                {
                                    TextWrapping = TextWrapping.Wrap,
                                    AcceptsReturn = true,
                                    Height = 110,
                                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                    Padding = new Thickness(8),
                                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                                    Text = currentAnswer,
                                    Margin = new Thickness(0, 2, 0, 0)
                                };

                                txtBox.TextChanged += (s, ev) =>
                                {
                                    vm.Answers[fieldName] = txtBox.Text;
                                };

                                cardStack.Children.Add(txtBox);
                            }
                            else // TextShort
                            {
                                var txtBox = new TextBox
                                {
                                    Height = 34,
                                    VerticalContentAlignment = VerticalAlignment.Center,
                                    Padding = new Thickness(8, 0, 8, 0),
                                    BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                                    Text = currentAnswer,
                                    Margin = new Thickness(0, 2, 0, 0)
                                };

                                txtBox.TextChanged += (s, ev) =>
                                {
                                    vm.Answers[fieldName] = txtBox.Text;
                                };

                                cardStack.Children.Add(txtBox);
                            }

                            card.Child = cardStack;
                            DynamicFormContainer.Children.Add(card);

                            qNum++;
                        }
                    }

                    sectionNum++;
                }
            }
            catch (Exception ex)
            {
                var errBlock = new TextBlock
                {
                    Text = $"Erreur lors de la génération visuelle du canevas : {ex.Message}",
                    Foreground = Brushes.Red,
                    FontWeight = FontWeights.Bold
                };
                DynamicFormContainer.Children.Add(errBlock);
            }
        }
    }
}

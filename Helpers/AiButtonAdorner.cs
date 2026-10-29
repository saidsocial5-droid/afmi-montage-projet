using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Helpers
{
    /// <summary>
    /// Adorner WPF affichant une mini-barre d'outils flottante discrète et élégante
    /// en haut à droite de chaque zone de texte avec les boutons visibles :
    /// [ 📝 Enrichir ] [ 🔄 Reformuler ] [ 🔍 Détailler ] [ ✨ IA ]
    /// </summary>
    public class AiButtonAdorner : Adorner
    {
        private readonly Border _container;
        private readonly Button _btnEnrichir;
        private readonly Button _btnReformuler;
        private readonly Button _btnDetailler;
        private readonly Button _btnMore;
        private readonly VisualCollection _visuals;

        public AiButtonAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _visuals = new VisualCollection(this);

            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            DateTime lastClickTime = DateTime.MinValue;
            Action Throttle(Action action) => () =>
            {
                if (DateTime.UtcNow - lastClickTime < TimeSpan.FromMilliseconds(400)) return;
                lastClickTime = DateTime.UtcNow;
                action();
            };

            // 1. Bouton Enrichir
            _btnEnrichir = CreateActionButton(
                "📝 Enrichir",
                "#0284C7",
                "#E0F2FE",
                "Enrichir le texte avec le contexte du projet (Ctrl+Maj+E)",
                Throttle(() =>
                {
                    if (AdornedElement is TextBoxBase tb)
                    {
                        AiTextAssistant.OpenEnhancementDialog(tb, TextAssistActionType.Enrichir);
                    }
                }));
            sp.Children.Add(_btnEnrichir);

            // 2. Bouton Reformuler
            _btnReformuler = CreateActionButton(
                "🔄 Reformuler",
                "#059669",
                "#ECFDF5",
                "Reformuler selon les normes des bailleurs de fonds (Ctrl+Maj+R)",
                Throttle(() =>
                {
                    if (AdornedElement is TextBoxBase tb)
                    {
                        AiTextAssistant.OpenEnhancementDialog(tb, TextAssistActionType.Reformuler);
                    }
                }));
            sp.Children.Add(_btnReformuler);

            // 3. Bouton Détailler
            _btnDetailler = CreateActionButton(
                "🔍 Détailler",
                "#475569",
                "#F1F5F9",
                "Détailler en étapes et sous-composantes (Ctrl+Maj+D)",
                Throttle(() =>
                {
                    if (AdornedElement is TextBoxBase tb)
                    {
                        AiTextAssistant.OpenEnhancementDialog(tb, TextAssistActionType.Detailler);
                    }
                }));
            sp.Children.Add(_btnDetailler);

            // 4. Bouton menu options avancées ✨ IA
            _btnMore = CreateActionButton(
                "✨ IA",
                "#7C3AED",
                "#EDE9FE",
                "Assistant Rédactionnel complet (Ctrl+Alt+A)",
                Throttle(() =>
                {
                    if (AdornedElement is TextBoxBase tb)
                    {
                        AiTextAssistant.OpenEnhancementDialog(tb, TextAssistActionType.Enrichir);
                    }
                }));
            sp.Children.Add(_btnMore);

            _container = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255)), // Blanc presque opaque
                BorderBrush = new SolidColorBrush(Color.FromArgb(220, 203, 213, 225)), // Gris clair #CBD5E1
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(2, 1, 2, 1),
                Opacity = 0.95,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 5,
                    Color = Color.FromRgb(100, 116, 139),
                    Opacity = 0.25,
                    ShadowDepth = 1
                },
                Child = sp
            };

            _container.MouseEnter += (s, e) => _container.Opacity = 1.0;
            _container.MouseLeave += (s, e) => _container.Opacity = 0.95;

            _visuals.Add(_container);
        }

        private static Button CreateActionButton(string text, string hexFg, string hexBg, string tooltip, Action onClick)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new BrushConverter().ConvertFromString(hexFg) as Brush ?? Brushes.Black,
                Background = new BrushConverter().ConvertFromString(hexBg) as Brush ?? Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(1, 0, 1, 0),
                ToolTip = tooltip
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;
            btn.Template = template;

            // Déclenchement fiable via PreviewMouseLeftButtonDown pour ne pas être avalé par TextBoxBase
            btn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            btn.Click += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            return btn;
        }

        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index) => _visuals[index];

        protected override Size MeasureOverride(Size constraint)
        {
            _container.Measure(constraint);
            return _container.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (AdornedElement is TextBoxBase tb && (tb.IsReadOnly || !tb.IsEnabled || AiTextAssistant.GetIsExcluded(tb)))
            {
                _container.Visibility = Visibility.Collapsed;
                return finalSize;
            }

            // Sécurité anti-chevauchement : si le contrôle est étroit (< 130px) ou petit (< 22px de haut),
            // masquer complètement l'adorner pour ne jamais recouvrir les chiffres, devises ou dates
            if (finalSize.Width < 130 || finalSize.Height < 22)
            {
                _container.Visibility = Visibility.Collapsed;
                return finalSize;
            }

            _container.Visibility = Visibility.Visible;

            // Ajustement réactif selon la largeur disponible dans la zone de texte
            if (finalSize.Width < 280)
            {
                _btnDetailler.Visibility = Visibility.Collapsed;
            }
            else
            {
                _btnDetailler.Visibility = Visibility.Visible;
            }

            if (finalSize.Width < 210)
            {
                _btnReformuler.Visibility = Visibility.Collapsed;
            }
            else
            {
                _btnReformuler.Visibility = Visibility.Visible;
            }

            if (finalSize.Width < 160)
            {
                _btnEnrichir.Visibility = Visibility.Collapsed;
                _btnMore.Content = "✨ IA";
            }
            else
            {
                _btnEnrichir.Visibility = Visibility.Visible;
                _btnMore.Content = "✨";
            }

            _container.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = _container.DesiredSize;

            // Positionnement en haut à droite avec marge de 4px
            double x = Math.Max(0, finalSize.Width - desired.Width - 4);
            double y = 2;

            _container.Arrange(new Rect(new Point(x, y), desired));
            return finalSize;
        }
    }
}

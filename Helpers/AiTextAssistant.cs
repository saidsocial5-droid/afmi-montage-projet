using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Views;

namespace AFMI_Montage_projet_2.Helpers
{
    /// <summary>
    /// Fournit une assistance IA universelle pour tous les contrôles de texte (TextBox et RichTextBox)
    /// de l'ensemble de l'application, avec les 3 actions standardisées : Enrichir, Détailler, Reformuler.
    /// </summary>
    public static class AiTextAssistant
    {
        private static bool _isHookInitialized = false;

        #region Attached Properties

        public static readonly DependencyProperty FieldLabelProperty =
            DependencyProperty.RegisterAttached(
                "FieldLabel",
                typeof(string),
                typeof(AiTextAssistant),
                new PropertyMetadata(null));

        public static string GetFieldLabel(DependencyObject obj) =>
            (string)obj.GetValue(FieldLabelProperty);

        public static void SetFieldLabel(DependencyObject obj, string value) =>
            obj.SetValue(FieldLabelProperty, value);

        public static readonly DependencyProperty SemanticTypeProperty =
            DependencyProperty.RegisterAttached(
                "SemanticType",
                typeof(FieldSemanticType),
                typeof(AiTextAssistant),
                new PropertyMetadata(FieldSemanticType.Generique));

        public static FieldSemanticType GetSemanticType(DependencyObject obj) =>
            (FieldSemanticType)obj.GetValue(SemanticTypeProperty);

        public static void SetSemanticType(DependencyObject obj, FieldSemanticType value) =>
            obj.SetValue(SemanticTypeProperty, value);

        public static readonly DependencyProperty FieldIdentifierProperty =
            DependencyProperty.RegisterAttached(
                "FieldIdentifier",
                typeof(string),
                typeof(AiTextAssistant),
                new PropertyMetadata(null));

        public static string GetFieldIdentifier(DependencyObject obj) =>
            (string)obj.GetValue(FieldIdentifierProperty);

        public static void SetFieldIdentifier(DependencyObject obj, string value) =>
            obj.SetValue(FieldIdentifierProperty, value);

        public static readonly DependencyProperty EntityIdProperty =
            DependencyProperty.RegisterAttached(
                "EntityId",
                typeof(int?),
                typeof(AiTextAssistant),
                new PropertyMetadata(null));

        public static int? GetEntityId(DependencyObject obj) =>
            (int?)obj.GetValue(EntityIdProperty);

        public static void SetEntityId(DependencyObject obj, int? value) =>
            obj.SetValue(EntityIdProperty, value);

        public static readonly DependencyProperty ParentEntityIdProperty =
            DependencyProperty.RegisterAttached(
                "ParentEntityId",
                typeof(int?),
                typeof(AiTextAssistant),
                new PropertyMetadata(null));

        public static int? GetParentEntityId(DependencyObject obj) =>
            (int?)obj.GetValue(ParentEntityIdProperty);

        public static void SetParentEntityId(DependencyObject obj, int? value) =>
            obj.SetValue(ParentEntityIdProperty, value);

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(AiTextAssistant),
                new PropertyMetadata(true));

        public static bool GetIsEnabled(DependencyObject obj) =>
            (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) =>
            obj.SetValue(IsEnabledProperty, value);

        public static readonly DependencyProperty IsExcludedProperty =
            DependencyProperty.RegisterAttached(
                "IsExcluded",
                typeof(bool),
                typeof(AiTextAssistant),
                new PropertyMetadata(false));

        public static bool GetIsExcluded(DependencyObject obj)
        {
            if (obj == null) return true;
            if ((bool)obj.GetValue(IsExcludedProperty)) return true;
            if (obj is TextBoxBase tb && IsControlAutoExcluded(tb, out _)) return true;
            return false;
        }

        public static void SetIsExcluded(DependencyObject obj, bool value) =>
            obj.SetValue(IsExcludedProperty, value);

        public static readonly DependencyProperty ExclusionReasonProperty =
            DependencyProperty.RegisterAttached(
                "ExclusionReason",
                typeof(string),
                typeof(AiTextAssistant),
                new PropertyMetadata(null));

        public static string? GetExclusionReason(DependencyObject obj)
        {
            if (obj == null) return null;
            string? explicitReason = (string?)obj.GetValue(ExclusionReasonProperty);
            if (!string.IsNullOrEmpty(explicitReason)) return explicitReason;
            if (obj is TextBoxBase tb && IsControlAutoExcluded(tb, out string autoReason))
            {
                return autoReason;
            }
            return null;
        }

        public static void SetExclusionReason(DependencyObject obj, string value) =>
            obj.SetValue(ExclusionReasonProperty, value);

        /// <summary>
        /// Détecte automatiquement et exclut les champs conformément au cahier des charges :
        /// Mots de passe, clés API, jetons, identifiants techniques, codes générés automatiquement,
        /// champs numériques, champs de date (ex: DatePicker / Calendar), chemins de fichiers, etc.
        /// </summary>
        public static bool IsControlAutoExcluded(TextBoxBase tb, out string reason)
        {
            if (tb == null)
            {
                reason = "Contrôle nul.";
                return true;
            }

            // 1. Détection prioritaire des sélecteurs de dates (DatePicker, DatePickerTextBox, Calendar)
            string typeName = tb.GetType().Name;
            if (typeName.IndexOf("DatePicker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Composant de sélection de date (DatePicker).";
                return true;
            }

            if (tb.TemplatedParent is DatePicker || FindVisualAncestor<DatePicker>(tb) != null || FindLogicalAncestor<DatePicker>(tb) != null)
            {
                reason = "Composant de sélection de date (DatePicker).";
                return true;
            }

            if (FindVisualAncestor<Calendar>(tb) != null || FindLogicalAncestor<Calendar>(tb) != null)
            {
                reason = "Composant de calendrier.";
                return true;
            }

            // 2. Détection de PasswordBox ou mot de passe
            if (tb is PasswordBox)
            {
                reason = "Mot de passe sécurisé.";
                return true;
            }

            // 3. Les zones de texte multi-lignes et les RichTextBox sont par nature des zones rédactionnelles
            // (Justification, Contexte, Objectifs, Impacts, etc.) et ne sont jamais des dates ni des montants.
            if (tb is TextBox multilineBox && multilineBox.AcceptsReturn)
            {
                reason = string.Empty;
                return false;
            }

            if (tb is RichTextBox)
            {
                reason = string.Empty;
                return false;
            }

            // 4. Détection de sélecteurs numériques (NumericUpDown, etc.)
            if (typeName.IndexOf("Numeric", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Integer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Double", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reason = "Contrôle de saisie numérique.";
                return true;
            }

            // 5. Contrôles à largeur explicite ultra-réduite (< 120px) pour les champs mono-lignes
            // (ex: devise EUR/USD, année, code court, pourcentages)
            if (tb is TextBox singleLineBox && !singleLineBox.AcceptsReturn)
            {
                if (!double.IsNaN(singleLineBox.Width) && singleLineBox.Width > 0 && singleLineBox.Width < 120)
                {
                    reason = "Champ technique ou court (largeur réduite < 120px).";
                    return true;
                }

                if (singleLineBox.MaxLength > 0 && singleLineBox.MaxLength <= 12)
                {
                    reason = "Champ à longueur contrainte (code ou valeur numérique courte).";
                    return true;
                }
            }

            // 6. Analyse sémantique des métadonnées (Name, FieldIdentifier, FieldLabel, Tag, Binding Path)
            string name = tb.Name ?? string.Empty;
            string id = (string)tb.GetValue(FieldIdentifierProperty) ?? string.Empty;
            string label = (string)tb.GetValue(FieldLabelProperty) ?? string.Empty;
            string tag = tb.Tag?.ToString() ?? string.Empty;

            string bindingPath = string.Empty;
            var textBinding = System.Windows.Data.BindingOperations.GetBinding(tb, TextBox.TextProperty);
            if (textBinding?.Path != null)
            {
                bindingPath = textBinding.Path.Path ?? string.Empty;
            }

            string combined = $"{name} {id} {label} {tag} {bindingPath}";

            // A. Champs de date
            if (Regex.IsMatch(combined, @"(date|calendar|annee|year|jour|day|mois|month|quarter|trimestre|semestre|echeance|createdat|updatedat)", RegexOptions.IgnoreCase))
            {
                reason = "Champ de date ou calendrier.";
                return true;
            }

            // B. Mots de passe, clés API, jetons et données sensibles
            if (Regex.IsMatch(combined, @"(password|passphrase|secret|apikey|api_key|token|jwt|bearer|privatekey|hash|salt|connectionstring)", RegexOptions.IgnoreCase))
            {
                reason = "Donnée sensible / authentification (mot de passe, clé API, jeton).";
                return true;
            }

            // C. Identifiants techniques et codes générés automatiquement
            if (Regex.IsMatch(combined, @"(guid|sysid|entityid|technicalid|foreignkey|auto_code)", RegexOptions.IgnoreCase))
            {
                reason = "Identifiant technique ou clé de base de données.";
                return true;
            }

            if (tb is TextBox singleCodeBox && !singleCodeBox.AcceptsReturn &&
                Regex.IsMatch(combined, @"(code|reference|numero)", RegexOptions.IgnoreCase))
            {
                reason = "Code ou référence généré automatiquement.";
                return true;
            }

            // D. Chemins de fichiers et répertoires
            if (Regex.IsMatch(combined, @"(filepath|file_path|folderpath|folder_path|directory|chemindacces)", RegexOptions.IgnoreCase))
            {
                reason = "Chemin de fichier ou répertoire.";
                return true;
            }

            // E. Champs numériques purs, monétaires et pourcentages (pour champs mono-lignes)
            if (Regex.IsMatch(combined, @"(currency|devise|budget|amount|montant|cout|cost|prix|price|solde|balance|taux|rate|pourcent|percent|score|poids|weight|urgenc|feasib|faisabil|duree|duration|quantit|somme)", RegexOptions.IgnoreCase))
            {
                reason = "Champ numérique, monétaire ou indicateur chiffré.";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static T? FindVisualAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch
                {
                    break;
                }
            }
            return null;
        }

        private static T? FindLogicalAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                try
                {
                    current = LogicalTreeHelper.GetParent(current);
                }
                catch
                {
                    break;
                }
            }
            return null;
        }

        #endregion

        /// <summary>
        /// Initialise les écouteurs globaux WPF sur TextBoxBase (TextBox et RichTextBox)
        /// pour intercepter le clic droit, les raccourcis clavier et l'icône Adorner dans toute l'application.
        /// </summary>
        public static void InitializeGlobalHooks()
        {
            if (_isHookInitialized) return;
            _isHookInitialized = true;

            // 0. Écouteur global pour attacher la barre d'outils dès le chargement du contrôle
            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnTextBoxLoaded),
                true);

            // 1. Écouteur global pour l'ouverture du menu contextuel (clic droit)
            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                FrameworkElement.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnContextMenuOpening),
                true);

            // 2. Écouteur global pour les raccourcis clavier :
            // Ctrl+Shift+E (Enrichir), Ctrl+Shift+D (Détailler), Ctrl+Shift+R (Reformuler), Ctrl+Alt+A (Général)
            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(OnPreviewKeyDown),
                true);

            // 3. Écouteurs pour s'assurer que la barre d'outils est bien attachée sur focus et survol
            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                UIElement.GotKeyboardFocusEvent,
                new RoutedEventHandler(OnGotKeyboardFocus),
                true);

            EventManager.RegisterClassHandler(
                typeof(TextBoxBase),
                UIElement.MouseEnterEvent,
                new MouseEventHandler(OnMouseEnter),
                true);

            // 4. Textes affichés (TextBlock : réponses IA, rapports, synthèses) : copie et traduction au clic droit (ADR 26)
            EventManager.RegisterClassHandler(
                typeof(TextBlock),
                FrameworkElement.ContextMenuOpeningEvent,
                new ContextMenuEventHandler(OnTextBlockContextMenuOpening),
                true);
        }

        /// <summary>Longueur minimale d'un TextBlock pour proposer copie et traduction (exclut libellés et boutons).</summary>
        public const int MinTranslatableTextLength = 25;

        private static void OnTextBlockContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not TextBlock textBlock || e.Handled) return;
            if (textBlock.ContextMenu != null && !Equals(textBlock.ContextMenu.Tag, "AFMI_TEXTBLOCK_MENU")) return;

            string text = textBlock.Text ?? string.Empty;
            if (text.Trim().Length < MinTranslatableTextLength) return;
            if (FindVisualAncestor<ButtonBase>(textBlock) != null || FindVisualAncestor<MenuItem>(textBlock) != null ||
                FindVisualAncestor<ComboBox>(textBlock) != null || FindVisualAncestor<TabPanel>(textBlock) != null) return;

            var cm = new ContextMenu { Tag = "AFMI_TEXTBLOCK_MENU" };
            var copy = new MenuItem { Header = "📋 Copier le texte" };
            copy.Click += (s, ev) => CopyToClipboard(text);
            cm.Items.Add(copy);
            cm.Items.Add(new Separator());
            AddTranslationItems(cm.Items, () => text, null);

            textBlock.ContextMenu = cm;
            cm.PlacementTarget = textBlock;
            cm.IsOpen = true;
            e.Handled = true;
        }

        private static void CopyToClipboard(string text)
        {
            try { Clipboard.SetText(text); }
            catch (System.Runtime.InteropServices.COMException ex) { AppLogger.Warn(nameof(AiTextAssistant), "Presse-papiers indisponible.", ex); }
        }

        /// <summary>Ajoute « Traduire en français / العربية / English » à un menu.</summary>
        private static void AddTranslationItems(ItemCollection items, Func<string> getText, TextBoxBase? target)
        {
            var translate = new MenuItem { Header = "🌐 Traduire (Ctrl+Maj+T)", Tag = "AFMI_AI_TAG" };
            foreach (var lang in Services.Language.AppLanguages.All)
            {
                var item = new MenuItem { Header = $"→ {Services.Language.AppLanguages.NativeName(lang)}" };
                item.Click += (s, ev) => OpenTranslation(getText(), target, lang);
                translate.Items.Add(item);
            }
            items.Add(translate);
        }

        /// <summary>
        /// Ouvre la fenêtre de traduction. Pour un champ modifiable (<paramref name="target"/>), la traduction peut
        /// remplacer la sélection ou le texte, ou s'y ajouter.
        /// </summary>
        public static void OpenTranslation(string text, TextBoxBase? target, string? targetLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Aucun texte à traduire.", "Traduction", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool canApply = target != null && !target.IsReadOnly && GetIsEnabled(target) && !GetIsExcluded(target);
            bool isSelection = target != null && !string.IsNullOrWhiteSpace(GetSelectedText(target));
            var window = new TranslationWindow(isSelection ? GetSelectedText(target!) : text, canApply, targetLanguage);
            var owner = (target != null ? Window.GetWindow(target) : null)
                        ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current.MainWindow;
            if (owner != null && owner != window) window.Owner = owner;

            if (window.ShowDialog() != true || !canApply || target == null || string.IsNullOrEmpty(window.ResultText)) return;
            ApplyText(target, window.ResultText, isSelection, window.AppliedMode == TranslationApplyMode.Append);
        }

        /// <summary>Remplace (sélection ou texte complet) ou ajoute un texte dans un champ, en mettant à jour la liaison de données.</summary>
        private static void ApplyText(TextBoxBase target, string text, bool isSelection, bool append)
        {
            if (target is TextBox tb)
            {
                if (append) tb.AppendText((string.IsNullOrEmpty(tb.Text) || tb.Text.EndsWith("\n") ? "" : "\n\n") + text);
                else if (isSelection && tb.SelectionLength > 0) tb.SelectedText = text;
                else tb.Text = text;
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            else if (target is RichTextBox rtb)
            {
                if (append) rtb.AppendText("\n\n" + text);
                else if (isSelection) rtb.Selection.Text = text;
                else
                {
                    rtb.Document.Blocks.Clear();
                    rtb.Document.Blocks.Add(new Paragraph(new Run(text)));
                }
            }
        }

        private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBoxBase textBoxBase) return;
            if (textBoxBase.IsReadOnly || !GetIsEnabled(textBoxBase) || GetIsExcluded(textBoxBase)) return;

            AttachAdorner(textBoxBase);
        }

        private static void OnGotKeyboardFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBoxBase textBoxBase) return;
            if (textBoxBase.IsReadOnly || !GetIsEnabled(textBoxBase) || GetIsExcluded(textBoxBase)) return;

            AttachAdorner(textBoxBase);
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not TextBoxBase textBoxBase) return;
            if (textBoxBase.IsReadOnly || !GetIsEnabled(textBoxBase) || GetIsExcluded(textBoxBase)) return;

            AttachAdorner(textBoxBase);
        }

        public static void AttachAdorner(TextBoxBase tb)
        {
            try
            {
                if (tb.IsReadOnly || !GetIsEnabled(tb) || GetIsExcluded(tb)) return;

                if (!tb.IsLoaded)
                {
                    RoutedEventHandler? onLoaded = null;
                    onLoaded = (s, e) =>
                    {
                        tb.Loaded -= onLoaded;
                        AttachAdorner(tb);
                    };
                    tb.Loaded += onLoaded;
                    return;
                }

                var layer = AdornerLayer.GetAdornerLayer(tb);
                if (layer == null)
                {
                    EventHandler? onLayout = null;
                    onLayout = (s, e) =>
                    {
                        tb.LayoutUpdated -= onLayout;
                        var l = AdornerLayer.GetAdornerLayer(tb);
                        if (l != null)
                        {
                            var existingInLayout = l.GetAdorners(tb)?.OfType<AiButtonAdorner>().FirstOrDefault();
                            if (existingInLayout == null)
                            {
                                l.Add(new AiButtonAdorner(tb));
                            }
                        }
                    };
                    tb.LayoutUpdated += onLayout;
                    return;
                }

                var existing = layer.GetAdorners(tb)?.OfType<AiButtonAdorner>().FirstOrDefault();
                if (existing == null)
                {
                    layer.Add(new AiButtonAdorner(tb));
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiTextAssistant), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        public static void DetachAdorner(TextBoxBase tb)
        {
            try
            {
                var layer = AdornerLayer.GetAdornerLayer(tb);
                if (layer == null) return;

                var adorners = layer.GetAdorners(tb)?.OfType<AiButtonAdorner>().ToList();
                if (adorners != null)
                {
                    foreach (var ad in adorners)
                    {
                        layer.Remove(ad);
                    }
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiTextAssistant), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not TextBoxBase textBoxBase) return;
            if (!GetIsEnabled(textBoxBase) || GetIsExcluded(textBoxBase)) return;
            if (textBoxBase.IsReadOnly && string.IsNullOrWhiteSpace(GetFullText(textBoxBase))) return;

            if (textBoxBase.ContextMenu == null)
            {
                var cm = CreateStandardContextMenu(textBoxBase);
                textBoxBase.ContextMenu = cm;
                cm.PlacementTarget = textBoxBase;
                cm.IsOpen = true;
                e.Handled = true;
            }
            else
            {
                UpdateAiItemsInContextMenu(textBoxBase, textBoxBase.ContextMenu);
            }
        }

        private static ContextMenu CreateStandardContextMenu(TextBoxBase textBoxBase)
        {
            var cm = new ContextMenu();

            var cutItem = new MenuItem { Header = "Couper", Command = ApplicationCommands.Cut, CommandTarget = textBoxBase };
            var copyItem = new MenuItem { Header = "Copier", Command = ApplicationCommands.Copy, CommandTarget = textBoxBase };
            var pasteItem = new MenuItem { Header = "Coller", Command = ApplicationCommands.Paste, CommandTarget = textBoxBase };

            cm.Items.Add(cutItem);
            cm.Items.Add(copyItem);
            cm.Items.Add(pasteItem);

            UpdateAiItemsInContextMenu(textBoxBase, cm);
            return cm;
        }

        private static void UpdateAiItemsInContextMenu(TextBoxBase textBoxBase, ContextMenu cm)
        {
            var oldItems = cm.Items.OfType<FrameworkElement>()
                .Where(item => Equals(item.Tag, "AFMI_AI_TAG") || Equals(item.Tag, "AFMI_AI_SEPARATOR"))
                .ToList();

            foreach (var item in oldItems)
            {
                cm.Items.Remove(item);
            }

            if (GetIsExcluded(textBoxBase)) return;

            string selectedText = GetSelectedText(textBoxBase);
            bool hasSelection = !string.IsNullOrWhiteSpace(selectedText);
            string targetDesc = hasSelection ? "la sélection" : "le texte";

            var separator = new Separator { Tag = "AFMI_AI_SEPARATOR" };
            cm.Items.Add(separator);

            // Traduction : pour tous les champs, y compris en lecture seule (réponses IA, synthèses) (ADR 26)
            AddTranslationItems(cm.Items, () => GetFullText(textBoxBase), textBoxBase);
            if (textBoxBase.IsReadOnly) return;

            // 1. ✨ Enrichir (Ctrl+Maj+E)
            var itemEnrichir = new MenuItem
            {
                Header = $"✨ Enrichir {targetDesc} (Ctrl+Maj+E)",
                Tag = "AFMI_AI_TAG",
                FontWeight = FontWeights.SemiBold
            };
            itemEnrichir.Click += (s, ev) => OpenEnhancementDialog(textBoxBase, TextAssistActionType.Enrichir);
            cm.Items.Add(itemEnrichir);

            // 2. 📝 Détailler (Ctrl+Maj+D)
            var itemDetailler = new MenuItem
            {
                Header = $"📝 Détailler {targetDesc} (Ctrl+Maj+D)",
                Tag = "AFMI_AI_TAG"
            };
            itemDetailler.Click += (s, ev) => OpenEnhancementDialog(textBoxBase, TextAssistActionType.Detailler);
            cm.Items.Add(itemDetailler);

            // 3. 🔄 Reformuler (Ctrl+Maj+R)
            var itemReformuler = new MenuItem
            {
                Header = $"🔄 Reformuler {targetDesc} (Ctrl+Maj+R)",
                Tag = "AFMI_AI_TAG"
            };
            itemReformuler.Click += (s, ev) => OpenEnhancementDialog(textBoxBase, TextAssistActionType.Reformuler);
            cm.Items.Add(itemReformuler);

            // 4. Sous-menu options avancées
            var subMenu = new MenuItem
            {
                Header = "🤖 Plus d'outils d'assistance...",
                Tag = "AFMI_AI_TAG"
            };

            var itemHistory = new MenuItem
            {
                Header = "📜 Historique des versions de ce champ..."
            };
            itemHistory.Click += (s, ev) => OpenEnhancementDialog(textBoxBase, TextAssistActionType.Enrichir, openHistoryDirectly: true);
            subMenu.Items.Add(itemHistory);

            subMenu.Items.Add(new Separator());

            var itemFull = new MenuItem
            {
                Header = "⚡ Ouvrir l'Assistant Rédactionnel complet (Ctrl+Alt+A)..."
            };
            itemFull.Click += (s, ev) => OpenEnhancementDialog(textBoxBase, TextAssistActionType.Enrichir);
            subMenu.Items.Add(itemFull);

            cm.Items.Add(subMenu);
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBoxBase textBoxBase) return;
            if (!GetIsEnabled(textBoxBase) || GetIsExcluded(textBoxBase)) return;

            bool isCtrlShift = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift);
            bool isCtrlAlt = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt);

            // Ctrl+Maj+T : traduction, disponible aussi dans les champs en lecture seule
            if (isCtrlShift && e.Key == Key.T)
            {
                e.Handled = true;
                OpenTranslation(GetFullText(textBoxBase), textBoxBase);
                return;
            }

            if (textBoxBase.IsReadOnly) return;

            if (isCtrlShift && e.Key == Key.E)
            {
                e.Handled = true;
                OpenEnhancementDialog(textBoxBase, TextAssistActionType.Enrichir);
            }
            else if (isCtrlShift && e.Key == Key.D)
            {
                e.Handled = true;
                OpenEnhancementDialog(textBoxBase, TextAssistActionType.Detailler);
            }
            else if (isCtrlShift && e.Key == Key.R)
            {
                e.Handled = true;
                OpenEnhancementDialog(textBoxBase, TextAssistActionType.Reformuler);
            }
            else if (isCtrlAlt && e.Key == Key.A)
            {
                e.Handled = true;
                OpenEnhancementDialog(textBoxBase, TextAssistActionType.Enrichir);
            }
        }

        /// <summary>
        /// Ouvre le dialogue d'assistance rédactionnelle pour le contrôle de texte ciblé.
        /// </summary>
        public static void OpenEnhancementDialog(TextBoxBase textBoxBase, TextEnhancementMode mode)
        {
            var action = mode switch
            {
                TextEnhancementMode.Detailler => TextAssistActionType.Detailler,
                TextEnhancementMode.Reformuler => TextAssistActionType.Reformuler,
                _ => TextAssistActionType.Enrichir
            };
            OpenEnhancementDialog(textBoxBase, action);
        }

        public static void OpenEnhancementDialog(
            TextBoxBase textBoxBase,
            TextAssistActionType initialAction = TextAssistActionType.Enrichir,
            bool openHistoryDirectly = false)
        {
            if (textBoxBase == null) return;
            if (GetIsExcluded(textBoxBase))
            {
                string reason = GetExclusionReason(textBoxBase) ?? "Ce champ est exclu de l'assistance.";
                MessageBox.Show(reason, "Champ non assisté", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selectedText = GetSelectedText(textBoxBase);
            string fullText = GetFullText(textBoxBase);
            bool isSelection = !string.IsNullOrWhiteSpace(selectedText);
            string textToProcess = isSelection ? selectedText : fullText;

            var descriptor = CreateFieldDescriptor(textBoxBase);
            int? projectId = SessionManager.CurrentProjectId;

            var dialog = new AiTextEnhancementDialog(
                descriptor: descriptor,
                initialText: textToProcess,
                isSelection: isSelection,
                initialAction: initialAction,
                projectId: projectId,
                openHistoryDirectly: openHistoryDirectly);

            var ownerWindow = Window.GetWindow(textBoxBase)
                              ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                              ?? Application.Current.MainWindow;

            if (ownerWindow != null)
            {
                dialog.Owner = ownerWindow;
            }

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.ResultText))
            {
                string replacement = dialog.ResultText;

                if (dialog.AppliedAction == AiEnhancementAction.Replace)
                {
                    if (textBoxBase is TextBox tb)
                    {
                        if (isSelection && tb.SelectionLength > 0)
                        {
                            tb.SelectedText = replacement;
                        }
                        else
                        {
                            tb.Text = replacement;
                        }
                        var be = tb.GetBindingExpression(TextBox.TextProperty);
                        be?.UpdateSource();
                    }
                    else if (textBoxBase is RichTextBox rtb)
                    {
                        if (isSelection)
                        {
                            rtb.Selection.Text = replacement;
                        }
                        else
                        {
                            rtb.Document.Blocks.Clear();
                            rtb.Document.Blocks.Add(new Paragraph(new Run(replacement)));
                        }
                    }
                }
                else if (dialog.AppliedAction == AiEnhancementAction.Append)
                {
                    if (textBoxBase is TextBox tb)
                    {
                        if (!string.IsNullOrEmpty(tb.Text) && !tb.Text.EndsWith("\n") && !tb.Text.EndsWith(" "))
                        {
                            tb.AppendText("\n\n" + replacement);
                        }
                        else
                        {
                            tb.AppendText(replacement);
                        }
                        var be = tb.GetBindingExpression(TextBox.TextProperty);
                        be?.UpdateSource();
                    }
                    else if (textBoxBase is RichTextBox rtb)
                    {
                        rtb.AppendText("\n\n" + replacement);
                    }
                }
            }
        }

        public static FieldDescriptor CreateFieldDescriptor(TextBoxBase tb)
        {
            string label = DetectFieldLabel(tb);
            var semantic = GetSemanticType(tb);

            if (semantic == FieldSemanticType.Generique)
            {
                semantic = FieldTypeRegistry.DetectSemanticType(tb.Name, label, tb.Tag?.ToString());
            }

            string identifier = GetFieldIdentifier(tb);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                string windowName = Window.GetWindow(tb)?.GetType().Name ?? "View";
                string controlName = !string.IsNullOrWhiteSpace(tb.Name) ? tb.Name : label.Replace(" ", "");
                identifier = $"{windowName}.{controlName}";
            }

            return new FieldDescriptor(identifier, label, semantic)
            {
                EntityId = GetEntityId(tb),
                ParentEntityId = GetParentEntityId(tb),
                IsExcluded = GetIsExcluded(tb),
                ExclusionReason = GetExclusionReason(tb)
            };
        }

        public static string DetectFieldLabel(TextBoxBase tb)
        {
            var attached = GetFieldLabel(tb);
            if (!string.IsNullOrWhiteSpace(attached)) return attached;

            if (tb.Tag != null && !string.IsNullOrWhiteSpace(tb.Tag.ToString()))
                return tb.Tag.ToString()!;

            if (tb.ToolTip != null && !string.IsNullOrWhiteSpace(tb.ToolTip.ToString()))
                return tb.ToolTip.ToString()!;

            var autoName = System.Windows.Automation.AutomationProperties.GetName(tb);
            if (!string.IsNullOrWhiteSpace(autoName)) return autoName;

            try
            {
                var parent = VisualTreeHelper.GetParent(tb);
                if (parent is Panel panel)
                {
                    int index = panel.Children.IndexOf(tb);
                    for (int i = index - 1; i >= 0; i--)
                    {
                        var sibling = panel.Children[i];
                        if (sibling is TextBlock txtBlock && !string.IsNullOrWhiteSpace(txtBlock.Text))
                        {
                            string clean = txtBlock.Text.Trim().TrimEnd(':', ' ', '?', '*');
                            if (clean.Length >= 2 && clean.Length < 100) return clean;
                        }
                        if (sibling is Label lbl && lbl.Content != null)
                        {
                            string clean = lbl.Content.ToString()!.Trim().TrimEnd(':', ' ', '?', '*');
                            if (clean.Length >= 2 && clean.Length < 100) return clean;
                        }
                    }
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiTextAssistant), $"Erreur ignorée : {swallowedEx.Message}"); }

            return "Champ de saisie";
        }

        private static string GetSelectedText(TextBoxBase tb)
        {
            if (tb is TextBox textBox) return textBox.SelectedText;
            if (tb is RichTextBox rtb) return new TextRange(rtb.Selection.Start, rtb.Selection.End).Text;
            return string.Empty;
        }

        private static string GetFullText(TextBoxBase tb)
        {
            if (tb is TextBox textBox) return textBox.Text;
            if (tb is RichTextBox rtb) return new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text;
            return string.Empty;
        }
    }
}

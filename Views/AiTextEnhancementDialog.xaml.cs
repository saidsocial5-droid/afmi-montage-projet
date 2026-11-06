using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.Views
{
    public partial class AiTextEnhancementDialog : Window
    {
        private readonly TextAssistService _service = new();
        private readonly FieldDescriptor _descriptor;
        private readonly string _initialText;
        private readonly bool _isSelection;
        private readonly int? _projectId;
        private TextAssistActionType _currentAction;
        private TextAssistResponse? _lastResponse;
        private bool _isInitialized = false;

        public string ResultText { get; private set; } = string.Empty;
        public AiEnhancementAction AppliedAction { get; private set; } = AiEnhancementAction.None;

        public AiTextEnhancementDialog(
            FieldDescriptor descriptor,
            string initialText,
            bool isSelection,
            TextAssistActionType initialAction,
            int? projectId,
            bool openHistoryDirectly = false)
        {
            InitializeComponent();

            _descriptor = descriptor ?? new FieldDescriptor("field", "Champ de saisie");
            _initialText = initialText ?? string.Empty;
            _isSelection = isSelection;
            _currentAction = initialAction;
            _projectId = projectId;

            TxtFieldLabel.Text = $"Champ : {_descriptor.FieldLabel}";
            TxtSemanticType.Text = $"Rôle : {_descriptor.SemanticType}";
            TxtScopeLabel.Text = _isSelection ? "Cible : Sélection partielle" : "Cible : Texte complet";

            // Afficher le titre du projet si disponible
            if (_projectId.HasValue && _projectId.Value > 0)
            {
                using var db = new AppDbContext();
                var proj = db.Projects.FirstOrDefault(p => p.Id == _projectId.Value);
                if (proj != null)
                {
                    TxtProjectTitle.Text = $"Projet : {proj.Title}";
                    BorderProjectInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    BorderProjectInfo.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                BorderProjectInfo.Visibility = Visibility.Collapsed;
            }

            TxtOriginal.Text = _initialText;
            UpdateOriginalCount();

            // Sélectionner le radio button approprié
            switch (_currentAction)
            {
                case TextAssistActionType.Enrichir: if (RbEnrichir != null) RbEnrichir.IsChecked = true; break;
                case TextAssistActionType.Detailler: if (RbDetailler != null) RbDetailler.IsChecked = true; break;
                case TextAssistActionType.Reformuler: if (RbReformuler != null) RbReformuler.IsChecked = true; break;
            }

            if (RbTabDiff != null) RbTabDiff.IsChecked = true;

            UpdateActionDescription();

            Loaded += async (s, e) =>
            {
                _isInitialized = true;
                if (openHistoryDirectly)
                {
                    if (TbToggleHistory != null) TbToggleHistory.IsChecked = true;
                    await LoadHistoryAsync();
                }
                else
                {
                    await TriggerGenerationAsync();
                }
            };
        }

        // Constructeur de compatibilité descendante avec l'ancien TextEnhancementMode
        public AiTextEnhancementDialog(
            string fieldLabel,
            string initialText,
            bool isSelection,
            TextEnhancementMode initialMode,
            int? projectId) : this(
                new FieldDescriptor("field", fieldLabel),
                initialText,
                isSelection,
                MapModeToAction(initialMode),
                projectId)
        {
        }

        private static TextAssistActionType MapModeToAction(TextEnhancementMode mode) => mode switch
        {
            TextEnhancementMode.Detailler => TextAssistActionType.Detailler,
            TextEnhancementMode.Reformuler => TextAssistActionType.Reformuler,
            _ => TextAssistActionType.Enrichir
        };

        private void ActionRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (RbEnrichir == null || RbDetailler == null || RbReformuler == null) return;

            if (RbEnrichir.IsChecked == true) _currentAction = TextAssistActionType.Enrichir;
            else if (RbDetailler.IsChecked == true) _currentAction = TextAssistActionType.Detailler;
            else if (RbReformuler.IsChecked == true) _currentAction = TextAssistActionType.Reformuler;

            UpdateActionDescription();

            if (_isInitialized)
            {
                _ = TriggerGenerationAsync();
            }
        }

        private void UpdateActionDescription()
        {
            if (TxtActionDescription == null) return;

            switch (_currentAction)
            {
                case TextAssistActionType.Enrichir:
                    TxtActionDescription.Text = "✨ Enrichir : Ajoute des arguments et précisions issus du contexte projet en conservant scrupuleusement l'existant.";
                    break;
                case TextAssistActionType.Detailler:
                    TxtActionDescription.Text = "📝 Détailler : Développe l'écrit existant en sous-points et étapes concrètes à sens rigoureusement invariant.";
                    break;
                case TextAssistActionType.Reformuler:
                    TxtActionDescription.Text = "🔄 Reformuler : Réécrit pour l'élégance syntaxique et le registre professionnel adapté aux évaluateurs bailleurs.";
                    break;
            }
        }

        private void ViewTab_Checked(object sender, RoutedEventArgs e)
        {
            if (RtbDiffView == null || TxtEnhanced == null || RbTabDiff == null) return;

            if (RbTabDiff.IsChecked == true)
            {
                RtbDiffView.Visibility = Visibility.Visible;
                TxtEnhanced.Visibility = Visibility.Collapsed;
            }
            else
            {
                RtbDiffView.Visibility = Visibility.Collapsed;
                TxtEnhanced.Visibility = Visibility.Visible;
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            await TriggerGenerationAsync();
        }

        private async Task TriggerGenerationAsync()
        {
            TxtStatusMessage.Text = string.Empty;
            LoadingOverlay.Visibility = Visibility.Visible;
            BorderFidelityAlerts.Visibility = Visibility.Collapsed;

            var settings = new TextAssistSettings
            {
                ProjectId = _projectId,
                ToneRegister = (CbTone.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Institutionnel bailleur",
                TargetLength = (CbLength.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Équivalent",
                // Premier choix « Même langue (Auto) » : la langue du texte d'origine est détectée et imposée (ADR 26)
                OutputLanguage = CbLanguage.SelectedIndex <= 0 ? "Auto" : (CbLanguage.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Auto",
                CustomInstruction = TxtCustomInstruction.Text?.Trim()
            };

            try
            {
                _lastResponse = await _service.ProcessActionAsync(
                    action: _currentAction,
                    currentText: _initialText,
                    descriptor: _descriptor,
                    settings: settings,
                    projectId: _projectId);

                if (_lastResponse.IsSuccess)
                {
                    string proposal = _lastResponse.Proposal;
                    TxtEnhanced.Text = proposal;

                    // Mettre à jour le FlowDocument de Diff avec surbrillance
                    RtbDiffView.Document = TextDiffHelper.CreateHighlightedDiffDocument(_initialText, proposal, _currentAction);

                    // Moteur IA utilisé
                    if (!string.IsNullOrWhiteSpace(_lastResponse.ProviderUsed))
                    {
                        TxtEngineName.Text = $"Moteur : {_lastResponse.ProviderUsed}";
                    }

                    // Éléments de contexte utilisés
                    IcContextUsed.ItemsSource = _lastResponse.ContextUsed;
                    BorderContextUsed.Visibility = _lastResponse.ContextUsed.Any() ? Visibility.Visible : Visibility.Collapsed;

                    // Avertissements de fidélité et marqueurs [à compléter : ...]
                    var allWarnings = new List<string>(_lastResponse.Warnings);
                    foreach (var p in _lastResponse.Placeholders)
                    {
                        if (!allWarnings.Any(w => w.Contains(p)))
                            allWarnings.Add($"Donnée à compléter : {p}");
                    }

                    if (allWarnings.Any())
                    {
                        IcFidelityWarnings.ItemsSource = allWarnings;
                        BorderFidelityAlerts.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        BorderFidelityAlerts.Visibility = Visibility.Collapsed;
                    }

                    TxtStatusMessage.Text = $"✅ Proposition formulée en {_lastResponse.ExecutionDurationMs} ms.";
                    TxtStatusMessage.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    TxtStatusMessage.Text = _lastResponse.ErrorMessage ?? "Erreur lors de la formulation.";
                    TxtStatusMessage.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Erreur : {ex.Message}";
                TxtStatusMessage.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                UpdateEnhancedCount();
            }
        }

        private void TxtEnhanced_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateEnhancedCount();
            // Mettre à jour le diff si l'utilisateur modifie directement le texte
            if (_isInitialized && RtbDiffView != null)
            {
                RtbDiffView.Document = TextDiffHelper.CreateHighlightedDiffDocument(_initialText, TxtEnhanced.Text, _currentAction);
            }
        }

        private void UpdateOriginalCount()
        {
            if (TxtOriginal == null || TxtOriginalCount == null) return;
            int chars = TxtOriginal.Text.Length;
            int words = CountWords(TxtOriginal.Text);
            TxtOriginalCount.Text = $"{chars} car. ({words} mots)";
        }

        private void UpdateEnhancedCount()
        {
            if (TxtEnhanced == null || TxtEnhancedCount == null) return;
            int chars = TxtEnhanced.Text.Length;
            int words = CountWords(TxtEnhanced.Text);
            TxtEnhancedCount.Text = $"{chars} car. ({words} mots)";
        }

        private static int CountWords(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return s.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        #region Actions de validation et persistance

        private async void BtnReplace_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtEnhanced.Text))
            {
                MessageBox.Show("La proposition est vide.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ResultText = TxtEnhanced.Text;
            AppliedAction = AiEnhancementAction.Replace;

            await SaveHistoryAndAuditAsync(wasAccepted: true, wasModified: TxtEnhanced.Text != _lastResponse?.Proposal);

            DialogResult = true;
            Close();
        }

        private async void BtnAppend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtEnhanced.Text))
            {
                MessageBox.Show("La proposition est vide.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ResultText = TxtEnhanced.Text;
            AppliedAction = AiEnhancementAction.Append;

            await SaveHistoryAndAuditAsync(wasAccepted: true, wasModified: false);

            DialogResult = true;
            Close();
        }

        private async Task SaveHistoryAndAuditAsync(bool wasAccepted, bool wasModified)
        {
            try
            {
                string provider = _lastResponse?.ProviderUsed ?? "Local";
                string model = _lastResponse?.ModelUsed ?? "Default";

                // Sauvegarder dans la table d'historique de versions
                await _service.SaveVersionHistoryAsync(
                    fieldIdentifier: _descriptor.FieldIdentifier,
                    originalText: _initialText,
                    proposedText: _lastResponse?.Proposal ?? TxtEnhanced.Text,
                    finalText: ResultText,
                    actionType: _currentAction,
                    semanticType: _descriptor.SemanticType,
                    providerUsed: provider,
                    modelUsed: model,
                    contextUsed: _lastResponse?.ContextUsed,
                    warnings: _lastResponse?.Warnings,
                    projectId: _projectId);

                // Enregistrer l'audit
                await _service.LogAuditAsync(
                    fieldIdentifier: _descriptor.FieldIdentifier,
                    actionType: _currentAction,
                    providerUsed: provider,
                    modelUsed: model,
                    wasAccepted: wasAccepted,
                    wasModified: wasModified,
                    originalLength: _initialText.Length,
                    finalLength: ResultText.Length,
                    elapsedMs: _lastResponse?.ExecutionDurationMs ?? 0,
                    projectId: _projectId);
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(AiTextEnhancementDialog), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtEnhanced.Text))
            {
                Clipboard.SetText(TxtEnhanced.Text);
                TxtStatusMessage.Text = "✅ Texte copié dans le presse-papiers.";
                TxtStatusMessage.Foreground = System.Windows.Media.Brushes.Green;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Historique de versions

        private async void TbToggleHistory_Changed(object sender, RoutedEventArgs e)
        {
            if (TbToggleHistory == null || BorderHistoryDrawer == null) return;

            if (TbToggleHistory.IsChecked == true)
            {
                BorderHistoryDrawer.Visibility = Visibility.Visible;
                await LoadHistoryAsync();
            }
            else
            {
                BorderHistoryDrawer.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnCloseHistory_Click(object sender, RoutedEventArgs e)
        {
            if (TbToggleHistory != null) TbToggleHistory.IsChecked = false;
            if (BorderHistoryDrawer != null) BorderHistoryDrawer.Visibility = Visibility.Collapsed;
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var history = await _service.GetVersionHistoryAsync(_descriptor.FieldIdentifier, _projectId, 15);
                DgVersionHistory.ItemsSource = history;
                TxtHistoryEmpty.Visibility = history.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Historique indisponible : {ex.Message}";
            }
        }

        private void BtnRestoreVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TextVersionHistory hist)
            {
                TxtEnhanced.Text = hist.FinalText;
                RbTabEdit.IsChecked = true;
                BorderHistoryDrawer.Visibility = Visibility.Collapsed;
                TbToggleHistory.IsChecked = false;

                TxtStatusMessage.Text = $"Version du {hist.CreatedAt:dd/MM/yyyy HH:mm} chargée. Cliquez sur 'Accepter' pour appliquer.";
                TxtStatusMessage.Foreground = System.Windows.Media.Brushes.Blue;
            }
        }

        #endregion
    }
}

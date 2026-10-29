using System;
using System.Threading.Tasks;
using System.Windows;
using AFMI_Montage_projet_2.Services;
using AFMI_Montage_projet_2.Services.Language;

namespace AFMI_Montage_projet_2.Views
{
    public enum TranslationApplyMode
    {
        None,
        Replace,
        Append
    }

    /// <summary>
    /// Fenêtre de traduction français ⇄ arabe ⇄ anglais (ADR 26), ouverte depuis le menu contextuel de n'importe quel
    /// champ ou texte de l'application. Pour un champ modifiable, la traduction peut remplacer le texte ou s'y ajouter.
    /// </summary>
    public partial class TranslationWindow : Window
    {
        private readonly string _original;
        private readonly bool _canApply;
        private readonly TranslationService _service = new();

        public TranslationApplyMode AppliedMode { get; private set; } = TranslationApplyMode.None;
        public string ResultText => TxtTranslation.Text;

        public TranslationWindow(string original, bool canApply, string? initialTarget = null)
        {
            InitializeComponent();
            _original = original ?? string.Empty;
            _canApply = canApply;
            TxtOriginal.Text = _original;

            var detected = LanguageGuard.Detect(_original);
            TxtOriginal.FlowDirection = detected.Language == AppLanguages.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            TxtSourceInfo.Text = detected.IsReliable
                ? $"Langue détectée : {AppLanguages.NativeName(detected.Language)} — {_original.Length:N0} caractères."
                : $"Langue non déterminée — {_original.Length:N0} caractères.";

            BtnReplace.Visibility = BtnAppend.Visibility = canApply ? Visibility.Visible : Visibility.Collapsed;

            string target = initialTarget ?? DefaultTarget(detected);
            Loaded += async (_, _) => await TranslateAsync(target);
        }

        /// <summary>Cible par défaut : le français pour un texte arabe ou anglais, l'arabe pour un texte français.</summary>
        public static string DefaultTarget(LanguageDetection detected) =>
            detected.Language == AppLanguages.French ? AppLanguages.Arabic : AppLanguages.French;

        private async Task TranslateAsync(string target)
        {
            if (string.IsNullOrWhiteSpace(_original))
            {
                TxtWarnings.Text = "Aucun texte à traduire.";
                return;
            }

            SetBusy(true);
            TxtWarnings.Text = string.Empty;
            TxtResultTitle.Text = $"Traduction en {AppLanguages.NativeName(target)} (modifiable)";
            TxtTranslation.FlowDirection = target == AppLanguages.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            try
            {
                var result = await _service.TranslateAsync(_original, target);
                TxtTranslation.Text = result.Text;
                TxtWarnings.Text = string.Join("\n", result.Warnings);
                bool ok = result.IsSuccess && !string.IsNullOrWhiteSpace(result.Text);
                BtnCopy.IsEnabled = ok;
                BtnReplace.IsEnabled = BtnAppend.IsEnabled = ok && _canApply;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(TranslationWindow), "Traduction impossible.", ex);
                TxtWarnings.Text = $"Traduction impossible : {ex.Message}";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BtnFrench.IsEnabled = BtnArabic.IsEnabled = BtnEnglish.IsEnabled = !busy;
        }

        private async void BtnFrench_Click(object sender, RoutedEventArgs e) => await TranslateAsync(AppLanguages.French);
        private async void BtnArabic_Click(object sender, RoutedEventArgs e) => await TranslateAsync(AppLanguages.Arabic);
        private async void BtnEnglish_Click(object sender, RoutedEventArgs e) => await TranslateAsync(AppLanguages.English);

        private void BtnReplace_Click(object sender, RoutedEventArgs e) => Close(TranslationApplyMode.Replace);
        private void BtnAppend_Click(object sender, RoutedEventArgs e) => Close(TranslationApplyMode.Append);

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtTranslation.Text);
                TxtWarnings.Text = "Traduction copiée dans le presse-papiers.";
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                AppLogger.Warn(nameof(TranslationWindow), "Presse-papiers indisponible.", ex);
                TxtWarnings.Text = "Presse-papiers occupé : réessayez.";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close(TranslationApplyMode.None);

        private void Close(TranslationApplyMode mode)
        {
            AppliedMode = mode;
            DialogResult = mode != TranslationApplyMode.None;
        }
    }
}

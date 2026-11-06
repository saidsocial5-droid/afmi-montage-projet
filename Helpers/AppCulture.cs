using System.Globalization;
using System.Threading;

namespace AFMI_Montage_projet_2.Helpers
{
    /// <summary>
    /// Culture de formatage unique de l'application (ADR 27) : fr-FR, quelle que soit la langue de Windows.
    /// Sans elle, « {n:N0} » donnait « 1 200 » sur un poste français, « 1,200 » sur un poste anglais et
    /// « ١٬٢٠٠ » sur un poste arabe, dans les textes enregistrés comme dans les dossiers exportés.
    /// La langue d'affichage (CurrentUICulture) et la propriété Language de WPF ne sont pas modifiées :
    /// la saisie des nombres dans les champs liés reste inchangée.
    /// </summary>
    public static class AppCulture
    {
        public const string Name = "fr-FR";

        public static CultureInfo Formatting { get; } = CultureInfo.GetCultureInfo(Name);

        /// <summary>À appeler au tout début du démarrage, avant la création des threads de travail.</summary>
        public static void Apply()
        {
            CultureInfo.DefaultThreadCurrentCulture = Formatting;
            Thread.CurrentThread.CurrentCulture = Formatting;
        }
    }
}

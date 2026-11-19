using System;
using System.Windows;
using System.Windows.Threading;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static System.Threading.Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Formatage des nombres et dates indépendant de la langue de Windows (ADR 27)
            Helpers.AppCulture.Apply();

            const string appName = "AFMI_Montage_Projet_SingleInstance_Mutex";
            _mutex = new System.Threading.Mutex(true, appName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Le programme est déjà en cours d'exécution.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown();
                return;
            }

            // Gestionnaires globaux d'exceptions enregistrés en premier pour couvrir aussi l'initialisation
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppLogger.Error("App", "Exception non observée dans une tâche en arrière-plan.", args.Exception);
                args.SetObserved();
            };

            AppLogger.Info("App", $"Démarrage d'AFMI Montage Projet v{AutoUpdaterService.CurrentVersion}");

            base.OnStartup(e);

            try
            {
                DataMigrationService.MigrateLegacyData();
            }
            catch (Exception ex)
            {
                AppLogger.Error("App", "Échec de la migration des données héritées.", ex);
                MessageBox.Show($"Erreur lors de la migration des données :\n{ex.Message}", "Avertissement", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ThemeManager.Initialize();

            // Initialiser l'assistance IA universelle pour tous les contrôles texte de l'application
            AFMI_Montage_projet_2.Helpers.AiTextAssistant.InitializeGlobalHooks();

            // Initialiser la base de données au démarrage
            try
            {
                using var db = new AppDbContext();
                db.SeedDefaultData();
            }
            catch (Exception ex)
            {
                AppLogger.Error("App", "Échec de l'initialisation de la base de données.", ex);
                MessageBox.Show($"Erreur d'initialisation de la base de données locale :\n{ex.Message}\n\nJournal : {AppLogger.LogDirectory}",
                                "Erreur critique",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }

            // La vérification des mises à jour est effectuée par MainViewModel (GitHub, puis Google Drive à défaut).
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(App), $"Erreur ignorée : {swallowedEx.Message}"); }

            // Mise à jour téléchargée et vérifiée pendant la session : installée maintenant, mutex déjà libéré (ADR 29)
            Services.Updates.AutoUpdateCoordinator.InstallPendingOnExit();
            AppLogger.Info("App", $"Fermeture (code {e.ApplicationExitCode})");
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Error("App", "Exception non gérée sur le thread UI.", e.Exception);
            MessageBox.Show($"Une erreur inattendue est survenue dans l'interface :\n\n{e.Exception.Message}\n\nLes détails techniques ont été enregistrés dans :\n{AppLogger.LogDirectory}",
                            "Erreur d'application",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
            e.Handled = true; // Empêcher la fermeture brutale de l'application
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Error("App", "Exception critique non gérée.", ex);
                MessageBox.Show($"Une erreur critique est survenue :\n\n{ex.Message}\n\nLes détails techniques ont été enregistrés dans :\n{AppLogger.LogDirectory}",
                                "Erreur système",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}

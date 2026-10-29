using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class CloudSyncViewModel : ObservableObject
    {
        [ObservableProperty]
        private DatabaseSettings _settings;

        [ObservableProperty]
        private string _connectionStatus = string.Empty;

        [ObservableProperty]
        private bool _isTesting;

        [ObservableProperty]
        private bool _isMigrating;

        public CloudSyncViewModel()
        {
            _settings = DatabaseConfigManager.LoadSettings();
        }

        [RelayCommand]
        private void SaveSettings()
        {
            if (!DatabaseConfigManager.SaveSettings(Settings))
            {
                MessageBox.Show($"La configuration n'a pas pu être enregistrée. Consultez le journal :\n{AppLogger.LogDirectory}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show("Configuration enregistrée. Le logiciel utilisera ces paramètres lors de la prochaine action.", "Sauvegarde", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task TestConnection()
        {
            if (string.IsNullOrWhiteSpace(Settings.Host))
            {
                MessageBox.Show("Veuillez saisir l'adresse du serveur (Hôte).", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsTesting = true;
            ConnectionStatus = "Test de connexion en cours...";

            try
            {
                // Sauvegarde temporaire pour le test
                DatabaseConfigManager.SaveSettings(Settings);

                using var db = new AppDbContext();
                bool canConnect = await db.Database.CanConnectAsync();

                if (canConnect)
                {
                    // Initialiser la base si elle est vide
                    db.Database.EnsureCreated();
                    ConnectionStatus = "✅ Connexion réussie ! La base de données est prête.";
                }
                else
                {
                    ConnectionStatus = "❌ Échec de la connexion. Vérifiez vos identifiants et l'adresse.";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"❌ Erreur : {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        }

        [RelayCommand]
        private async Task MigrateData()
        {
            if (MessageBox.Show("Attention : Cette action va désactiver temporairement le mode Cloud, lire toutes vos données locales (SQLite), et les copier vers le serveur PostgreSQL distant. Assurez-vous d'avoir testé la connexion au préalable. Continuer ?", "Confirmation de Migration", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            IsMigrating = true;
            ConnectionStatus = "Migration en cours... (Ne fermez pas le logiciel)";

            try
            {
                // Etape 1: Forcer la lecture en local
                bool originalCloudState = Settings.UseCloud;
                Settings.UseCloud = false;
                DatabaseConfigManager.SaveSettings(Settings);

                var localData = new System.Collections.Generic.List<Project>();
                var localAppConfigs = new System.Collections.Generic.List<AppConfig>();

                using (var localDb = new AppDbContext())
                {
                    // Lecture avec Include pour charger tout le graphe d'objets du projet
                    localData = await localDb.Projects
                        .Include(p => p.Objectives).ThenInclude(o => o.Results).ThenInclude(r => r.Activities).ThenInclude(a => a.Indicators)
                        .Include(p => p.BudgetItems)
                        .AsNoTracking()
                        .ToListAsync();

                    localAppConfigs = await localDb.AppConfigs.AsNoTracking().ToListAsync();
                }

                // Etape 2: Forcer l'écriture sur le Cloud
                Settings.UseCloud = true;
                DatabaseConfigManager.SaveSettings(Settings);

                using (var cloudDb = new AppDbContext())
                {
                    // Récréer/assurer la structure
                    await cloudDb.Database.EnsureCreatedAsync();

                    // Insérer configs s'il n'y en a pas
                    if (!await cloudDb.AppConfigs.AnyAsync() && localAppConfigs.Any())
                    {
                        foreach (var cfg in localAppConfigs)
                        {
                            cfg.Id = 0; // Reset ID for auto-increment
                            cloudDb.AppConfigs.Add(cfg);
                        }
                    }

                    // Insérer projets (Reset ID pour les entités principales de façon simplifiée)
                    // Note : EF Core gére les auto-incréments si on met ID à 0
                    foreach (var project in localData)
                    {
                        project.Id = 0;
                        
                        if (project.Objectives != null) {
                            foreach (var obj in project.Objectives) {
                                obj.Id = 0; obj.ProjectId = 0;
                                if (obj.Results != null) {
                                    foreach (var res in obj.Results) {
                                        res.Id = 0; res.ObjectiveId = 0;
                                        if (res.Activities != null) {
                                            foreach (var act in res.Activities) {
                                                act.Id = 0; act.ResultId = 0;
                                                if (act.Indicators != null) {
                                                    foreach(var ind in act.Indicators) { ind.Id = 0; ind.ActivityId = 0; }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (project.BudgetItems != null) {
                            foreach(var bi in project.BudgetItems) {
                                bi.Id = 0; bi.ProjectId = 0;
                            }
                        }

                        cloudDb.Projects.Add(project);
                    }

                    await cloudDb.SaveChangesAsync();
                }

                ConnectionStatus = "✅ Migration terminée avec succès ! Vos données locales sont désormais sur le Cloud.";
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"❌ Erreur de migration : {ex.Message}";
                // Restore settings state
                Settings.UseCloud = false;
                DatabaseConfigManager.SaveSettings(Settings);
            }
            finally
            {
                IsMigrating = false;
            }
        }
    }
}


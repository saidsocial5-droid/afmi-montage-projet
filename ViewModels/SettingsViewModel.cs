using AFMI_Montage_projet_2.Services.Drive;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        // === Paramètres Cloud & Google Drive ===
        [ObservableProperty] private string _googleClientId = string.Empty;
        [ObservableProperty] private string _googleClientSecret = string.Empty;
        [ObservableProperty] private bool _isCloudConnected = false;
        [ObservableProperty] private string _cloudStatusMessage = "Non connecté";
        [ObservableProperty] private string _workspaceId = string.Empty;
        [ObservableProperty] private string _inviteEmail = string.Empty;
        // === Paramètres IA Existants ===
        [ObservableProperty] private string _selectedProvider = "Gemini";
        [ObservableProperty] private string _apiKey = string.Empty;
        [ObservableProperty] private string _selectedModel = "gemini-3.6-flash";
        [ObservableProperty] private ObservableCollection<string> _availableModels = new();
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _useLocalByDefault = true;
        [ObservableProperty] private bool _cloudOptInGranted = false;
        [ObservableProperty] private string _ollamaUrl = "http://localhost:11434";
        [ObservableProperty] private string _ollamaModel = "";
        [ObservableProperty] private bool _dataMinimizationEnabled = true;
        [ObservableProperty] private int _monthlyRequestLimit = 200;
        [ObservableProperty] private string _diagnosticResult = "Cliquez sur 'Lancer le diagnostic' pour tester les capacités de votre ordinateur.";

        // === Paramètres Typographie & Apparence ===
        [ObservableProperty] private string _selectedBaseFont = "Segoe UI";
        [ObservableProperty] private string _selectedHeadingFont = "Segoe UI";
        [ObservableProperty] private string _selectedArabicFont = "Tahoma";
        [ObservableProperty] private string _selectedFontSize = "Normal";

        public ObservableCollection<string> AvailableBaseFonts { get; } = new(new[] { "Inter", "Roboto", "Open Sans", "Lato", "Poppins", "Nunito", "Source Sans 3", "Noto Sans", "IBM Plex Sans", "Segoe UI", "Arial" });
        public ObservableCollection<string> AvailableHeadingFonts { get; } = new(new[] { "Merriweather", "Lora", "Source Serif 4", "Noto Serif", "Segoe UI", "Inter", "Roboto", "Times New Roman" });
        public ObservableCollection<string> AvailableArabicFonts { get; } = new(new[] { "Cairo", "Tajawal", "Noto Naskh Arabic", "Amiri", "IBM Plex Sans Arabic", "Tahoma", "Arial" });
        public ObservableCollection<string> AvailableFontSizes { get; } = new(new[] { "Petit", "Normal", "Grand", "Très grand" });

        // === Langue des réponses IA (ADR 26) ===
        public sealed record LanguageOption(string Code, string Label);

        public LanguageOption[] ResponseLanguageOptions { get; } =
        {
            new(Services.Language.AppLanguages.Auto, "Automatique (langue de la demande ou du texte)"),
            new(Services.Language.AppLanguages.French, "Français"),
            new(Services.Language.AppLanguages.Arabic, "العربية"),
            new(Services.Language.AppLanguages.English, "English")
        };

        [ObservableProperty] private LanguageOption? _selectedResponseLanguage;
        private bool _loadingSettings;

        partial void OnSelectedResponseLanguageChanged(LanguageOption? value)
        {
            if (_loadingSettings || value == null) return;
            SettingsService.SaveSetting(AiRouterService.ResponseLanguageSetting, value.Code);
            StatusMessage = value.Code == Services.Language.AppLanguages.Auto
                ? "Les réponses de l'IA suivront la langue de la demande ou du texte traité."
                : $"Les réponses de l'IA seront rédigées en {value.Label} (traduites automatiquement si le modèle répond dans une autre langue).";
        }

        // === Mises à jour par GitHub (ADR 26) ===
        [ObservableProperty] private string _gitHubRepository = string.Empty;
        [ObservableProperty] private string _updateStatus = string.Empty;
        [ObservableProperty] private bool _isCheckingUpdates;

        public string CurrentVersion => AutoUpdaterService.CurrentVersion;

        [ObservableProperty] private string _gitHubTokenStatus = string.Empty;

        /// <summary>Mise à jour automatique (ADR 29) : téléchargée au démarrage, installée à la fermeture.</summary>
        [ObservableProperty] private bool _autoInstallUpdates;

        /// <summary>État lisible du réglage (activée, désactivée ou pas encore choisie).</summary>
        [ObservableProperty] private string _autoInstallStatus = string.Empty;

        partial void OnAutoInstallUpdatesChanged(bool value)
        {
            if (_loadingSettings) return;
            Services.Updates.AutoUpdateCoordinator.AutoInstallEnabled = value;
            // Réactiver la mise à jour automatique relance aussi l'invitation à saisir la clé si elle manque
            if (value) Services.Updates.AutoUpdateCoordinator.TokenPromptEnabled = true;
            AutoInstallStatus = Services.Updates.AutoUpdateCoordinator.DescribeAutoInstall();
        }

        private void RefreshGitHubTokenStatus() =>
            GitHubTokenStatus = Services.Updates.GitHubUpdateService.HasAccessToken
                ? "🔒 Clé d'accès enregistrée sur ce poste."
                : "Aucune clé d'accès (nécessaire si le dépôt est privé).";

        public void SaveGitHubToken(string token)
        {
            if (!Services.Updates.GitHubUpdateService.LooksLikeToken(token))
            {
                UpdateStatus = "Clé d'accès non reconnue : elle commence par github_pat_ (ou ghp_).";
                return;
            }
            Services.Updates.GitHubUpdateService.SaveAccessToken(token);
            Services.Updates.AutoUpdateCoordinator.TokenPromptEnabled = true;
            RefreshGitHubTokenStatus();
            UpdateStatus = "Clé d'accès enregistrée (chiffrée). Cliquez sur « Rechercher une mise à jour » pour la tester.";
        }

        [RelayCommand]
        private void ClearGitHubToken()
        {
            Services.Updates.GitHubUpdateService.SaveAccessToken(null);
            RefreshGitHubTokenStatus();
            UpdateStatus = "Clé d'accès supprimée de ce poste.";
        }

        [RelayCommand]
        private void SaveGitHubRepository()
        {
            string value = GitHubRepository.Trim();
            if (value.Length > 0 && !Services.Updates.GitHubUpdateService.IsValidRepository(value))
            {
                UpdateStatus = "Format attendu : propriétaire/dépôt (ex. afmi-ifrane/afmi-montage-projet) ou l'adresse github.com du dépôt.";
                return;
            }
            string normalized = value.Length == 0 ? string.Empty : Services.Updates.GitHubUpdateService.NormalizeRepository(value);
            SettingsService.SaveSetting(Services.Updates.GitHubUpdateService.RepositorySetting, normalized);
            GitHubRepository = normalized.Length == 0 ? Services.Updates.GitHubUpdateService.Repository : normalized;
            UpdateStatus = GitHubRepository.Length == 0 ? "Aucun dépôt configuré." : $"Dépôt des mises à jour : {GitHubRepository}";
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task CheckGitHubUpdatesAsync()
        {
            IsCheckingUpdates = true;
            UpdateStatus = "Vérification des mises à jour…";
            try
            {
                var check = await new Services.Updates.GitHubUpdateService().CheckAsync();
                UpdateStatus = check.Message;
                // Recherche demandée explicitement : l'invitation à saisir la clé s'affiche même si elle avait été refusée
                if (check.NeedsAccessToken)
                {
                    var validated = await Views.GitHubTokenWindow.PromptAsync(Application.Current?.MainWindow, force: true);
                    if (validated == null) return;
                    check = validated;
                    RefreshGitHubTokenStatus();
                    UpdateStatus = check.Message;
                }
                // Même parcours qu'au démarrage : question sur la mise à jour automatique si elle n'est pas encore choisie
                if (check.UpdateAvailable && check.Release != null)
                {
                    await MainViewModel.ApplyGitHubUpdateAsync(check, new Services.Updates.GitHubUpdateService());
                    AutoInstallUpdates = Services.Updates.AutoUpdateCoordinator.AutoInstallEnabled;
                    AutoInstallStatus = Services.Updates.AutoUpdateCoordinator.DescribeAutoInstall();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(SettingsViewModel), "Vérification des mises à jour impossible.", ex);
                UpdateStatus = $"Vérification impossible : {ex.Message}";
            }
            finally
            {
                IsCheckingUpdates = false;
            }
        }

        // === Paramètres Appareils Mobiles ===
        [ObservableProperty] private ObservableCollection<LinkedDevice> _linkedDevices = new();

        public bool IsSuperAdmin => AFMI_Montage_projet_2.Services.SessionManager.CurrentUser?.IsSystemAdmin == true;

        public SettingsViewModel()
        {
            GoogleClientId = SettingsService.GetSetting("Google_ClientId", "");
            var encSecret = SettingsService.GetSetting("Google_ClientSecret", "");
            if (!string.IsNullOrEmpty(encSecret)) {
                try { GoogleClientSecret = EncryptionHelper.Unprotect(encSecret); } catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(SettingsViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
            }
            WorkspaceId = SettingsService.GetSetting("Google_WorkspaceId", "");
            if (SessionManager.CurrentDriveService != null)
            {
                IsCloudConnected = true;
                CloudStatusMessage = "Connecté à Google Drive";
            }
            LoadSettings();
            LoadLinkedDevices();
        }

        private void LoadSettings()
        {
            try
            {
                using var db = new AppDbContext();
                db.SeedDefaultData(); 

                var config = db.AppConfigs.FirstOrDefault();
                if (config != null)
                {
                    SelectedProvider = config.Provider;
                    ApiKey = EncryptionHelper.Unprotect(config.EncryptedApiKey);
                    SelectedModel = config.SelectedModel;

                    var localPrefs = LocalSettingsService.LoadSettings();
                    OllamaUrl = localPrefs.OllamaUrl;
                    OllamaModel = localPrefs.OllamaModel;
                    UseLocalByDefault = config.UseLocalByDefault;
                    CloudOptInGranted = config.CloudOptInGranted;
                    DataMinimizationEnabled = config.DataMinimizationEnabled;
                    MonthlyRequestLimit = config.MonthlyRequestLimit;
                }

                SelectedBaseFont = SettingsService.GetSetting("Font_Base", "Segoe UI");
                SelectedHeadingFont = SettingsService.GetSetting("Font_Heading", "Segoe UI");
                SelectedArabicFont = SettingsService.GetSetting("Font_Arabic", "Tahoma");
                SelectedFontSize = SettingsService.GetSetting("Font_Size", "Normal");

                _loadingSettings = true;
                AutoInstallUpdates = Services.Updates.AutoUpdateCoordinator.AutoInstallEnabled;
                AutoInstallStatus = Services.Updates.AutoUpdateCoordinator.DescribeAutoInstall();
                string language = SettingsService.GetSetting(AiRouterService.ResponseLanguageSetting, Services.Language.AppLanguages.Auto);
                SelectedResponseLanguage = ResponseLanguageOptions.FirstOrDefault(o => o.Code == language) ?? ResponseLanguageOptions[0];
                _loadingSettings = false;
                GitHubRepository = Services.Updates.GitHubUpdateService.Repository;
                RefreshGitHubTokenStatus();
            }
            catch (Exception ex)
            {
                _loadingSettings = false;
                AppLogger.Warn(nameof(SettingsViewModel), "Chargement des paramètres incomplet.", ex);
            }

            UpdateAvailableModels();
        }

        partial void OnSelectedProviderChanged(string value) => UpdateAvailableModels();

        private void UpdateAvailableModels()
        {
            AvailableModels.Clear();
            if (SelectedProvider == "Gemini") { AvailableModels.Add("gemini-3.6-flash"); AvailableModels.Add("gemini-3.6-pro"); AvailableModels.Add("gemini-flash-latest"); AvailableModels.Add("gemini-pro-latest"); if (!AvailableModels.Contains(SelectedModel)) SelectedModel = "gemini-3.6-flash"; }
            else if (SelectedProvider == "OpenAI") { AvailableModels.Add("gpt-4o-mini"); AvailableModels.Add("gpt-4o"); if (!AvailableModels.Contains(SelectedModel)) SelectedModel = "gpt-4o-mini"; }
            else if (SelectedProvider == "Claude") { AvailableModels.Add("claude-3-5-sonnet-20240620"); AvailableModels.Add("claude-3-opus-20240229"); if (!AvailableModels.Contains(SelectedModel)) SelectedModel = "claude-3-5-sonnet-20240620"; }
            else if (SelectedProvider == "DeepSeek") { AvailableModels.Add("deepseek-chat"); AvailableModels.Add("deepseek-reasoner"); if (!AvailableModels.Contains(SelectedModel)) SelectedModel = "deepseek-chat"; }
        }

        [RelayCommand]
        public void SaveSettings()
        {
            try
            {
                using var db = new AppDbContext();
                var config = db.AppConfigs.FirstOrDefault();
                string encryptedKey = EncryptionHelper.Protect(ApiKey.Trim());

                if (config == null)
                {
                    db.AppConfigs.Add(new AppConfig { Provider = SelectedProvider, EncryptedApiKey = encryptedKey, SelectedModel = SelectedModel, UseLocalByDefault = UseLocalByDefault, CloudOptInGranted = CloudOptInGranted, DataMinimizationEnabled = DataMinimizationEnabled, MonthlyRequestLimit = MonthlyRequestLimit });
                }
                else
                {
                    config.Provider = SelectedProvider; config.EncryptedApiKey = encryptedKey; config.SelectedModel = SelectedModel; config.UseLocalByDefault = UseLocalByDefault; config.CloudOptInGranted = CloudOptInGranted; config.DataMinimizationEnabled = DataMinimizationEnabled; config.MonthlyRequestLimit = MonthlyRequestLimit;
                }
                db.SaveChanges();

                var localPrefs = LocalSettingsService.LoadSettings();
                localPrefs.OllamaUrl = OllamaUrl;
                localPrefs.OllamaModel = OllamaModel;
                LocalSettingsService.SaveSettings(localPrefs);

                // Save typography
                SettingsService.SaveSetting("Font_Base", SelectedBaseFont);
                SettingsService.SaveSetting("Font_Heading", SelectedHeadingFont);
                SettingsService.SaveSetting("Font_Arabic", SelectedArabicFont);
                SettingsService.SaveSetting("Font_Size", SelectedFontSize);

                StatusMessage = "Tous les paramètres ont été enregistrés avec succès !";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur lors de la sauvegarde : {ex.Message}";
            }
        }

        [RelayCommand]
        public void ResetTypographyDefaults()
        {
            SelectedBaseFont = "Segoe UI";
            SelectedHeadingFont = "Segoe UI";
            SelectedArabicFont = "Tahoma";
            SelectedFontSize = "Normal";
            SaveSettings();
            StatusMessage = "Typographie réinitialisée aux valeurs par défaut.";
        }

        [RelayCommand]
        public void RunDiagnostic() { /* ... kept simple for now ... */ }
        [RelayCommand]
        public async System.Threading.Tasks.Task ConnectGoogleDrive()
        {
            if (string.IsNullOrWhiteSpace(GoogleClientId) || string.IsNullOrWhiteSpace(GoogleClientSecret))
            {
                CloudStatusMessage = "Veuillez entrer le Client ID et le Client Secret.";
                return;
            }

            SettingsService.SaveSetting("Google_ClientId", GoogleClientId);
            SettingsService.SaveSetting("Google_ClientSecret", EncryptionHelper.Protect(GoogleClientSecret));

            CloudStatusMessage = "Connexion en cours via le navigateur...";
            
            var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
            var driveService = await authService.AuthenticateAsync(GoogleClientId, GoogleClientSecret);

            if (driveService != null)
            {
                IsCloudConnected = true;
                SessionManager.CurrentDriveService = driveService;
                CloudStatusMessage = "Connecté avec succès à Google Drive !";

                if (string.IsNullOrEmpty(WorkspaceId))
                {
                    try
                    {
                        var manager = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveManager(driveService);
                        WorkspaceId = await manager.InitializeWorkspaceAsync();
                        SettingsService.SaveSetting("Google_WorkspaceId", WorkspaceId);
                        CloudStatusMessage = "Connecté et espace de travail prêt !";
                    }
                    catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(SettingsViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
                }
            }
            else
            {
                IsCloudConnected = false;
                CloudStatusMessage = "Échec de la connexion.";
            }
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task InitializeWorkspace()
        {
            if (!IsCloudConnected) return;
            try
            {
                CloudStatusMessage = "Vérification de l'identité...";
                var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
                var driveService = await authService.AuthenticateAsync(GoogleClientId, GoogleClientSecret);
                var email = await authService.GetUserEmailAsync();

                if (AFMI_Montage_projet_2.Services.SessionManager.CurrentUser?.IsSystemAdmin != true)
                {
                    CloudStatusMessage = "Seul l'administrateur principal peut créer un espace.";
                    return;
                }

                CloudStatusMessage = "Création de l'espace de travail...";
                var manager = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveManager(driveService!);
                WorkspaceId = await manager.InitializeWorkspaceAsync();
                SettingsService.SaveSetting("Google_WorkspaceId", WorkspaceId);
                CloudStatusMessage = "Espace de travail créé ! (ID: " + WorkspaceId + ")";
            }
            catch (Exception ex)
            {
                CloudStatusMessage = "Erreur: " + ex.Message;
            }
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task InviteMember()
        {
            if (!IsCloudConnected || string.IsNullOrWhiteSpace(WorkspaceId) || string.IsNullOrWhiteSpace(InviteEmail)) return;
            try
            {
                CloudStatusMessage = "Envoi de l'invitation à  " + InviteEmail + "...";
                var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
                var driveService = await authService.AuthenticateAsync(GoogleClientId, GoogleClientSecret);
                var manager = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveManager(driveService!);
                var emailToInvite = InviteEmail.Trim();
                await manager.InviteUserAsync(WorkspaceId, emailToInvite, "writer");
                CloudStatusMessage = "Invitation envoyée avec succès à  " + emailToInvite;
                InviteEmail = string.Empty;
            }
            catch (Exception ex)
            {
                CloudStatusMessage = "Erreur d'invitation: " + ex.Message;
            }
        }

                [RelayCommand]
        public async System.Threading.Tasks.Task PublishUpdate()
        {
            if (!IsCloudConnected || string.IsNullOrEmpty(WorkspaceId))
            {
                MessageBox.Show("Veuillez d'abord initialiser l'espace Google Drive.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Sélectionner le nouveau fichier Setup.exe"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string version = Microsoft.VisualBasic.Interaction.InputBox("Entrez le numéro de version (ex: 1.5):", "Version de la mise à jour", "");
                if (string.IsNullOrWhiteSpace(version)) return;

                string releaseNotes = Microsoft.VisualBasic.Interaction.InputBox("Entrez les notes de mise à jour:", "Notes", "Corrections de bugs et améliorations");

                CloudStatusMessage = "Publication de la mise à jour en cours...";
                try
                {
                    var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
                    var driveService = await authService.AuthenticateAsync(GoogleClientId, GoogleClientSecret);
                    var manager = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveManager(driveService!);
                    
                    var systemFolderId = await manager.GetOrCreateFolderAsync("_system", WorkspaceId);
                    var updateFolderId = await manager.GetOrCreateFolderAsync("update", systemFolderId);

                    // Upload exe
                    string exeName = $"AFMIMontageProjet_Setup_v{version}.exe";
                    var exeFileMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = exeName,
                        Parents = new System.Collections.Generic.List<string> { updateFolderId }
                    };
                    
                    using var stream = new System.IO.FileStream(openFileDialog.FileName, System.IO.FileMode.Open);
                    var request = driveService.Files.Create(exeFileMetadata, stream, "application/x-msdownload");
                    var uploadResult = await request.UploadAsync();
                    
                    if (uploadResult.Status != Google.Apis.Upload.UploadStatus.Completed)
                    {
                        CloudStatusMessage = "Erreur lors de l'upload du fichier EXE.";
                        return;
                    }
                    var uploadedExeId = request.ResponseBody.Id;

                    // Create version.json
                    var updateInfo = new AFMI_Montage_projet_2.Services.DriveUpdateInfo
                    {
                        Version = version,
                        FileId = uploadedExeId,
                        ReleaseNotes = releaseNotes,
                        // Vérifiée par chaque poste avant de lancer l'installateur
                        Sha256 = AFMI_Montage_projet_2.Services.Updates.GitHubUpdateService.ComputeSha256(openFileDialog.FileName)
                    };
                    string json = System.Text.Json.JsonSerializer.Serialize(updateInfo);
                    
                    // Check if version.json exists
                    string versionJsonId = null;
                    var searchReq = driveService.Files.List();
                    searchReq.Q = $"'{updateFolderId}' in parents and name = 'version.json' and trashed = false";
                    var searchRes = await searchReq.ExecuteAsync();
                    if (searchRes.Files != null && searchRes.Files.Count > 0) versionJsonId = searchRes.Files[0].Id;

                    using var jsonStream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                    if (string.IsNullOrEmpty(versionJsonId))
                    {
                        var jsonMeta = new Google.Apis.Drive.v3.Data.File { Name = "version.json", Parents = new System.Collections.Generic.List<string> { updateFolderId } };
                        await driveService.Files.Create(jsonMeta, jsonStream, "application/json").UploadAsync();
                    }
                    else
                    {
                        await driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), versionJsonId, jsonStream, "application/json").UploadAsync();
                    }

                    CloudStatusMessage = "Mise à jour publiée avec succès !";
                    MessageBox.Show("La mise à jour a été publiée. Les autres utilisateurs la recevront automatiquement.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CloudStatusMessage = "Erreur: " + ex.Message;
                }
            }
        }

                // === Join Workspace ===
        [ObservableProperty] private string _joinCode = string.Empty;

        [RelayCommand]
        public void GenerateJoinCode()
        {
            if (string.IsNullOrEmpty(GoogleClientId) || string.IsNullOrEmpty(GoogleClientSecret) || string.IsNullOrEmpty(WorkspaceId))
            {
                MessageBox.Show("Veuillez d'abord initialiser l'espace Google Drive et vous connecter.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string raw = $"{GoogleClientId}|{GoogleClientSecret}|{WorkspaceId}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
            string b64 = Convert.ToBase64String(bytes);
            
            Clipboard.SetText(b64);
            MessageBox.Show("Code de jointure copié dans le presse-papiers !\n\nEnvoyez ce code à votre collaborateur pour qu'il le colle dans 'Rejoindre un espace'.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public void JoinWorkspace()
        {
            if (string.IsNullOrWhiteSpace(JoinCode)) return;
            
            try
            {
                var bytes = Convert.FromBase64String(JoinCode);
                string raw = System.Text.Encoding.UTF8.GetString(bytes);
                var parts = raw.Split('|');
                
                if (parts.Length == 3)
                {
                    GoogleClientId = parts[0];
                    GoogleClientSecret = parts[1];
                    WorkspaceId = parts[2];
                    
                    SettingsService.SaveSetting("Google_ClientId", GoogleClientId);
                    SettingsService.SaveSetting("Google_ClientSecret", EncryptionHelper.Protect(GoogleClientSecret));
                    SettingsService.SaveSetting("Google_WorkspaceId", WorkspaceId);
                    SettingsService.SaveSetting("IsDesktopClient", "true"); // IMPORTANT: Mode Client
                    
                    MessageBox.Show("Code valide ! L'application est maintenant configurée en mode Client.\n\nVeuillez cliquer sur 'Connecter Google Drive' pour vous authentifier avec votre propre compte Google.", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    JoinCode = string.Empty;
                }
                else
                {
                    MessageBox.Show("Code invalide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch
            {
                MessageBox.Show("Code invalide ou corrompu.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadLinkedDevices()
        {
            try
            {
                using var db = new Models.AppDbContext();
                var userEmail = SessionManager.CurrentUser?.Email ?? "";
                var devices = db.LinkedDevices.Where(d => d.UserEmail == userEmail).ToList();
                LinkedDevices.Clear();
                foreach (var d in devices) LinkedDevices.Add(d);
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(SettingsViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task OpenPairingWindow()
        {
            var driveService = SessionManager.CurrentDriveService;
            if (driveService == null)
            {
                if (string.IsNullOrWhiteSpace(GoogleClientId) || string.IsNullOrWhiteSpace(GoogleClientSecret))
                {
                    System.Windows.MessageBox.Show("Veuillez d'abord renseigner vos identifiants Google (Client ID et Client Secret) dans l'onglet 'Espace de travail Cloud'.", "Configuration requise", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    CloudStatusMessage = "Connexion à Google Drive en cours...";
                    var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
                    driveService = await authService.AuthenticateAsync(GoogleClientId, GoogleClientSecret);
                    if (driveService != null)
                    {
                        SessionManager.CurrentDriveService = driveService;
                        IsCloudConnected = true;
                        CloudStatusMessage = "Connecté à Google Drive !";
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Impossible de se connecter à Google Drive : " + ex.Message, "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            if (driveService == null)
            {
                System.Windows.MessageBox.Show("Non connecté à Google Drive. Veuillez autoriser l'accès lors de la demande de connexion.", "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(WorkspaceId))
            {
                try
                {
                    CloudStatusMessage = "Initialisation de l'espace de travail Cloud...";
                    var manager = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveManager(driveService);
                    WorkspaceId = await manager.InitializeWorkspaceAsync();
                    SettingsService.SaveSetting("Google_WorkspaceId", WorkspaceId);
                    CloudStatusMessage = "Espace de travail prêt !";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Erreur lors de l'initialisation de l'espace Cloud : " + ex.Message, "Erreur", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
            }

            var pairingService = new DevicePairingService(driveService);
            var vm = new PairingViewModel(pairingService, WorkspaceId);
            vm.OnPairingSuccess = () => {
                System.Windows.Application.Current.Dispatcher.Invoke(() => LoadLinkedDevices());
            };

            var window = new Views.PairingWindow(vm)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            if (System.Windows.Application.Current.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                window.Owner = System.Windows.Application.Current.MainWindow;
            }
            window.ShowDialog();
        }

        [RelayCommand]
        private void RevokeDevice(LinkedDevice device)
        {
            if (device == null) return;
            var confirm = System.Windows.MessageBox.Show($"Révoquer l'appareil '{device.DeviceName}' ?", "Confirmer", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (confirm == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    using var db = new Models.AppDbContext();
                    var dbDevice = db.LinkedDevices.FirstOrDefault(d => d.Id == device.Id);
                    if (dbDevice != null)
                    {
                        db.LinkedDevices.Remove(dbDevice);
                        db.SaveChanges();
                        LoadLinkedDevices();
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Révocation Appareil", $"L'appareil {dbDevice.DeviceName} a été révoqué.");
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Erreur");
                }
            }
        }
    }
}























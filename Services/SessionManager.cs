using System;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Helpers;
using AFMI_Montage_projet_2.Services.Drive;
using Google.Apis.Drive.v3;

namespace AFMI_Montage_projet_2.Services
{
    public static class SessionManager
    {
        public static User? CurrentUser { get; private set; }
        public static DriveService? CurrentDriveService { get; set; }
        public static int? CurrentProjectId { get; set; }
        public static Project? CurrentProject { get; set; }

        public static void Login(User user)
        {
            CurrentUser = user;
        }

        public static void Logout()
        {
            CurrentUser = null;
            CurrentDriveService = null;
            CurrentProjectId = null;
            CurrentProject = null;
        }

        public static bool IsAuthenticated => CurrentUser != null;

        public static async System.Threading.Tasks.Task InitDriveAsync()
        {
            try
            {
                var clientId = SettingsService.GetSetting("Google_ClientId", "");
                var encSecret = SettingsService.GetSetting("Google_ClientSecret", "");
                var secret = "";
                if (!string.IsNullOrEmpty(encSecret)) {
                    try { secret = EncryptionHelper.Unprotect(encSecret); } catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(SessionManager), $"Erreur ignorée : {swallowedEx.Message}"); }
                }

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret)) return;

                var authService = new GoogleDriveAuthService();
                CurrentDriveService = await authService.AuthenticateAsync(clientId, secret);

                if (CurrentDriveService != null)
                {
                    var workspaceId = SettingsService.GetSetting("Google_WorkspaceId", "");
                    if (string.IsNullOrEmpty(workspaceId))
                    {
                        var manager = new GoogleDriveManager(CurrentDriveService);
                        workspaceId = await manager.InitializeWorkspaceAsync();
                        if (!string.IsNullOrEmpty(workspaceId))
                        {
                            SettingsService.SaveSetting("Google_WorkspaceId", workspaceId);
                        }
                    }
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(SessionManager), $"Erreur ignorée : {swallowedEx.Message}"); }
        }
    }
}

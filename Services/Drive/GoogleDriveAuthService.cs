using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using AFMI_Montage_projet_2.Services; // For SettingsService if needed
using Google.Apis.Auth.OAuth2.Flows;

namespace AFMI_Montage_projet_2.Services.Drive
{
    public class GoogleDriveAuthService
    {
        private UserCredential? _credential;
        private DriveService? _driveService;

        // Scopes: drive.file est restrictif et sécurisé.
        // On ajoute Oauth2.UserinfoEmail pour vérifier l'identité du Super Admin.
        private readonly string[] Scopes = { DriveService.Scope.Drive, "https://www.googleapis.com/auth/userinfo.email" };

        public bool IsAuthenticated => _credential != null;

        public async Task<DriveService?> AuthenticateAsync(string clientId, string clientSecret)
        {
            try
            {
                var secrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    Scopes,
                    "user", // On gère un seul compte actif localement pour cette instance
                    CancellationToken.None,
                    new DpapiDataStore()
                );

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = _credential,
                    ApplicationName = "AFMI Project Manager"
                });

                return _driveService;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Auth Error: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> GetUserEmailAsync()
        {
            if (_driveService == null) return null;
            try
            {
                var request = _driveService.About.Get();
                request.Fields = "user(emailAddress)";
                var about = await request.ExecuteAsync();
                return about.User?.EmailAddress;
            }
            catch
            {
                return null;
            }
        }

        public async Task LogoutAsync()
        {
            if (_credential != null)
            {
                await _credential.RevokeTokenAsync(CancellationToken.None);
                var ds = new DpapiDataStore();
                await ds.ClearAsync();
                _credential = null;
                _driveService = null;
            }
        }

        public DriveService? GetDriveService() => _driveService;
    }
}



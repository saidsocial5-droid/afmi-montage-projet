using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;
using AFMI_Montage_projet_2.Security;
using AFMI_Montage_projet_2.Services.Drive;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>
    /// خدمة التوثيق وإدارة الهوية الفردية عبر Google OAuth 2.0 (Loopback + PKCE)
    /// تستخرج الهوية من الـ ID Token الموثوق، وتخزن المفاتيح مشفرة بـ DPAPI في %LOCALAPPDATA%.
    /// Chaque membre se connecte avec son propre compte Google ; l'espace d'équipe reste celui du compte officiel (ADR 23).
    /// </summary>
    public class AuthService
    {
        private static AuthService? _instance;
        public static AuthService Instance => _instance ??= new AuthService();

        public const string WorkspaceFolderSetting = "Collab_WorkspaceFolderId";
        public const string AutoSignInSetting = "Collab_AutoSignIn";

        public static readonly string[] Scopes = new[]
        {
            "openid",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/userinfo.profile",
            DriveService.Scope.Drive // نطاق درايف للوصول إلى مجلد مساحة العمل المشترك
        };

        private UserCredential? _credential;
        private DriveService? _driveService;
        private ICollabStore? _collabStore;
        private UserProfile? _currentUser;
        private TeamConfig? _currentTeamConfig;

        public UserCredential? Credential => _credential;
        public DriveService? DriveService => _driveService;
        public ICollabStore? CollabStore => _collabStore;
        public UserProfile? CurrentUser => _currentUser;
        public TeamConfig? CurrentTeamConfig => _currentTeamConfig;
        public bool IsAuthenticated => _credential != null && _currentUser != null && _currentUser.IsAuthenticated;

        /// <summary>Connecté et l'espace d'équipe du compte officiel est accessible.</summary>
        public bool IsWorkspaceReady => IsAuthenticated && _collabStore != null;

        /// <summary>Dernière erreur de connexion, en français, destinée à l'utilisateur.</summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// Poste officiel : connecté avec le compte propriétaire de l'espace et base SQLite locale.
        /// C'est sa base qui fait foi ; il intègre les propositions approuvées et publie la version officielle.
        /// </summary>
        public bool IsOfficialStation => _officialStationOverride ??
            (IsWorkspaceReady && SecurityConstants.IsWorkspaceOwner(_currentUser?.Email) && !AppDbContext.IsCloudDatabaseConfigured);

        private bool? _officialStationOverride;

        public event EventHandler<UserProfile?>? UserStateChanged;

        public AuthService()
        {
        }

        /// <summary>
        /// Session hors Google (tests, outils) : identité et équipe fournies, sans effet sur <see cref="Instance"/>,
        /// seule session utilisée par l'application et alimentée exclusivement par OAuth.
        /// </summary>
        public static AuthService CreateDetached(UserProfile user, TeamConfig teamConfig, bool isOfficialStation)
        {
            user.Role = teamConfig.RoleOf(user.Email);
            return new AuthService
            {
                _currentUser = user,
                _currentTeamConfig = teamConfig,
                _officialStationOverride = isOfficialStation
            };
        }

        /// <summary>Peut approuver ou rejeter des propositions.</summary>
        public bool CanDecide() => _currentUser != null &&
            (SecurityConstants.IsSuperAdmin(_currentUser.Email) || (_currentTeamConfig?.CanDecide(_currentUser.Email) ?? false));

        /// <summary>Lit l'identifiant client OAuth (Paramètres ou code d'accès de l'équipe).</summary>
        public static bool TryGetClientCredentials(out string clientId, out string clientSecret)
        {
            clientId = SettingsService.GetSetting("Google_ClientId", "");
            clientSecret = "";
            var encSecret = SettingsService.GetSetting("Google_ClientSecret", "");
            if (!string.IsNullOrEmpty(encSecret))
            {
                try { clientSecret = AFMI_Montage_projet_2.Helpers.EncryptionHelper.Unprotect(encSecret); }
                catch (Exception ex) { AppLogger.Warn(nameof(AuthService), "Client Secret Google illisible sur ce poste.", ex); }
            }
            return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        }

        /// <summary>
        /// Rétablit la session au démarrage si l'utilisateur s'était connecté auparavant et qu'un jeton est mémorisé
        /// (aucune fenêtre de navigateur n'est ouverte dans le cas normal).
        /// </summary>
        public async Task TryRestoreSessionAsync()
        {
            if (IsAuthenticated) return;
            if (SettingsService.GetSetting(AutoSignInSetting, "false") != "true") return;
            if (!new DpapiDataStore().HasStoredToken()) return;
            if (!TryGetClientCredentials(out var id, out var secret)) return;

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await SignInAsync(id, secret, cts.Token);
        }

        /// <summary>
        /// تسجيل الدخول أو استعادة الجلسة المحفوظة
        /// </summary>
        public async Task<bool> SignInAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
        {
            LastError = null;
            try
            {
                var secrets = new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };

                // Loopback listener + PKCE via GoogleWebAuthorizationBroker
                _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    Scopes,
                    "user", // حساب العضو الحالي على هذا الحاسوب
                    cancellationToken,
                    new DpapiDataStore()
                );

                if (_credential == null) return false;

                _driveService = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = _credential,
                    ApplicationName = "AFMI Project Manager Collaborative"
                });

                // استخراج هوية المستخدم من ID Token أو من Drive UserInfo
                _currentUser = await ExtractUserProfileAsync(_credential, _driveService, cancellationToken);
                if (!_currentUser.IsAuthenticated)
                {
                    LastError = "Impossible de lire l'adresse e-mail du compte Google.";
                    return false;
                }

                SettingsService.SaveSetting(AutoSignInSetting, "true");

                // Espace d'équipe du compte officiel
                var store = new GoogleDriveCollabStore(_driveService, _currentUser.Email, SettingsService.GetSetting(WorkspaceFolderSetting, ""));
                try
                {
                    await store.EnsureWorkspaceStructureAsync(cancellationToken);
                }
                catch (WorkspaceAccessException ex)
                {
                    LastError = ex.Message;
                    _collabStore = null;
                    _currentTeamConfig = new TeamConfig { QuorumRule = QuorumRules.AdminDecision };
                    _currentUser.Role = _currentTeamConfig.RoleOf(_currentUser.Email);
                    UserStateChanged?.Invoke(this, _currentUser);
                    return false;
                }

                _collabStore = store;
                if (!string.IsNullOrEmpty(store.WorkspaceFolderId))
                {
                    SettingsService.SaveSetting(WorkspaceFolderSetting, store.WorkspaceFolderId);
                    // Canal des mises à jour de l'application (_system/update), publié dans le même dossier
                    if (string.IsNullOrEmpty(SettingsService.GetSetting("Google_WorkspaceId", "")))
                        SettingsService.SaveSetting("Google_WorkspaceId", store.WorkspaceFolderId);
                }

                await LoadTeamConfigAsync(cancellationToken);

                AppLogger.Info(nameof(AuthService), $"Connexion à l'espace d'équipe : {_currentUser.Email} ({_currentUser.Role}){(IsOfficialStation ? ", poste officiel" : "")}.");
                UserStateChanged?.Invoke(this, _currentUser);
                SyncService.Instance.Start();
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AuthService), "Échec de la connexion Google.", ex);
                LastError = "Connexion impossible : " + ex.Message;
                _currentUser = null;
                _collabStore = null;
                UserStateChanged?.Invoke(this, null);
                return false;
            }
        }

        /// <summary>
        /// Charge team.json. S'il est absent, un administrateur principal l'initialise ; la règle de décision
        /// de l'association (décision de l'administrateur seul) est appliquée.
        /// </summary>
        private async Task LoadTeamConfigAsync(CancellationToken cancellationToken)
        {
            if (_collabStore == null || _currentUser == null) return;
            bool isSuperAdmin = SecurityConstants.IsSuperAdmin(_currentUser.Email);

            var config = await _collabStore.GetTeamConfigAsync(cancellationToken);
            if (config == null && isSuperAdmin)
            {
                config = new TeamConfig
                {
                    TeamId = "afmi-workspace-main",
                    TeamName = "Équipe AFMI",
                    QuorumRule = QuorumRules.AdminDecision,
                    UpdatedByEmail = _currentUser.Email,
                    Members = SecurityConstants.SuperAdminEmails
                        .Select(e => new TeamMemberDto { Email = e, Name = e.Split('@')[0], Role = TeamRoles.Admin, AddedAt = DateTime.UtcNow })
                        .ToList()
                };
                await _collabStore.SaveTeamConfigAsync(config, cancellationToken);
            }
            else if (config != null && isSuperAdmin && config.QuorumRule != QuorumRules.AdminDecision)
            {
                config.QuorumRule = QuorumRules.AdminDecision;
                config.UpdatedByEmail = _currentUser.Email;
                await _collabStore.SaveTeamConfigAsync(config, cancellationToken);
            }

            // Jamais null après connexion : sinon les contrôles de rôle seraient contournés
            _currentTeamConfig = config ?? new TeamConfig { QuorumRule = QuorumRules.AdminDecision };
            _currentUser.Role = _currentTeamConfig.RoleOf(_currentUser.Email);
        }

        /// <summary>Recharge team.json (après une modification distante).</summary>
        public async Task RefreshTeamConfigAsync(CancellationToken cancellationToken = default)
        {
            await LoadTeamConfigAsync(cancellationToken);
            UserStateChanged?.Invoke(this, _currentUser);
        }

        /// <summary>
        /// استخراج معلومات المستخدم الموثوقة من الـ ID Token المشفر أو واجهة About
        /// </summary>
        private async Task<UserProfile> ExtractUserProfileAsync(UserCredential credential, DriveService driveService, CancellationToken cancellationToken)
        {
            string email = string.Empty;
            string displayName = string.Empty;
            string? picture = null;

            // 1. محاولة قراءة ID Token إن توفر
            string? idToken = credential.Token?.IdToken;
            if (!string.IsNullOrEmpty(idToken))
            {
                try
                {
                    var payload = ParseJwtPayload(idToken);
                    if (payload.TryGetValue("email", out var emailVal)) email = emailVal?.ToString() ?? "";
                    if (payload.TryGetValue("name", out var nameVal)) displayName = nameVal?.ToString() ?? "";
                    if (payload.TryGetValue("picture", out var picVal)) picture = picVal?.ToString();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(AuthService), "ID Token illisible, lecture du profil via Drive.", ex);
                }
            }

            // 2. كخيار احتياطي ومؤكد، استرجاع المستخدم من Google Drive About API
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(displayName))
            {
                try
                {
                    var aboutReq = driveService.About.Get();
                    aboutReq.Fields = "user(displayName, emailAddress, photoLink)";
                    var about = await aboutReq.ExecuteAsync(cancellationToken);
                    if (about?.User != null)
                    {
                        if (string.IsNullOrEmpty(email)) email = about.User.EmailAddress ?? "";
                        if (string.IsNullOrEmpty(displayName)) displayName = about.User.DisplayName ?? email.Split('@')[0];
                        picture ??= about.User.PhotoLink;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(AuthService), "Lecture du profil Google via Drive impossible.", ex);
                }
            }

            if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(email))
            {
                displayName = email.Split('@')[0];
            }

            return new UserProfile
            {
                Email = email,
                DisplayName = displayName,
                PictureUrl = picture,
                IsAuthenticated = !string.IsNullOrEmpty(email),
                Role = TeamRoles.Guest
            };
        }

        private static System.Collections.Generic.Dictionary<string, object?> ParseJwtPayload(string jwt)
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return new();

            string base64 = parts[1].Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(base64);
            string json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object?>>(json) ?? new();
        }

        /// <summary>Gestion de l'équipe : réservée aux administrateurs principaux (seuls auteurs fiables de team.json).</summary>
        public bool CanManageTeam() => IsWorkspaceReady && SecurityConstants.IsSuperAdmin(_currentUser?.Email);

        /// <summary>
        /// تحديث إعدادات الفريق (متاح للمدير فقط)
        /// </summary>
        public async Task<bool> UpdateTeamConfigAsync(TeamConfig newConfig, CancellationToken cancellationToken = default)
        {
            if (_collabStore == null || _currentUser == null) return false;

            if (!CanManageTeam())
            {
                throw new UnauthorizedAccessException("Seuls les administrateurs de l'association peuvent modifier l'équipe.");
            }

            newConfig.UpdatedByEmail = _currentUser.Email;
            newConfig.LastUpdatedAt = DateTime.UtcNow;

            await _collabStore.SaveTeamConfigAsync(newConfig, cancellationToken);
            _currentTeamConfig = newConfig;
            return true;
        }

        /// <summary>
        /// دعوة عضو جديد للفريق ومشاركة المجلد معه
        /// </summary>
        public async Task<bool> InviteMemberAsync(string email, string role, CancellationToken cancellationToken = default)
        {
            if (_collabStore == null || _currentUser == null) return false;
            if (!CanManageTeam())
            {
                throw new UnauthorizedAccessException("Seuls les administrateurs de l'association peuvent inviter des membres.");
            }

            bool success = await _collabStore.InviteMemberAsync(email, role, cancellationToken);
            if (success)
            {
                await LoadTeamConfigAsync(cancellationToken);
            }
            return success;
        }

        public async Task<bool> RemoveMemberAsync(string email, CancellationToken cancellationToken = default)
        {
            if (_collabStore == null || _currentUser == null) return false;
            if (!CanManageTeam())
            {
                throw new UnauthorizedAccessException("Seuls les administrateurs de l'association peuvent retirer des membres.");
            }
            if (SecurityConstants.IsSuperAdmin(email))
            {
                throw new InvalidOperationException("Un administrateur principal ne peut pas être retiré de l'équipe.");
            }

            bool success = await _collabStore.RemoveMemberAsync(email, cancellationToken);
            if (success)
            {
                await LoadTeamConfigAsync(cancellationToken);
            }
            return success;
        }

        public bool IsCurrentUserAdmin()
        {
            return _currentUser != null &&
                   (SecurityConstants.IsSuperAdmin(_currentUser.Email) || (_currentTeamConfig?.IsAdmin(_currentUser.Email) ?? false));
        }

        public async Task LogoutAsync()
        {
            try
            {
                SyncService.Instance.Stop();
                SettingsService.SaveSetting(AutoSignInSetting, "false");
                if (_credential != null)
                {
                    try { await _credential.RevokeTokenAsync(CancellationToken.None); }
                    catch (Exception ex) { AppLogger.Warn(nameof(AuthService), "Révocation du jeton Google impossible (déjà expiré ?).", ex); }
                    var dataStore = new DpapiDataStore();
                    await dataStore.ClearAsync();
                }
            }
            finally
            {
                _credential = null;
                _driveService = null;
                _collabStore = null;
                _currentUser = null;
                _currentTeamConfig = null;
                LastError = null;
                UserStateChanged?.Invoke(this, null);
            }
        }
    }
}

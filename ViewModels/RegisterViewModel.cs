using System;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string? _backgroundImagePath;

        public Action? OnRegisterSuccess { get; set; }
        public Action? OnCancel { get; set; }

        public RegisterViewModel()
        {
            var settings = AFMI_Montage_projet_2.Services.LocalSettingsService.LoadSettings();
            BackgroundImagePath = settings.BackgroundImagePath;
        }

        [RelayCommand]
        private void Register(object? passwordBox)
        {
            var pb = passwordBox as System.Windows.Controls.PasswordBox;
            string password = pb?.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Veuillez remplir tous les champs.";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                if (db.Users.Any(u => u.Username == Username || u.Email == Email))
                {
                    ErrorMessage = "Ce nom d'utilisateur ou cet email existe déjà.";
                    return;
                }

                bool isFirstUser = !db.Users.Any();
                bool isSuperAdmin = isFirstUser || AFMI_Montage_projet_2.Security.SecurityConstants.IsSuperAdmin(Email.Trim());

                var user = new User
                {
                    Username = Username.Trim(),
                    Email = Email.Trim(),
                    PasswordHash = password, // Note: L'utilisateur devra lier son compte Google pour valider l'email, ou on s'inscrit directement via Google.
                    IsSystemAdmin = isSuperAdmin,
                    Status = isSuperAdmin ? "Active" : "Pending",
                    Role = isSuperAdmin ? "SuperAdmin" : "Reader"
                };

                db.Users.Add(user);
                db.SaveChanges();

                if (isSuperAdmin)
                {
                    AFMI_Montage_projet_2.Security.AuditLogger.Log("Inscription", "Compte Super Admin créé et activé automatiquement.", user.Email);
                    MessageBox.Show("Compte Super Admin créé et activé !", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    AFMI_Montage_projet_2.Security.AuditLogger.Log("Inscription", "Nouvelle inscription en attente d'approbation.", user.Email);
                    MessageBox.Show("Compte créé avec succès ! Votre compte est en attente d'approbation par l'administrateur.", "En attente", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                OnRegisterSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Erreur : " + ex.Message;
            }
        }

        /// <summary>Vrai pendant la connexion Google (bouton désactivé, message d'attente affiché).</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterWithGoogleCommand))]
        private bool _isGoogleBusy;

        /// <summary>Affiche le champ « code d'accès de l'équipe » quand ce poste n'a pas l'identifiant Google.</summary>
        [ObservableProperty] private bool _showAccessCodePanel;
        [ObservableProperty] private string _accessCodeInput = string.Empty;

        private bool CanRegisterWithGoogle() => !IsGoogleBusy;

        /// <summary>
        /// Inscription Google : ouvre le navigateur sur la page de connexion Google et récupère l'email du compte choisi.
        /// Il faut l'identifiant client OAuth de l'association sur ce poste (Paramètres, ou code d'accès de l'équipe) :
        /// s'il manque, la fenêtre l'explique et propose de coller le code d'accès (ADR 31).
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRegisterWithGoogle))]
        private async System.Threading.Tasks.Task RegisterWithGoogle()
        {
            if (!Services.Collab.AuthService.TryGetClientCredentials(out var clientId, out var secret))
            {
                ShowAccessCodePanel = true;
                ErrorMessage = "La connexion Google n'est pas encore configurée sur ce poste. Collez ci-dessous le code d'accès de l'équipe " +
                               "(l'administrateur le copie depuis Espace d'équipe › « Copier le code d'accès »), puis cliquez sur « Valider le code ».";
                return;
            }

            IsGoogleBusy = true;
            ErrorMessage = "Ouverture du navigateur… Choisissez votre compte Google puis revenez dans le logiciel.";
            try
            {
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(5));
                var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
                var driveService = await authService.AuthenticateAsync(clientId, secret, timeout.Token);
                if (driveService == null)
                {
                    ErrorMessage = "Connexion Google impossible : " + DescribeGoogleError(authService.LastError);
                    return;
                }

                var email = await authService.GetUserEmailAsync();
                if (string.IsNullOrEmpty(email))
                {
                    ErrorMessage = "Impossible de récupérer l'email depuis Google : " + DescribeGoogleError(authService.LastError);
                    return;
                }

                Email = email;
                Username = email.Split('@')[0];
                ErrorMessage = "Email vérifié avec Google. Choisissez un mot de passe puis cliquez sur « S'inscrire ».";
            }
            catch (Exception ex)
            {
                Services.AppLogger.Warn(nameof(RegisterViewModel), "Inscription Google impossible.", ex);
                ErrorMessage = "Erreur Google : " + DescribeGoogleError(ex.Message);
            }
            finally
            {
                IsGoogleBusy = false;
            }
        }

        [RelayCommand]
        private void ApplyAccessCode()
        {
            if (!Services.Collab.AuthService.TryApplyAccessCode(AccessCodeInput, out var error))
            {
                ErrorMessage = error;
                return;
            }
            AccessCodeInput = string.Empty;
            ShowAccessCodePanel = false;
            ErrorMessage = "Code d'accès enregistré. Cliquez maintenant sur « S'inscrire avec Google ».";
        }

        /// <summary>Traduit les erreurs OAuth les plus courantes en consignes compréhensibles.</summary>
        public static string DescribeGoogleError(string? error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "aucune réponse de Google.";
            if (error.Contains("invalid_client", StringComparison.OrdinalIgnoreCase))
                return "l'identifiant client Google enregistré sur ce poste est refusé par Google (identifiant ou secret erroné, ou client supprimé). Demandez un nouveau code d'accès à l'administrateur.";
            if (error.Contains("redirect_uri", StringComparison.OrdinalIgnoreCase))
                return "le client Google doit être de type « Application de bureau » dans Google Cloud Console.";
            if (error.Contains("access_denied", StringComparison.OrdinalIgnoreCase))
                return "accès refusé. Si l'application Google est « en test », ajoutez votre adresse dans les utilisateurs de test de l'écran de consentement (Google Cloud Console).";
            return error;
        }

        [RelayCommand]
        private void Cancel() => OnCancel?.Invoke();
    }
}


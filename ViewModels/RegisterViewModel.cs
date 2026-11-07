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

        [RelayCommand]
        private async System.Threading.Tasks.Task RegisterWithGoogle()
        {
            try
            {
                var authService = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveAuthService();
                
                // On a besoin des credentials configurés (si non présents, on ne peut pas faire d'OAuth)
                var clientId = Services.SettingsService.GetSetting("Google_ClientId", "");
                var encSecret = Services.SettingsService.GetSetting("Google_ClientSecret", "");
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(encSecret))
                {
                    ErrorMessage = "L'intégration Google n'est pas configurée par l'administrateur.";
                    return;
                }
                
                string secret = "";
                try { secret = AFMI_Montage_projet_2.Helpers.EncryptionHelper.Unprotect(encSecret); } catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(RegisterViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }

                var driveService = await authService.AuthenticateAsync(clientId, secret);
                var email = await authService.GetUserEmailAsync();

                if (string.IsNullOrEmpty(email))
                {
                    ErrorMessage = "Impossible de récupérer l'email depuis Google.";
                    return;
                }

                Email = email; // Remplit le champ
                Username = email.Split('@')[0]; // Génère un nom d'utilisateur par défaut
                ErrorMessage = "Email vérifié avec Google. Veuillez saisir un mot de passe et valider.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Erreur Google Auth : " + ex.Message;
            }
        }

        [RelayCommand]
        private void Cancel() => OnCancel?.Invoke();
    }
}


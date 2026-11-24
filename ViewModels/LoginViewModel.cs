using System;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Microsoft.Win32;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _usernameOrEmail = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _rememberMe;

        [ObservableProperty]
        private string? _backgroundImagePath;

        public Action? OnLoginSuccess { get; set; }

        public LoginViewModel()
        {
            var settings = LocalSettingsService.LoadSettings();
            BackgroundImagePath = settings.BackgroundImagePath;
            RememberMe = settings.RememberMe;
            if (RememberMe)
            {
                UsernameOrEmail = settings.SavedUsername ?? string.Empty;
            }
        }

        [RelayCommand]
        private void ChangeBackground()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Choisir une image de fond",
                Filter = "Fichiers image|*.jpg;*.jpeg;*.png;*.bmp",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                BackgroundImagePath = openFileDialog.FileName;
                var settings = LocalSettingsService.LoadSettings();
                settings.BackgroundImagePath = BackgroundImagePath;
                LocalSettingsService.SaveSettings(settings);
            }
        }

        public Action? OnNavigateRegister { get; set; }

        [RelayCommand]
        private void NavigateRegister()
        {
            OnNavigateRegister?.Invoke();
        }

        [RelayCommand]
        private void Login(object parameter)
        {
            var passwordBox = parameter as System.Windows.Controls.PasswordBox;
            if (passwordBox == null) return;
            string password = passwordBox.Password;

            if (string.IsNullOrWhiteSpace(UsernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Veuillez saisir votre email et mot de passe.";
                return;
            }

            try
            {
                using var db = new AppDbContext();

                // Vérification hors base : le mot de passe n'est plus comparé en SQL (empreinte PBKDF2, ADR 35).
                var login = UsernameOrEmail.Trim();
                User? user = null;
                bool needsRehash = false;
                foreach (var candidate in db.Users.Where(u => u.Email == login || u.Username == login).ToList())
                {
                    if (AFMI_Montage_projet_2.Security.PasswordHasher.Verify(candidate.PasswordHash, password, out needsRehash))
                    {
                        user = candidate;
                        break;
                    }
                }

                if (user != null && needsRehash)
                {
                    // Ancien compte enregistré en clair : conversion transparente à la première connexion réussie.
                    user.PasswordHash = AFMI_Montage_projet_2.Security.PasswordHasher.Hash(password);
                    db.SaveChanges();
                }

                if (user != null)
                {
                    if (user.Status == "Pending")
                    {
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Connexion bloquée", "Le compte est en attente d'approbation.", user.Email);
                        ErrorMessage = "Votre compte est en attente d'approbation par l'administrateur.";
                        return;
                    }
                    if (user.Status == "Suspended")
                    {
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Connexion bloquée", "Le compte a été suspendu.", user.Email);
                        ErrorMessage = "Votre compte a été suspendu. Veuillez contacter l'administrateur.";
                        return;
                    }

                    // Save remember me settings
                    var settings = LocalSettingsService.LoadSettings();
                    settings.RememberMe = RememberMe;
                    settings.SavedUsername = RememberMe ? UsernameOrEmail : null;
                    LocalSettingsService.SaveSettings(settings);

                    SessionManager.Login(user);
                    _ = SessionManager.InitDriveAsync();
                    AFMI_Montage_projet_2.Security.AuditLogger.Log("Connexion réussie", "L'utilisateur s'est connecté.", user.Email);
                    OnLoginSuccess?.Invoke();
                }
                else
                {
                    AFMI_Montage_projet_2.Security.AuditLogger.Log("Connexion échouée", $"Tentative échouée pour : {UsernameOrEmail}", UsernameOrEmail);
                    ErrorMessage = "Identifiants incorrects.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Erreur de connexion : {ex.Message}";
            }
        }
    }
}



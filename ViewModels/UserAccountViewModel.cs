using System;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class UserAccountViewModel : ObservableObject
    {
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _newPassword = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        private readonly User _currentUser;

        public UserAccountViewModel()
        {
            _currentUser = SessionManager.CurrentUser!;
            if (_currentUser != null)
            {
                Username = _currentUser.Username;
                Email = _currentUser.Email;
            }
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email))
            {
                StatusMessage = "Le nom d'utilisateur et l'email sont requis.";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var dbUser = db.Users.FirstOrDefault(u => u.Id == _currentUser.Id);
                if (dbUser != null)
                {
                    dbUser.Username = Username.Trim();
                    dbUser.Email = Email.Trim();
                    if (!string.IsNullOrWhiteSpace(NewPassword))
                    {
                        dbUser.PasswordHash = AFMI_Montage_projet_2.Security.PasswordHasher.Hash(NewPassword);
                    }
                    db.SaveChanges();

                    SessionManager.CurrentUser!.Username = dbUser.Username;
                    SessionManager.CurrentUser!.Email = dbUser.Email;

                    StatusMessage = "Compte mis à jour avec succès !";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur : " + ex.Message;
            }
        }
    }
}


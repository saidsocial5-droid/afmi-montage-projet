using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<User> _users = new();
        [ObservableProperty] private User? _selectedUser;
        [ObservableProperty] private string _newUsername = string.Empty;
        [ObservableProperty] private string _newEmail = string.Empty;
        [ObservableProperty] private bool _isSystemAdmin = false;
        [ObservableProperty] private string _selectedStatus = string.Empty;
        [ObservableProperty] private string _selectedRole = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public bool IsAdmin => AFMI_Montage_projet_2.Security.SecurityConstants.IsSuperAdmin(SessionManager.CurrentUser?.Email);

        public UserManagementViewModel()
        {
            LoadUsers();
        }

        private void LoadUsers()
        {
            using var db = new AppDbContext();
            Users = new ObservableCollection<User>(db.Users.ToList());
        }

        [RelayCommand]
        private void SelectUser(User? user)
        {
            if (user == null) return;
            SelectedUser = user;
            NewUsername = user.Username;
            NewEmail = user.Email;
            IsSystemAdmin = user.IsSystemAdmin;
            SelectedStatus = user.Status;
            SelectedRole = user.Role;
        }

        [RelayCommand]
        private void SaveUser()
        {
            if (SelectedUser == null)
            {
                StatusMessage = "Sélectionnez un utilisateur à modifier.";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var dbUser = db.Users.FirstOrDefault(u => u.Id == SelectedUser.Id);
                if (dbUser != null)
                {
                    bool isTargetSuperAdmin = AFMI_Montage_projet_2.Security.SecurityConstants.IsSuperAdmin(dbUser.Email);

                    dbUser.Username = NewUsername.Trim();
                    dbUser.Email = NewEmail.Trim();

                    if (isTargetSuperAdmin)
                    {
                        dbUser.IsSystemAdmin = true;
                        dbUser.Status = "Active";
                        dbUser.Role = "SuperAdmin";
                        StatusMessage = "Profil Super Admin mis à jour (statut/rôle figés).";
                    }
                    else
                    {
                        dbUser.IsSystemAdmin = IsSystemAdmin;
                        dbUser.Status = SelectedStatus;
                        dbUser.Role = SelectedRole;
                        StatusMessage = "Utilisateur mis à jour avec succès !";
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Modification Utilisateur", $"Statut: {SelectedStatus}, Rôle: {SelectedRole}, Admin: {IsSystemAdmin}", dbUser.Email);
                    }

                    db.SaveChanges();
                    LoadUsers();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur : " + ex.Message;
            }
        }

        [RelayCommand]
        private void DeleteUser()
        {
            if (SelectedUser == null) return;
            if (SelectedUser.Id == SessionManager.CurrentUser?.Id)
            {
                StatusMessage = "Vous ne pouvez pas supprimer votre propre compte.";
                return;
            }

            if (AFMI_Montage_projet_2.Security.SecurityConstants.IsSuperAdmin(SelectedUser.Email))
            {
                StatusMessage = "Le compte Super Admin ne peut pas être supprimé.";
                return;
            }

            var confirm = MessageBox.Show($"Supprimer l'utilisateur '{SelectedUser.Username}' ?", "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    using var db = new AppDbContext();
                    var dbUser = db.Users.FirstOrDefault(u => u.Id == SelectedUser.Id);
                    if (dbUser != null)
                    {
                        string emailDeleted = dbUser.Email;
                        db.Users.Remove(dbUser);
                        db.SaveChanges();
                        LoadUsers();
                        StatusMessage = "Utilisateur supprimé.";
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Suppression Utilisateur", $"Utilisateur {emailDeleted} supprimé définitivement.", emailDeleted);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = "Erreur : " + ex.Message;
                }
            }
        }

        [RelayCommand]
        private void ResetPassword()
        {
            if (SelectedUser == null) return;
            try
            {
                using var db = new AppDbContext();
                var dbUser = db.Users.FirstOrDefault(u => u.Id == SelectedUser.Id);
                if (dbUser != null)
                {
                    string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);
                    dbUser.PasswordHash = tempPassword;
                    db.SaveChanges();
                    StatusMessage = $"Mot de passe de '{dbUser.Username}' réinitialisé. Nouveau MDP : {tempPassword}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Erreur : " + ex.Message;
            }
        }
    }
}


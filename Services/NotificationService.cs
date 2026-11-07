using System;
using System.Linq;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services
{
    public static class NotificationService
    {
        public static void CreateNotification(int targetUserId, string title, string message)
        {
            try
            {
                using var db = new AppDbContext();
                db.Notifications.Add(new Notification
                {
                    UserId = targetUserId,
                    Title = title,
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }
            catch (Exception)
            {
                // Ignorer les erreurs de notification en silence
            }
        }

        public static void NotifyAdminsOfProject(int projectId, string title, string message)
        {
            try
            {
                using var db = new AppDbContext();
                // Trouver les admins du projet ou admins globaux
                var adminUsers = db.ProjectRoles
                    .Where(pr => pr.ProjectId == projectId && pr.RoleName == "Admin")
                    .Select(pr => pr.UserId)
                    .ToList();
                
                var systemAdmins = db.Users
                    .Where(u => u.IsSystemAdmin)
                    .Select(u => u.Id)
                    .ToList();

                var allAdminsToNotify = adminUsers.Union(systemAdmins).Distinct().ToList();

                // Ne pas notifier l'auteur de l'action lui-même
                if (SessionManager.CurrentUser != null)
                {
                    allAdminsToNotify.Remove(SessionManager.CurrentUser.Id);
                }

                foreach (var adminId in allAdminsToNotify)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = adminId,
                        Title = title,
                        Message = message,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                db.SaveChanges();
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(NotificationService), $"Erreur ignorée : {swallowedEx.Message}"); }
        }
    }
}


using System;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.Security
{
    public static class AuditLogger
    {
        public static void Log(string action, string details, string? userEmail = null)
        {
            try
            {
                if (string.IsNullOrEmpty(userEmail))
                {
                    userEmail = SessionManager.CurrentUser?.Email ?? "Système";
                }

                using var db = new AppDbContext();
                var log = new AuditLog
                {
                    Action = action,
                    Details = details,
                    UserEmail = userEmail,
                    Timestamp = DateTime.UtcNow
                };
                db.AuditLogs.Add(log);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // L'audit ne doit pas bloquer l'action, mais sa perte doit être tracée.
                AppLogger.Error(nameof(AuditLogger), $"Échec d'écriture de l'entrée d'audit '{action}': {details}", ex);
            }
        }
    }
}

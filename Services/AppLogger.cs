using System;
using System.IO;
using System.Linq;

namespace AFMI_Montage_projet_2.Services
{
    /// <summary>
    /// Journal technique de l'application (fichier texte quotidien).
    /// Remplace les Console.WriteLine invisibles dans une application WinExe
    /// et les catch vides qui masquaient les erreurs.
    /// Emplacement : %LOCALAPPDATA%\AFMI_Montage_Projet\logs\app_yyyyMMdd.log
    /// Ne lève jamais d'exception.
    /// </summary>
    public static class AppLogger
    {
        private static readonly object _lock = new();
        private const int RetentionDays = 14;
        private static bool _cleanupDone;

        public static string LogDirectory => Path.Combine(AFMI_Montage_projet_2.Models.AppDbContext.LocalDataDirectory, "logs");

        public static void Info(string source, string message) => Write("INFO", source, message, null);

        public static void Warn(string source, string message, Exception? ex = null) => Write("WARN", source, message, ex);

        public static void Error(string source, string message, Exception? ex = null) => Write("ERROR", source, message, ex);

        private static void Write(string level, string source, string message, Exception? ex)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{source}] {message}";
                if (ex != null) line += Environment.NewLine + "    " + ex.ToString().Replace(Environment.NewLine, Environment.NewLine + "    ");

                System.Diagnostics.Debug.WriteLine(line);

                lock (_lock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
                    if (!_cleanupDone) { _cleanupDone = true; CleanupOldLogs(); }
                }
            }
            catch
            {
                // Le journal ne doit jamais faire échouer l'application.
            }
        }

        private static void CleanupOldLogs()
        {
            foreach (var file in Directory.GetFiles(LogDirectory, "app_*.log")
                         .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-RetentionDays)))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }
}

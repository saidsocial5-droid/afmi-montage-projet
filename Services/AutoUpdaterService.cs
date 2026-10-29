using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Drive.v3;
using AFMI_Montage_projet_2.Helpers;

namespace AFMI_Montage_projet_2.Services
{
    public class DriveUpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string FileId { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public class AutoUpdaterService
    {
        private readonly DriveService _driveService;
        /// <summary>Version courante lue depuis l'assembly (définie par &lt;Version&gt; dans le .csproj).</summary>
        public static string CurrentVersion { get; } =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        public AutoUpdaterService(DriveService driveService)
        {
            _driveService = driveService;
        }

        public async Task<DriveUpdateInfo?> CheckForUpdatesAsync(string workspaceId)
        {
            try
            {
                var manager = new AFMI_Montage_projet_2.Services.Drive.GoogleDriveManager(_driveService);
                var systemFolderId = await manager.GetOrCreateFolderAsync("_system", workspaceId);
                var updateFolderId = await manager.GetOrCreateFolderAsync("update", systemFolderId);

                // Find version.json
                var request = _driveService.Files.List();
                request.Q = $"'{updateFolderId}' in parents and name = 'version.json' and trashed = false";
                request.Fields = "files(id, name, lastModifyingUser(emailAddress))";
                var result = await request.ExecuteAsync();

                if (result.Files != null && result.Files.Count > 0)
                {
                    // Tout membre de l'équipe peut écrire dans l'espace partagé : seule une publication d'administrateur est acceptée
                    string? publishedBy = result.Files[0].LastModifyingUser?.EmailAddress;
                    if (!Security.SecurityConstants.IsSuperAdmin(publishedBy))
                    {
                        AppLogger.Warn(nameof(AutoUpdaterService), $"version.json ignoré : déposé par {publishedBy ?? "inconnu"}, qui n'est pas administrateur.");
                        return null;
                    }

                    var fileId = result.Files[0].Id;
                    var getRequest = _driveService.Files.Get(fileId);
                    using var stream = new MemoryStream();
                    await getRequest.DownloadAsync(stream);
                    stream.Position = 0;
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    
                    var updateInfo = JsonSerializer.Deserialize<DriveUpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (updateInfo != null && IsNewerVersion(updateInfo.Version, CurrentVersion))
                    {
                        return updateInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(nameof(AutoUpdaterService), "Échec de la vérification des mises à jour.", ex);
            }
            return null;
        }

        public async Task DownloadAndApplyUpdateAsync(DriveUpdateInfo updateInfo, Action<double> progressCallback)
        {
            try
            {
                string tempFile = Path.Combine(Path.GetTempPath(), $"AFMI_Update_v{updateInfo.Version}.exe");

                // L'installateur lui-même doit avoir été déposé par un administrateur (et non remplacé par un membre)
                var metaRequest = _driveService.Files.Get(updateInfo.FileId);
                metaRequest.Fields = "id, lastModifyingUser(emailAddress), owners(emailAddress)";
                var meta = await metaRequest.ExecuteAsync();
                if (!Security.SecurityConstants.IsSuperAdmin(meta.LastModifyingUser?.EmailAddress))
                    throw new InvalidOperationException($"Installateur refusé : déposé par {meta.LastModifyingUser?.EmailAddress ?? "inconnu"}, qui n'est pas administrateur.");

                var request = _driveService.Files.Get(updateInfo.FileId);
                
                // Note: progress reporting for Google Drive download requires event hooking, 
                // for simplicity we'll just download it directly here.
                using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
                
                request.MediaDownloader.ProgressChanged += (Google.Apis.Download.IDownloadProgress progress) =>
                {
                    if (progress.Status == Google.Apis.Download.DownloadStatus.Downloading)
                    {
                        // Estimate progress if we know file size, or just ping
                    }
                };
                
                await request.DownloadAsync(fileStream);
                fileStream.Close();

                // Launch installer silently
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tempFile,
                    Arguments = "/SILENT /CLOSEAPPLICATIONS",
                    UseShellExecute = true
                };
                Process.Start(psi);

                // Exit current app
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error(nameof(AutoUpdaterService), "Échec du téléchargement ou du lancement de la mise à jour.", ex);
                MessageBox.Show("Erreur lors de la mise à jour: " + ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsNewerVersion(string remoteVersion, string localVersion)
        {
            if (Version.TryParse(remoteVersion, out Version? rv) && Version.TryParse(localVersion, out Version? lv))
            {
                return rv > lv;
            }
            return false;
        }
    }
}


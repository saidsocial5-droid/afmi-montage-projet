using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AFMI_Montage_projet_2.Models;
using Google.Apis.Drive.v3;

namespace AFMI_Montage_projet_2.Services.Drive
{
    public class SyncEngineService
    {
        private readonly DriveService _driveService;
        private readonly string _workspaceId;

        public SyncEngineService(DriveService driveService, string workspaceId)
        {
            _driveService = driveService;
            _workspaceId = workspaceId;
        }

        public async Task<bool> ExportProjectsToCloudAsync()
        {
            try
            {
                var systemFolderId = await GetOrCreateFolderAsync("_system", _workspaceId);
                var syncFolderId = await GetOrCreateFolderAsync("sync", systemFolderId);

                using var db = new AppDbContext();
                var projects = db.Projects.ToList();
                
                var exportList = new List<object>();
                foreach (var p in projects)
                {
                    exportList.Add(new
                    {
                        id = p.Id.ToString(),
                        title = p.Title ?? "",
                        description = p.GeneralObjective ?? "",
                        startDate = p.StartDate.HasValue ? new DateTimeOffset(p.StartDate.Value).ToUnixTimeMilliseconds() : 0,
                        endDate = p.EndDate.HasValue ? new DateTimeOffset(p.EndDate.Value).ToUnixTimeMilliseconds() : 0,
                        budgetTotal = (double)(p.TotalBudget),
                        budgetSpent = 0.0, // Calculate later if needed
                        progressPercentage = 0, // Calculate from activities
                        lastSyncAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });
                }

                var json = JsonSerializer.Serialize(exportList, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                
                await UploadOrUpdateFileAsync("projects.json", syncFolderId, json);
                
                return true;
            }
            catch (Exception ex)
            {
                global::AFMI_Montage_projet_2.Services.AppLogger.Warn(nameof(SyncEngineService), "Échec de l'export de synchronisation", ex);
                return false;
            }
        }

        private async Task<string> GetOrCreateFolderAsync(string folderName, string parentId)
        {
            var request = _driveService.Files.List();
            request.Q = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and '{parentId}' in parents and trashed = false";
            request.Spaces = "drive";
            request.Fields = "files(id)";
            var result = await request.ExecuteAsync();

            var folder = result.Files?.FirstOrDefault();
            if (folder != null) return folder.Id;

            var newFolder = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder",
                Parents = new List<string> { parentId }
            };

            var createRequest = _driveService.Files.Create(newFolder);
            createRequest.Fields = "id";
            var createdFolder = await createRequest.ExecuteAsync();
            return createdFolder.Id;
        }

        private async Task UploadOrUpdateFileAsync(string fileName, string parentId, string content)
        {
            var request = _driveService.Files.List();
            request.Q = $"name = '{fileName}' and '{parentId}' in parents and trashed = false";
            request.Spaces = "drive";
            request.Fields = "files(id)";
            var result = await request.ExecuteAsync();

            var existingFile = result.Files?.FirstOrDefault();
            
            var byteArray = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(byteArray);

            if (existingFile != null)
            {
                var updateRequest = _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFile.Id, stream, "application/json");
                await updateRequest.UploadAsync();
            }
            else
            {
                var newFile = new Google.Apis.Drive.v3.Data.File
                {
                    Name = fileName,
                    Parents = new List<string> { parentId }
                };
                var createRequest = _driveService.Files.Create(newFile, stream, "application/json");
                await createRequest.UploadAsync();
            }
        }

                public async Task<bool> DownloadProjectsFromCloudAsync()
        {
            try
            {
                var systemFolderId = await GetOrCreateFolderAsync("_system", _workspaceId);
                var syncFolderId = await GetOrCreateFolderAsync("sync", systemFolderId);

                var request = _driveService.Files.List();
                request.Q = $"name = 'projects.json' and '{syncFolderId}' in parents and trashed = false";
                request.Spaces = "drive";
                request.Fields = "files(id)";
                var result = await request.ExecuteAsync();

                var file = result.Files?.FirstOrDefault();
                if (file == null) return false;

                using var stream = new MemoryStream();
                await _driveService.Files.Get(file.Id).DownloadAsync(stream);
                stream.Position = 0;
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                // Parcourir le JSON et mettre à jour la BDD locale
                using var doc = JsonDocument.Parse(json);
                using var db = new AppDbContext();
                
                // Pour simplifier ce mode client, on va vider les projets locaux et réimporter.
                // Attention: Dans un vrai système robuste, il faudrait synchroniser ID par ID.
                var existingProjects = db.Projects.ToList();
                db.Projects.RemoveRange(existingProjects);
                await db.SaveChangesAsync();

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var p = new Project
                    {
                        Id = int.Parse(element.GetProperty("id").GetString() ?? "0"),
                        Title = element.GetProperty("title").GetString() ?? string.Empty,
                        GeneralObjective = element.GetProperty("description").GetString() ?? string.Empty,
                        TotalBudget = (decimal)element.GetProperty("budgetTotal").GetDouble(),
                    };
                    
                    long sDate = element.GetProperty("startDate").GetInt64();
                    if (sDate > 0) p.StartDate = DateTimeOffset.FromUnixTimeMilliseconds(sDate).DateTime;
                    
                    long eDate = element.GetProperty("endDate").GetInt64();
                    if (eDate > 0) p.EndDate = DateTimeOffset.FromUnixTimeMilliseconds(eDate).DateTime;

                    db.Projects.Add(p);
                }
                
                await db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                global::AFMI_Montage_projet_2.Services.AppLogger.Warn(nameof(SyncEngineService), "Échec du téléchargement de synchronisation", ex);
                return false;
            }
        }

        public async Task<int> ImportContributionsFromCloudAsync()
        {
            try
            {
                var systemFolderId = await GetOrCreateFolderAsync("_system", _workspaceId);
                var syncFolderId = await GetOrCreateFolderAsync("sync", systemFolderId);
                var contributionsFolderId = await GetOrCreateFolderAsync("contributions", syncFolderId);

                // Fetch all JSON files in the contributions folder
                var request = _driveService.Files.List();
                request.Q = $"'{contributionsFolderId}' in parents and mimeType = 'application/json' and trashed = false";
                request.Spaces = "drive";
                request.Fields = "files(id, name)";
                var result = await request.ExecuteAsync();

                if (result.Files == null || !result.Files.Any())
                    return 0; // No new contributions

                using var db = new AppDbContext();
                int importedCount = 0;

                foreach (var file in result.Files)
                {
                    // 1. Download content
                    var stream = new MemoryStream();
                    await _driveService.Files.Get(file.Id).DownloadAsync(stream);
                    stream.Position = 0;
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();

                    // 2. Parse (Simulated contribution format)
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        var actIdStr = root.GetProperty("activityId").GetString();
                        // For this basic setup, assume we find the activity. 
                        // In reality, we'd map it to the proper table. For now, we'll log it or create a pending record.
                        
                        // We will insert an audit log or a dedicated Contribution record if we had one.
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Contribution Reçue", $"Depuis mobile: {file.Name}");

                        // 3. Delete the file after consuming it
                        await _driveService.Files.Delete(file.Id).ExecuteAsync();
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        global::AFMI_Montage_projet_2.Services.AppLogger.Warn(nameof(SyncEngineService), $"Contribution illisible : {file.Name}", ex);
                    }
                }

                return importedCount;
            }
            catch (Exception ex)
            {
                global::AFMI_Montage_projet_2.Services.AppLogger.Warn(nameof(SyncEngineService), "Échec de l'import de synchronisation", ex);
                return -1;
            }
        }
    }
}





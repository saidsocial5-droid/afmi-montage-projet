using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;

namespace AFMI_Montage_projet_2.Services.Drive
{
    public class GoogleDriveManager
    {
        private readonly DriveService _service;

        public GoogleDriveManager(DriveService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        // 1. Trouver ou Créer un dossier
        public async Task<string> GetOrCreateFolderAsync(string folderName, string? parentId = null)
        {
            var query = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            if (!string.IsNullOrEmpty(parentId))
            {
                query += $" and '{parentId}' in parents";
            }

            var request = _service.Files.List();
            request.Q = query;
            request.Spaces = "drive";
            request.Fields = "files(id, name)";

            var result = await request.ExecuteAsync();
            var existingFolder = result.Files?.FirstOrDefault();

            if (existingFolder != null)
            {
                return existingFolder.Id;
            }

            // Création
            var fileMetadata = new File()
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            
            if (!string.IsNullOrEmpty(parentId))
            {
                fileMetadata.Parents = new List<string> { parentId };
            }

            var createRequest = _service.Files.Create(fileMetadata);
            createRequest.Fields = "id";
            var folder = await createRequest.ExecuteAsync();
            return folder.Id;
        }

        // 2. Initialiser l'espace de travail (Admin)
        public async Task<string> InitializeWorkspaceAsync()
        {
            // Root
            var rootId = await GetOrCreateFolderAsync("AssociationPM_Workspace");
            
            // Subfolders
            await GetOrCreateFolderAsync("official", rootId);
            await GetOrCreateFolderAsync("contributions", rootId);

            return rootId;
        }

        // 3. Inviter un utilisateur (Donner les droits)
        public async Task InviteUserAsync(string fileOrFolderId, string emailAddress, string role = "writer")
        {
            var permission = new Permission()
            {
                Type = "user",
                Role = role, // "reader" ou "writer"
                EmailAddress = emailAddress
            };

            var request = _service.Permissions.Create(permission, fileOrFolderId);
            request.SendNotificationEmail = true; // Notifie l'utilisateur
            await request.ExecuteAsync();
        }
    }
}

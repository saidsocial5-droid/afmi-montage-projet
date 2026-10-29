using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Apis.Drive.v3;
using AFMI_Montage_projet_2.Models;

namespace AFMI_Montage_projet_2.Services.Drive
{
    public class PairingSessionPayload
    {
        public string Version { get; set; } = "v1";
        public string WorkspaceId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string QrId { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = "pending";
        public string RequestingDeviceName { get; set; } = string.Empty;
        public string RequestingPublicKey { get; set; } = string.Empty;
    }

    public class DevicePairingService
    {
        private readonly DriveService _driveService;
        private readonly GoogleDriveManager _driveManager;

        public DevicePairingService(DriveService driveService)
        {
            _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
            _driveManager = new GoogleDriveManager(driveService);
        }

        public async Task<string> GetPairingFolderIdAsync(string workspaceId)
        {
            string systemFolderId = await _driveManager.GetOrCreateFolderAsync("_system", workspaceId);
            return await _driveManager.GetOrCreateFolderAsync("pairing", systemFolderId);
        }

        public string GenerateNonce(int length = 32)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }

        public async Task<PairingSessionPayload> CreatePairingSessionAsync(string workspaceId, string userEmail)
        {
            var folderId = await GetPairingFolderIdAsync(workspaceId);
            var payload = new PairingSessionPayload
            {
                WorkspaceId = workspaceId,
                UserEmail = userEmail,
                QrId = Guid.NewGuid().ToString(),
                Nonce = GenerateNonce(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(120),
                Status = "pending"
            };

            var json = JsonSerializer.Serialize(payload);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = $"{payload.QrId}.json",
                Parents = new System.Collections.Generic.List<string> { folderId }
            };

            var request = _driveService.Files.Create(fileMetadata, stream, "application/json");
            await request.UploadAsync();

            return payload;
        }

        public async Task<PairingSessionPayload?> CheckPairingSessionAsync(string workspaceId, string qrId)
        {
            var folderId = await GetPairingFolderIdAsync(workspaceId);
            var query = $"name = '{qrId}.json' and '{folderId}' in parents and trashed = false";
            var request = _driveService.Files.List();
            request.Q = query;
            var result = await request.ExecuteAsync();
            var file = result.Files?.Count > 0 ? result.Files[0] : null;

            if (file == null) return null;

            var getRequest = _driveService.Files.Get(file.Id);
            using var stream = new MemoryStream();
            await getRequest.DownloadAsync(stream);
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<PairingSessionPayload>(json);
        }

        public async Task DeletePairingSessionAsync(string workspaceId, string qrId)
        {
            var folderId = await GetPairingFolderIdAsync(workspaceId);
            var query = $"name = '{qrId}.json' and '{folderId}' in parents and trashed = false";
            var request = _driveService.Files.List();
            request.Q = query;
            var result = await request.ExecuteAsync();
            var file = result.Files?.Count > 0 ? result.Files[0] : null;

            if (file != null)
            {
                await _driveService.Files.Delete(file.Id).ExecuteAsync();
            }
        }
    }
}

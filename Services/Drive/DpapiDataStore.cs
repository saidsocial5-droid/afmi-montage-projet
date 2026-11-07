using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using Newtonsoft.Json;

namespace AFMI_Montage_projet_2.Services.Drive
{
    public class DpapiDataStore : IDataStore
    {
        private readonly string _folderPath;

        public DpapiDataStore()
        {
            _folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AFMI_Montage_Projet", "GoogleAuth");
            if (!Directory.Exists(_folderPath))
            {
                Directory.CreateDirectory(_folderPath);
            }

            // الهجرة الآمنة للتوكنات القديمة من Roaming AppData إلى LocalAppData إن وجدت
            try
            {
                string oldRoamingFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AFMI_Montage_Projet", "GoogleAuth");
                if (Directory.Exists(oldRoamingFolder) && oldRoamingFolder != _folderPath)
                {
                    foreach (var oldFile in Directory.GetFiles(oldRoamingFolder))
                    {
                        string destFile = Path.Combine(_folderPath, Path.GetFileName(oldFile));
                        if (!File.Exists(destFile))
                        {
                            File.Copy(oldFile, destFile, true);
                        }
                    }
                }
            }
            catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(DpapiDataStore), $"Erreur ignorée : {swallowedEx.Message}"); }
        }

        /// <summary>Vrai si un jeton OAuth est mémorisé pour cette clé (la reconnexion ne demandera pas le navigateur).</summary>
        public bool HasStoredToken(string key = "user") => File.Exists(Path.Combine(_folderPath, key));

        public Task ClearAsync()
        {
            if (Directory.Exists(_folderPath))
            {
                var files = Directory.GetFiles(_folderPath);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync<T>(string key)
        {
            var file = Path.Combine(_folderPath, key);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            var file = Path.Combine(_folderPath, key);
            if (File.Exists(file))
            {
                try
                {
                    var encryptedBytes = File.ReadAllBytes(file);
                    var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                    var json = Encoding.UTF8.GetString(decryptedBytes);
                    return Task.FromResult(JsonConvert.DeserializeObject<T>(json));
                }
                catch
                {
                    return Task.FromResult<T?>(default);
                }
            }
            return Task.FromResult<T?>(default);
        }

        public Task StoreAsync<T>(string key, T value)
        {
            var file = Path.Combine(_folderPath, key);
            var json = JsonConvert.SerializeObject(value);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(file, encryptedBytes);
            return Task.CompletedTask;
        }
    }
}

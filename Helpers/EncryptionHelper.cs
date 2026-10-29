using System;
using System.Security.Cryptography;
using System.Text;

namespace AFMI_Montage_projet_2.Helpers
{
    public static class EncryptionHelper
    {
        // Un sel additionnel pour le chiffrement DPAPI
        private static readonly byte[] Entropy = { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 };

        public static string Protect(string clearText)
        {
            if (string.IsNullOrEmpty(clearText)) return string.Empty;

            try
            {
                byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
                byte[] encryptedBytes = ProtectedData.Protect(clearBytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception)
            {
                // Fallback de sécurité (base64 simple) si DPAPI n'est pas disponible
                byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
                return "FALLBACK:" + Convert.ToBase64String(clearBytes);
            }
        }

        public static string Unprotect(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;

            try
            {
                if (encryptedText.StartsWith("FALLBACK:"))
                {
                    string base64 = encryptedText.Substring("FALLBACK:".Length);
                    byte[] fallbackBytes = Convert.FromBase64String(base64);
                    return Encoding.UTF8.GetString(fallbackBytes);
                }

                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] clearBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clearBytes);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;

namespace AFMI_Montage_projet_2.Security
{
    /// <summary>
    /// Hachage des mots de passe des comptes locaux (ADR 35) : PBKDF2-HMAC-SHA512 avec sel aléatoire,
    /// uniquement avec System.Security.Cryptography (aucune dépendance ajoutée).
    /// Format stocké dans User.PasswordHash : « PBKDF2$SHA512$itérations$sel(base64)$empreinte(base64) ».
    /// Les anciens comptes (mot de passe en clair) restent valides : Verify les reconnaît et signale
    /// qu'il faut les ré-hacher, ce que fait la connexion.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2";
        private const string Algorithm = "SHA512";
        public const int DefaultIterations = 210_000;
        private const int SaltSize = 16;
        private const int HashSize = 64;

        /// <summary>Renvoie l'empreinte à enregistrer pour ce mot de passe.</summary>
        public static string Hash(string password)
        {
            ArgumentNullException.ThrowIfNull(password);
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Derive(password, salt, DefaultIterations, HashSize);
            return string.Join('$', Prefix, Algorithm, DefaultIterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        /// <summary>Vrai si la valeur stockée est déjà une empreinte (et non un ancien mot de passe en clair).</summary>
        public static bool IsHashed(string? stored) =>
            !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix + "$", StringComparison.Ordinal);

        /// <summary>
        /// Vérifie un mot de passe saisi. needsRehash est vrai quand la valeur stockée est un ancien mot de passe
        /// en clair ou une empreinte plus faible que les paramètres actuels : l'appelant doit alors la remplacer par Hash(password).
        /// </summary>
        public static bool Verify(string? stored, string? password, out bool needsRehash)
        {
            needsRehash = false;
            if (string.IsNullOrEmpty(stored) || password == null) return false;

            if (!IsHashed(stored))
            {
                // Ancien compte : mot de passe enregistré en clair, comparé en temps constant.
                bool legacyMatch = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(password));
                needsRehash = legacyMatch;
                return legacyMatch;
            }

            string[] parts = stored.Split('$');
            if (parts.Length != 5 || parts[1] != Algorithm ||
                !int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int iterations) ||
                iterations <= 0)
            {
                return false;
            }

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expected = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return false;
            }
            if (expected.Length == 0) return false;

            byte[] actual = Derive(password, salt, iterations, expected.Length);
            bool match = CryptographicOperations.FixedTimeEquals(actual, expected);
            needsRehash = match && (iterations < DefaultIterations || expected.Length < HashSize);
            return match;
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, length);
    }
}

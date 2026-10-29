using System;
using System.Text.Json.Serialization;

namespace AFMI_Montage_projet_2.Models.Radar
{
    /// <summary>
    /// إعدادات خلاصة RSS/Atom المؤسسية أو المضافة من قِبل المستخدم.
    /// تُحفظ في ملف الإعدادات rss_feeds.json.
    /// </summary>
    public class RssFeedConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Category { get; set; } = "Environnement & Climat";
        public string Topic { get; set; } = "Subventions & Appels à projets";
        public bool Enabled { get; set; } = true;
        public bool IsDefault { get; set; } = false;
        public bool Verified { get; set; } = false;
        public string Language { get; set; } = "fr";
        public DateTime? LastFetchedAt { get; set; }
        public string LastStatus { get; set; } = "Non interrogé";
        public int LastItemCount { get; set; } = 0;

        // Santé du flux : les échecs répétés suspendent temporairement l'interrogation
        public DateTime? LastSuccessAt { get; set; }
        public int ConsecutiveFailures { get; set; }
        public string? LastError { get; set; }

        // Requêtes conditionnelles (HTTP 304) conservées d'une session à l'autre
        public string? ETag { get; set; }
        public DateTimeOffset? LastModified { get; set; }

        /// <summary>Version de la liste par défaut ayant produit ce flux (mise à jour des adresses obsolètes).</summary>
        public int DefaultsRevision { get; set; }

        [JsonIgnore]
        public string HealthLabel => !Enabled ? "Désactivé"
            : ConsecutiveFailures >= 3 ? $"⚠ {ConsecutiveFailures} échecs consécutifs"
            : LastSuccessAt.HasValue ? $"✔ OK ({LastSuccessAt.Value.ToLocalTime():dd/MM HH:mm})"
            : "Non interrogé";
    }
}

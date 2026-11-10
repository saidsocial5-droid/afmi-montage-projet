using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFMI_Montage_projet_2.Models.Radar
{
    /// <summary>
    /// Statuts d'un appel dans la base de veille (cycle : nouveau → en cours → soumis → clos / écarté / trop tard).
    /// « En rédaction » (formulation par l'IA) et les anciens statuts restent reconnus.
    /// </summary>
    public static class VeilleStatus
    {
        public const string Nouveau = "Nouveau";
        public const string EnCours = "En cours";
        public const string EnRedaction = "En rédaction";
        public const string Soumis = "Soumis";
        public const string Clos = "Clos";
        public const string Ecarte = "Écarté";
        public const string TropTard = "Trop tard";

        /// <summary>Statut des bases créées avant la veille (1.9.21 et antérieures).</summary>
        public const string LegacyMiseSousVeille = "Mise sous veille";
        public const string LegacyEnCoursEtude = "En cours d'étude";

        public static readonly string[] All = { Nouveau, EnCours, EnRedaction, Soumis, Clos, Ecarte, TropTard };

        /// <summary>Appel encore suivi (ni clos, ni écarté, ni trop tard).</summary>
        public static bool IsActive(string? status) =>
            status is not (Clos or Ecarte or TropTard);

        /// <summary>L'AFMI a commencé à préparer ou a déposé le dossier : la règle « trop tard » ne s'applique plus.</summary>
        public static bool IsEngaged(string? status) =>
            status is EnCours or EnRedaction or Soumis;
    }

    /// <summary>Statut de confirmation : un agrégateur repère, la page officielle valide.</summary>
    public static class VeilleConfirmation
    {
        public const string Confirme = "confirmé";
        public const string NonConfirme = "non confirmé (agrégateur)";
        public const string Annonce = "annoncé";

        public static readonly string[] All = { Confirme, NonConfirme, Annonce };
    }

    /// <summary>
    /// Appel annoncé mais pas encore ouvert (onglet A_VENIR de la base de veille).
    /// Quand la page de l'appel s'ouvre, il bascule vers le pipeline avec le statut « Nouveau ».
    /// </summary>
    [Table("veille_a_venir")]
    public class UpcomingCall : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>Identifiant AV-NNN.</summary>
        [MaxLength(20)]
        public string VeilleId { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Donor { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Programme { get; set; } = string.Empty;

        /// <summary>Texte libre (« mars 2027 », « T1 2027 ») : une date d'ouverture annoncée est rarement précise.</summary>
        [MaxLength(100)]
        public string ExpectedOpening { get; set; } = string.Empty;

        public DateTime? ExpectedDeadline { get; set; }

        [MaxLength(200)]
        public string Amount { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Link { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Appel du pipeline créé à l'ouverture (null tant que l'appel n'est pas ouvert).</summary>
        public int? PromotedGrantId { get; set; }
    }

    /// <summary>
    /// Appel vu et non retenu (onglet VUS_NON_RETENUS) : évite de revérifier chaque jour les mêmes appels.
    /// Le motif est obligatoire.
    /// </summary>
    [Table("veille_non_retenus")]
    public class RejectedCall : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public DateTime SeenAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Donor { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Link { get; set; } = string.Empty;

        public DateTime? Deadline { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}

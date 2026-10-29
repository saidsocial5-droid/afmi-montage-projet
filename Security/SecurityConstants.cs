using System;
using System.Linq;

namespace AFMI_Montage_projet_2.Security
{
    public static class SecurityConstants
    {
        /// <summary>
        /// Compte Google officiel de l'association : propriétaire de l'espace d'équipe sur Google Drive
        /// (dossier AssociationPM_Workspace), où sont stockés la version officielle des projets et les propositions.
        /// Le poste connecté avec ce compte est le « poste officiel » : il applique les décisions et publie.
        /// </summary>
        public const string WorkspaceOwnerEmail = "projet.afmi@gmail.com";

        /// <summary>
        /// Administrateurs : seuls habilités à gérer l'équipe et à approuver ou rejeter les propositions (ADR 23).
        /// L'identité est prouvée par OAuth Google, jamais par une saisie libre.
        /// </summary>
        public static readonly string[] SuperAdminEmails =
        {
            WorkspaceOwnerEmail,
            "said.afmi@gmail.com"
        };

        public static bool IsSuperAdmin(string? email) =>
            !string.IsNullOrWhiteSpace(email) &&
            SuperAdminEmails.Any(a => string.Equals(a, email.Trim(), StringComparison.OrdinalIgnoreCase));

        public static bool IsWorkspaceOwner(string? email) =>
            !string.IsNullOrWhiteSpace(email) &&
            string.Equals(WorkspaceOwnerEmail, email.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

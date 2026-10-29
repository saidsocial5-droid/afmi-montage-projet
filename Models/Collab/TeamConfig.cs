using System;
using System.Collections.Generic;
using System.Linq;
using AFMI_Montage_projet_2.Security;

namespace AFMI_Montage_projet_2.Models.Collab
{
    public static class TeamRoles
    {
        public const string Admin = "Admin";
        public const string Member = "Member";
        public const string Reviewer = "Reviewer";

        /// <summary>Compte ayant accès au dossier Drive mais absent de team.json : lecture seule.</summary>
        public const string Guest = "Guest";

        public static bool IsValid(string? role) =>
            role is Admin or Member or Reviewer or Guest;

        public static string Label(string? role) => role switch
        {
            Admin => "Administrateur",
            Member => "Membre (contributeur)",
            Reviewer => "Relecteur (lecture seule)",
            _ => "Invité (lecture seule)"
        };
    }

    public static class QuorumRules
    {
        public const string Unanimous = "Unanimous";
        public const string Majority = "Majority";
        public const string Threshold = "Threshold";

        /// <summary>Décision de l'administrateur seul : pas de vote, un administrateur approuve ou rejette (ADR 23).</summary>
        public const string AdminDecision = "AdminDecision";
    }

    public class TeamMemberDto
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = TeamRoles.Member;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public string RoleLabel => TeamRoles.Label(Role);
    }

    /// <summary>
    /// يمثل ملف team.json المخزن في: AssociationPM_Workspace/_system/collab/team.json
    /// المدير (Admin) فقط هو المخول بتعديله ورفعه.
    /// Les administrateurs principaux (SecurityConstants.SuperAdminEmails) sont toujours administrateurs,
    /// même s'ils sont absents du fichier.
    /// </summary>
    public class TeamConfig
    {
        public string TeamId { get; set; } = "afmi-workspace-default";
        public string TeamName { get; set; } = "AFMI Workspace";

        /// <summary>
        /// قاعدة النصاب: AdminDecision (قرار المدير) | Unanimous (إجماع) | Majority (أغلبية مطلقة) | Threshold (عتبة محددة n)
        /// </summary>
        public string QuorumRule { get; set; } = QuorumRules.Unanimous;

        /// <summary>
        /// عدد الأصوات المطلوب عند تفعيل قاعدة Threshold
        /// </summary>
        public int QuorumThreshold { get; set; } = 1;

        /// <summary>
        /// قائمة أعضاء الفريق وهوياتهم وأدوارهم
        /// </summary>
        public List<TeamMemberDto> Members { get; set; } = new();

        /// <summary>
        /// تاريخ آخر تعديل في التكوين
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// البريد الإلكتروني للمدير الذي قام بآخر تحديث
        /// </summary>
        public string UpdatedByEmail { get; set; } = string.Empty;

        public bool IsAdminDecisionMode => QuorumRule == QuorumRules.AdminDecision;

        public TeamMemberDto? FindMember(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            return Members.FirstOrDefault(m => string.Equals(m.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Rôle effectif d'un compte : administrateur principal, rôle déclaré dans team.json, ou invité.</summary>
        public string RoleOf(string? email)
        {
            if (SecurityConstants.IsSuperAdmin(email)) return TeamRoles.Admin;
            var member = FindMember(email);
            return member != null && TeamRoles.IsValid(member.Role) ? member.Role : TeamRoles.Guest;
        }

        public bool IsAdmin(string? email) => RoleOf(email) == TeamRoles.Admin;

        public bool CanPropose(string? email)
        {
            // Reviewer et Guest ne peuvent pas proposer, Admin et Member seulement
            var role = RoleOf(email);
            return role == TeamRoles.Admin || role == TeamRoles.Member;
        }

        public bool CanVote(string? email)
        {
            // جميع الأعضاء المعتمدين في team.json يحق لهم التصويت
            return SecurityConstants.IsSuperAdmin(email) || FindMember(email) != null;
        }

        /// <summary>Peut approuver ou rejeter une proposition (mode décision de l'administrateur).</summary>
        public bool CanDecide(string? email) => IsAdmin(email);
    }
}

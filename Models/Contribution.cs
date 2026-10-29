using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public static class ContributionStatuses
    {
        public const string Draft = "Draft";
        public const string Proposed = "Proposed";

        /// <summary>Approuvée par un administrateur, en attente d'intégration par le poste officiel.</summary>
        public const string Accepted = "Accepted";

        /// <summary>Intégrée dans la version officielle (ou suggestion prise en compte).</summary>
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string NeedsReview = "NeedsReview";

        public static bool IsOpen(string? status) => status is Proposed or NeedsReview or Accepted;

        public static string Label(string? status) => status switch
        {
            Proposed => "En attente",
            Accepted => "Approuvée (intégration en cours)",
            Approved => "Intégrée",
            Rejected => "Rejetée",
            NeedsReview => "À revoir",
            _ => status ?? string.Empty
        };
    }

    /// <summary>
    /// Valeurs spéciales de <see cref="Contribution.FieldName"/> pour les propositions qui ne modifient pas un champ.
    /// </summary>
    public static class ProposalFields
    {
        /// <summary>Création d'un nouvel élément : ProposedValue contient le JSON de ses colonnes (ADR 23).</summary>
        public const string Create = "__Create__";

        /// <summary>Suggestion libre (texte) sur un projet : aucune donnée n'est modifiée.</summary>
        public const string Note = "__Note__";
    }

    /// <summary>
    /// يمثل مساهمة نصية فردية مقترحة من عضو في الفريق
    /// تُحفظ في ملف مستقل على Google Drive: collab/proposals/{Id}.json
    /// </summary>
    public class Contribution
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// نوع الكيان المرتبط (Project, Objective, Result, Activity, BudgetItem, إلخ)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// المعرف الفريد للكيان (GlobalId أو المعرف النصي)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EntityId { get; set; } = string.Empty;

        /// <summary>
        /// اسم الحقل النصي المقترح تعديله (مثل Title, Description, GeneralObjective)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// القيمة الأصلية للحقل عند بداية التعديل
        /// </summary>
        public string? BaseValue { get; set; }

        /// <summary>
        /// القيمة المقترحة الجديدة من الكاتب
        /// </summary>
        public string? ProposedValue { get; set; }

        /// <summary>
        /// البريد الإلكتروني للكاتب (المستخرج من ID Token الموثوق)
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string AuthorEmail { get; set; } = string.Empty;

        /// <summary>
        /// اسم الكاتب الصريح
        /// </summary>
        [Required]
        [MaxLength(150)]
        public string AuthorName { get; set; } = string.Empty;

        /// <summary>
        /// تاريخ إنشاء المقترح
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// رقم إصدار الكيان الأصلي وقت صياغة المقترح (للتحكم المتفائل في التزامن)
        /// </summary>
        public int BaseVersion { get; set; } = 1;

        /// <summary>
        /// حالة المساهمة: Draft | Proposed | Approved | Rejected | NeedsReview
        /// </summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = ContributionStatuses.Proposed;

        /// <summary>
        /// معرف الملف على Google Drive: proposals/{guid}.json
        /// </summary>
        [MaxLength(150)]
        public string? DriveFileId { get; set; }

        /// <summary>
        /// تاريخ اعتماد أو مراجعة المساهمة
        /// </summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>
        /// بريد من أجرى الاعتماد أو الدمج
        /// </summary>
        [MaxLength(150)]
        public string? ReviewedByEmail { get; set; }

        /// <summary>
        /// ملاحظات الدمج أو سبب الرفض أو وصف التعارض في الإصدار
        /// </summary>
        public string? ReviewNotes { get; set; }

        /// <summary>
        /// Libellé lisible de l'élément visé (ex. « Activité : Formation des agriculteurs »), pour l'écran de validation.
        /// </summary>
        [MaxLength(300)]
        public string? EntityLabel { get; set; }

        /// <summary>GlobalId du projet concerné (filtrage par projet).</summary>
        [MaxLength(100)]
        public string? ProjectGlobalId { get; set; }

        /// <summary>
        /// قائمة أصوات أعضاء الفريق على هذا المقترح
        /// </summary>
        public List<Vote> Votes { get; set; } = new();
    }
}

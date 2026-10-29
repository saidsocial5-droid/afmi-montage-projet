using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Collab;

namespace AFMI_Montage_projet_2.Services.Collab
{
    /// <summary>Correspondance entre la proposition locale (<see cref="Contribution"/>) et son fichier Drive (<see cref="ProposalDto"/>).</summary>
    public static class ProposalMapper
    {
        public static ProposalDto ToDto(Contribution c) => new()
        {
            Id = c.Id,
            EntityType = c.EntityType,
            EntityId = c.EntityId,
            FieldName = c.FieldName,
            BaseValue = c.BaseValue,
            ProposedValue = c.ProposedValue,
            AuthorEmail = c.AuthorEmail,
            AuthorName = c.AuthorName,
            CreatedAt = c.CreatedAt,
            BaseVersion = c.BaseVersion,
            Status = c.Status,
            DriveFileId = c.DriveFileId,
            ReviewedByEmail = c.ReviewedByEmail,
            ReviewedAt = c.ReviewedAt,
            ReviewNotes = c.ReviewNotes,
            EntityLabel = c.EntityLabel,
            ProjectGlobalId = c.ProjectGlobalId
        };

        public static Contribution ToEntity(ProposalDto d) => new()
        {
            Id = d.Id,
            EntityType = d.EntityType,
            EntityId = d.EntityId,
            FieldName = d.FieldName,
            BaseValue = d.BaseValue,
            ProposedValue = d.ProposedValue,
            AuthorEmail = d.AuthorEmail,
            AuthorName = d.AuthorName,
            CreatedAt = d.CreatedAt,
            BaseVersion = d.BaseVersion,
            Status = d.Status,
            DriveFileId = d.DriveFileId,
            ReviewedByEmail = d.ReviewedByEmail,
            ReviewedAt = d.ReviewedAt,
            ReviewNotes = d.ReviewNotes,
            EntityLabel = d.EntityLabel,
            ProjectGlobalId = d.ProjectGlobalId
        };

        /// <summary>Recopie la décision et l'état d'une proposition distante dans la proposition locale.</summary>
        public static bool ApplyReview(Contribution local, Contribution d)
        {
            bool changed = local.Status != d.Status
                           || local.ReviewedByEmail != d.ReviewedByEmail
                           || local.ReviewNotes != d.ReviewNotes;
            local.Status = d.Status;
            local.ReviewedByEmail = d.ReviewedByEmail;
            local.ReviewedAt = d.ReviewedAt;
            local.ReviewNotes = d.ReviewNotes;
            local.EntityLabel ??= d.EntityLabel;
            local.ProjectGlobalId ??= d.ProjectGlobalId;
            if (string.IsNullOrEmpty(local.DriveFileId) && !string.IsNullOrEmpty(d.DriveFileId))
            {
                local.DriveFileId = d.DriveFileId;
                changed = true;
            }
            return changed;
        }
    }
}

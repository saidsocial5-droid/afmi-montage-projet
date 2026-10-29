using System;

namespace AFMI_Montage_projet_2.Models
{
    public class PendingContribution
    {
        public string GlobalId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string AuthorColor { get; set; } = "#000000";
        public DateTime LastModifiedAt { get; set; }
        public BaseEntity EntityReference { get; set; } = null!;
    }
}

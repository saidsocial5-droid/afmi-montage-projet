using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Output : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ResultId { get; set; }
        public Result? Result { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty; // ex: P1.1.1, PROD-1

        [Required]
        public string Description { get; set; } = string.Empty;

        public string ResponsibleRole { get; set; } = string.Empty;
        public string TargetDate { get; set; } = string.Empty;

        // Relations
        public List<Activity> Activities { get; set; } = new();
    }
}




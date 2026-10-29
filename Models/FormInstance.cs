using System;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class FormInstance : BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FormTemplateId { get; set; }
        public FormTemplate? FormTemplate { get; set; }

        [Required]
        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        public string JsonAnswers { get; set; } = "{}"; // Réponses de l'utilisateur stockées au format JSON

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}





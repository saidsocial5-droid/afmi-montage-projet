using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class FormTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public string JsonSchema { get; set; } = string.Empty; // Structure de formulaire sous forme de schéma JSON

        public int Version { get; set; } = 1;

        public bool IsActive { get; set; } = true;
    }
}


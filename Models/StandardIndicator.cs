using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class StandardIndicator
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = string.Empty; // ex: Environnement, Social, Économique

        [MaxLength(20)]
        public string Unit { get; set; } = string.Empty; // ex: %, personnes, hectares
    }
}


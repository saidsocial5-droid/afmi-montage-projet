using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class TaxonomyValue
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TaxonomyId { get; set; }
        public Taxonomy? Taxonomy { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty; // ex: ODD1, AGRI, RH

        [Required]
        [MaxLength(200)]
        public string Label { get; set; } = string.Empty; // ex: Pas de pauvreté, Agriculture, Ressources Humaines

        public int? ParentId { get; set; } // Hiérarchie de référentiel (sous-catégories)
        public TaxonomyValue? Parent { get; set; }
    }
}


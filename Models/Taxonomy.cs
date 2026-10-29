using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class Taxonomy
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // ex: ODD, Domaine d'intervention, Type de risque

        public string Description { get; set; } = string.Empty;

        public List<TaxonomyValue> Values { get; set; } = new();
    }
}

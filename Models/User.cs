using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AFMI_Montage_projet_2.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsSystemAdmin { get; set; } = false;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Active, Suspended, Rejected

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Reader"; // SuperAdmin, Manager, Contributor, Reader

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relations
        public List<ProjectRole> ProjectRoles { get; set; } = new();
    }
}

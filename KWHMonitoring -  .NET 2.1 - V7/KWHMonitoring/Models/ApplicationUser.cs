using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("ApplicationUsers")]
    public class ApplicationUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string NormalizedEmail { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(500)")]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string Role { get; set; } = UserRoles.None;

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime2")]
        public DateTime? LastLoginAt { get; set; }

        public bool EmailConfirmed { get; set; }

        public bool IsActive { get; set; } = true;

        public int AccessFailedCount { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? LockoutEnd { get; set; }

        public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;

        public bool CanLogin => IsActive && EmailConfirmed && !IsLockedOut;
    }

    public static class UserRoles
    {
        public const string None = "None";
        public const string Viewer = "Viewer";
        public const string Operator = "Operator";
        public const string Admin = "Admin";
    }
}

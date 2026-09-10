using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    public enum TokenPurpose
    {
        EmailVerification,
        ApprovalRequest,
        PasswordReset
    }

    [Table("EmailVerificationTokens")]
    public class EmailVerificationToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(128)")]
        [MaxLength(128)]
        public string TokenHash { get; set; } = string.Empty;

        [Required]
        public TokenPurpose Purpose { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime ExpiresAt { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsUsed { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? UsedAt { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        public bool IsValid => !IsUsed && !IsExpired;

        [Column(TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string RequestedRole { get; set; } = string.Empty;
    }
}

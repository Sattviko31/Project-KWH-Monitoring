using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    public enum SecurityAction
    {
        Register,
        EmailVerificationSent,
        EmailVerified,
        Login,
        Logout,
        LoginFailed,
        Lockout,
        PasswordResetRequested,
        PasswordReset,
        AccessRequestSent,
        AccessApproved,
        AccessDenied,
        RoleChanged,
        SettingsViewed,
        SettingsUpdated,
        RelayOn,
        RelayOff,
        RelayPulse,
        UnauthorizedAttempt
    }

    [Table("SecurityAuditLogs")]
    public class SecurityAuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [Column(TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public SecurityAction Action { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        [MaxLength(100)]
        public string TargetDevice { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string IpAddress { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(500)")]
        public string UserAgent { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(500)")]
        public string Details { get; set; } = string.Empty;

        public bool Success { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

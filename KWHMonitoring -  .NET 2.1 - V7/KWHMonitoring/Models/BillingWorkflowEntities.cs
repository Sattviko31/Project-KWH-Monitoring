using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("BillingConfirmationRecipients")]
    public class BillingConfirmationRecipient
    {
        [Key]
        public long Id { get; set; }

        public int UserId { get; set; }

        public bool IsSelected { get; set; } = true;

        [MaxLength(256)]
        public string SelectedBy { get; set; }

        public DateTime SelectedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("BillingAnomalyNotes")]
    public class BillingAnomalyNote
    {
        [Key]
        public long Id { get; set; }

        public long AnomalyLogId { get; set; }

        public int? UserId { get; set; }

        [Required, MaxLength(256)]
        public string Author { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string NoteType { get; set; } = "CONFIRMATION";

        [Required, MaxLength(2000)]
        public string Note { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Decision { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("BillingTrendProfiles")]
    public class BillingTrendProfile
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(20)]
        public string DeviceKey { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "ACTIVE";

        public DateTime AnchorBillingPeriod { get; set; }
        public decimal AnchorAmount { get; set; }
        public decimal? ActiveBaselineAmount { get; set; }
        public int BaselineVersion { get; set; }
        public int ValidConfirmationCount { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("BillingTrendConfirmations")]
    public class BillingTrendConfirmation
    {
        [Key]
        public long Id { get; set; }

        public long TrendProfileId { get; set; }
        public long? AnomalyLogId { get; set; }

        [Required, MaxLength(20)]
        public string DeviceKey { get; set; } = string.Empty;

        public DateTime BillingPeriod { get; set; }
        public decimal ObservedAmount { get; set; }

        [Required, MaxLength(20)]
        public string Decision { get; set; } = "VALID";

        [MaxLength(1000)]
        public string Reason { get; set; }

        [MaxLength(256)]
        public string ConfirmedBy { get; set; }

        [MaxLength(30)]
        public string Channel { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("BillingConfirmationTokens")]
    public class BillingConfirmationToken
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(128)]
        public string TokenHash { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string Purpose { get; set; } = "BILLING_CONFIRMATION";

        public long TrendProfileId { get; set; }

        public long? AnomalyLogId { get; set; }

        public int UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        [MaxLength(256)]
        public string UsedBy { get; set; }
    }

    [Table("BillingBudgetHistories")]
    public class BillingBudgetHistory
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(20)]
        public string DeviceKey { get; set; } = string.Empty;

        public decimal? PreviousAmount { get; set; }
        public decimal Amount { get; set; }

        [Required, MaxLength(20)]
        public string Source { get; set; } = "FALLBACK";

        public long? TrendProfileId { get; set; }
        public int? BaselineVersion { get; set; }

        [Required, MaxLength(100)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [MaxLength(256)]
        public string ChangedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("BillingNotificationOutbox")]
    public class BillingNotificationOutbox
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string EventKey { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string Channel { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        public string NotificationType { get; set; } = string.Empty;

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "PENDING";

        public int AttemptCount { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }

        [MaxLength(1000)]
        public string LastError { get; set; }

        [MaxLength(200)]
        public string ProviderMessageId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
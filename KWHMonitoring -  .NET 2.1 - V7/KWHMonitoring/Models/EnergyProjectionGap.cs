using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    /// <summary>
    /// Audit record for an MQTT/telemetry interval that has no actual readings.
    /// This table never replaces KWHData or the actual energy aggregate tables.
    /// </summary>
    [Table("EnergyProjectionGaps")]
    public class EnergyProjectionGap
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string DeviceKey { get; set; } = string.Empty;

        public DateTime GapStartUtc { get; set; }
        public DateTime? GapEndUtc { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "OPEN";

        [MaxLength(40)]
        public string DetectionSource { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
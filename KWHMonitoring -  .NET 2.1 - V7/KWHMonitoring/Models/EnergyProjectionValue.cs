using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    /// <summary>
    /// Separately stored projected hourly energy. Actual HourlyEnergy rows remain authoritative.
    /// </summary>
    [Table("EnergyProjectionValues")]
    public class EnergyProjectionValue
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string DeviceKey { get; set; } = string.Empty;

        public DateTime BucketStartUtc { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ProjectedKWh { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? LowerBoundKWh { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? UpperBoundKWh { get; set; }

        [Column(TypeName = "decimal(9,4)")]
        public decimal? ConfidenceScore { get; set; }

        [MaxLength(20)]
        public string ConfidenceLevel { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "PROJECTED";

        [Required]
        [MaxLength(80)]
        public string ProjectionMethod { get; set; } = "SAME_DEVICE_SAME_HOUR_WEIGHTED_MEDIAN";

        public int SampleCount { get; set; }
        public long? GapId { get; set; }
        public DateTime? SourcePeriodStartUtc { get; set; }
        public DateTime? SourcePeriodEndUtc { get; set; }

        [Required]
        [MaxLength(30)]
        public string CalculationVersion { get; set; } = "v1";

        public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReconciledAtUtc { get; set; }
    }
}
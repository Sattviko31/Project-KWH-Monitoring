using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("AnomalyLogs")]
    public class AnomalyLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("DeviceKey", TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string DeviceKey { get; set; } = string.Empty;

        [Column("DeviceId", TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string DeviceId { get; set; }

        [Required]
        [Column("AnomalyType", TypeName = "nvarchar(20)")]
        [MaxLength(20)]
        public string AnomalyType { get; set; } = string.Empty;

        [Column("PowerValue", TypeName = "decimal(18,2)")]
        public decimal PowerValue { get; set; }

        [Column("ThresholdValue", TypeName = "decimal(18,2)")]
        public decimal ThresholdValue { get; set; }

        [Column("Deviation", TypeName = "decimal(5,2)")]
        public decimal Deviation { get; set; }

        [Column("DetectedTime", TypeName = "datetime2")]
        public DateTime DetectedTime { get; set; }

        [Column("EMAValue", TypeName = "decimal(18,2)")]
        public decimal? EMAValue { get; set; }

        [Column("ThresholdMode", TypeName = "nvarchar(20)")]
        [MaxLength(20)]
        public string ThresholdMode { get; set; } = "manual";

        [Column("Acknowledged")]
        public bool? Acknowledged { get; set; } = false;

        [Column("AcknowledgedTime", TypeName = "datetime2")]
        public DateTime? AcknowledgedTime { get; set; }

        [Column("AcknowledgedBy", TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string AcknowledgedBy { get; set; }

        [Column("ResolvedBy", TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string ResolvedBy { get; set; }

        [Column("ResolvedTime", TypeName = "datetime2")]
        public DateTime? ResolvedTime { get; set; }

        [Column("IsResolved")]
        public bool IsResolved { get; set; }

        [Column("OperatorAction", TypeName = "nvarchar(100)")]
        [MaxLength(100)]
        public string OperatorAction { get; set; }

        [Column("OperatorNotes", TypeName = "nvarchar(1000)")]
        [MaxLength(1000)]
        public string OperatorNotes { get; set; }

        [Column("Severity", TypeName = "nvarchar(20)")]
        [MaxLength(20)]
        public string Severity { get; set; } = "medium";

        [Column("RootCause", TypeName = "nvarchar(500)")]
        [MaxLength(500)]
        public string RootCause { get; set; }

        [Column("RecommendedAction", TypeName = "nvarchar(1000)")]
        [MaxLength(1000)]
        public string RecommendedAction { get; set; }

        [Column("Notes", TypeName = "nvarchar(500)")]
        [MaxLength(500)]
        public string Notes { get; set; }

        public AnomalyChartSnapshot ChartSnapshot { get; set; }
    }
}

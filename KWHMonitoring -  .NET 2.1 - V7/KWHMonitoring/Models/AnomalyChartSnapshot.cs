using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("AnomalyChartSnapshots")]
    public class AnomalyChartSnapshot
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public long AnomalyLogId { get; set; }

        [ForeignKey("AnomalyLogId")]
        public AnomalyLog AnomalyLog { get; set; }

        [Column("DetectedTime", TypeName = "datetime2")]
        public DateTime DetectedTime { get; set; }

        [Column("BeforeDataJson", TypeName = "nvarchar(max)")]
        public string BeforeDataJson { get; set; }

        [Column("AfterDataJson", TypeName = "nvarchar(max)")]
        public string AfterDataJson { get; set; }

        [Column("UpperThreshold", TypeName = "decimal(18,2)")]
        public decimal UpperThreshold { get; set; }

        [Column("LowerThreshold", TypeName = "decimal(18,2)")]
        public decimal LowerThreshold { get; set; }

        [Column("EMAValue", TypeName = "decimal(18,2)")]
        public decimal? EMAValue { get; set; }

        [Column("SnapshotStatus", TypeName = "nvarchar(20)")]
        [MaxLength(20)]
        public string SnapshotStatus { get; set; } = "before"; // before | partial | complete

        [Column("CreatedAt", TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt", TypeName = "datetime2")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class ChartDataPoint
    {
        public DateTime? Timestamp { get; set; }
        public decimal Power { get; set; }
        public decimal? Upper { get; set; }
        public decimal? Lower { get; set; }
        public decimal? EMA { get; set; }
    }
}

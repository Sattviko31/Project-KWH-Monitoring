using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("AnomalyMonthlyReports")]
    public class AnomalyMonthlyReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public int Year { get; set; }

        public int Month { get; set; }

        public int TotalAnomalies { get; set; }

        public int OverloadCount { get; set; }

        public int DropCount { get; set; }

        public int AffectedDevices { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal AverageDeviation { get; set; }

        [Column("TopAffectedDevice", TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string TopAffectedDevice { get; set; }

        [Column("SummaryText", TypeName = "nvarchar(2000)")]
        public string SummaryText { get; set; }

        [Column("Recommendations", TypeName = "nvarchar(2000)")]
        public string Recommendations { get; set; }

        [Column("GeneratedBy", TypeName = "nvarchar(256)")]
        [MaxLength(256)]
        public string GeneratedBy { get; set; }

        [Column("GeneratedAt", TypeName = "datetime2")]
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}

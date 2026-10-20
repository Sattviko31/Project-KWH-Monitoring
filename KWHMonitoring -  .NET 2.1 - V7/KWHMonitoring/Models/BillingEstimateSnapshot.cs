using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("BillingEstimateSnapshots")]
    public class BillingEstimateSnapshot
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(20)]
        public string ReportType { get; set; } = string.Empty;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        [Required, MaxLength(100)]
        public string DeviceKey { get; set; } = string.Empty;

        [MaxLength(100)]
        public string GroupName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string KodeLokasi { get; set; } = string.Empty;

        [MaxLength(100)]
        public string IDPelanggan { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal EnergyKWh { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal WbpKWh { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal LwbpKWh { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FlatTariffPerKWh { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TariffWBP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TariffLWBP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WbpCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LwbpCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Required, MaxLength(20)]
        public string TariffMode { get; set; } = "FLAT";

        [Required, MaxLength(30)]
        public string DataStatus { get; set; } = "AVAILABLE";

        public DateTime CalculatedAt { get; set; } = DateTime.Now;
        public DateTime? SentAt { get; set; }
    }
}
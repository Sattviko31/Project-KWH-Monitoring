using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("DeviceSettings")]
    public class DeviceSettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("DeviceKey", TypeName = "varchar(20)")]
        [MaxLength(20)]
        public string DeviceKey { get; set; } = string.Empty;

        [Column("MaxCapacity", TypeName = "decimal(18,2)")]
        public decimal MaxCapacity { get; set; } = 0m;

        [Column("DeviceCategory", TypeName = "nvarchar(100)")]
        [MaxLength(100)]
        public string DeviceCategory { get; set; } = "Billboard";

        [Column("DowntimeEnabled")]
        public bool DowntimeEnabled { get; set; } = false;

        [Column("DowntimeStart", TypeName = "time")]
        public TimeSpan DowntimeStart { get; set; } = TimeSpan.FromHours(22);

        [Column("DowntimeEnd", TypeName = "time")]
        public TimeSpan DowntimeEnd { get; set; } = TimeSpan.FromHours(6);

        [Column("TariffPerKWh", TypeName = "decimal(18,2)")]
        public decimal TariffPerKWh { get; set; } = 1500m;

        [Column("LoadNormalThreshold")]
        public int LoadNormalThreshold { get; set; } = 30;

        [Column("LoadMediumThreshold")]
        public int LoadMediumThreshold { get; set; } = 70;

        [Column("EmaUpperThreshold")]
        public int EmaUpperThreshold { get; set; } = 0;

        [Column("EmaLowerThreshold")]
        public int EmaLowerThreshold { get; set; } = 0;

        [Column("EmaFibUpper", TypeName = "float")]
        public double EmaFibUpper { get; set; } = 0;

        [Column("EmaFibLower", TypeName = "float")]
        public double EmaFibLower { get; set; } = 0;

        [Column("ControlMode", TypeName = "varchar(50)")]
        [MaxLength(50)]
        public string ControlMode { get; set; } = "OnOff";

        // Financial fields — WBP/LWBP tariff split
        [Column("TariffWBP", TypeName = "decimal(18,2)")]
        public decimal TariffWBP { get; set; } = 0m;

        [Column("TariffLWBP", TypeName = "decimal(18,2)")]
        public decimal TariffLWBP { get; set; } = 0m;

        [Column("WbpStartHour")]
        public int WbpStartHour { get; set; } = 18;

        [Column("WbpEndHour")]
        public int WbpEndHour { get; set; } = 22;

        // Budget & unit economics
        [Column("BudgetKWh", TypeName = "decimal(18,2)")]
        public decimal BudgetKWh { get; set; } = 0m;

        [Column("SurfaceArea", TypeName = "decimal(18,2)")]
        public decimal SurfaceArea { get; set; } = 0m;

        // Revenue model for advertising media — used to estimate revenue loss during DROP anomalies
        [Column("RevenuePerHour", TypeName = "decimal(18,2)")]
        public decimal RevenuePerHour { get; set; } = 0m;

        [Column("CreatedAt", TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt", TypeName = "datetime2")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DeviceSettingsListItem
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }
}

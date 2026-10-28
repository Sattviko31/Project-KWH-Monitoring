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

        // Kapasitas efektif untuk tampilan berasal dari WWMERP2019.
        // Properti ini hanya dipakai pada response API dan tidak disimpan di database aplikasi.
        [NotMapped]
        public decimal InstalledCapacityVA { get; set; } = 0m;

        [NotMapped]
        public decimal EffectiveMaxCapacityWatt { get; set; } = 0m;

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
        // Tarif flat per-device. Digunakan hanya bila tarif device tidak
        // tersedia/valid atau pembacaan settings gagal.
        public decimal TariffPerKWh { get; set; } = 1444.70m;

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

        [Column("InvertRelayCommand")]
        public bool InvertRelayCommand { get; set; } = false;

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

        // Null means use the global billing anomaly threshold.
        [Column("BillingAnomalyThreshold", TypeName = "decimal(18,2)")]
        public decimal? BillingAnomalyThreshold { get; set; }

        // Billing anomaly threshold can be configured as a nominal amount or percentage.
        [Column("BillingAnomalyThresholdMode", TypeName = "varchar(20)")]
        [MaxLength(20)]
        public string BillingAnomalyThresholdMode { get; set; } = "AMOUNT";

        [Column("BillingAnomalyThresholdPercent", TypeName = "decimal(9,4)")]
        public decimal? BillingAnomalyThresholdPercent { get; set; }

        // Candidate-trend tolerance can be configured as a nominal amount or percentage.
        [Column("BillingTrendToleranceMode", TypeName = "varchar(20)")]
        [MaxLength(20)]
        public string BillingTrendToleranceMode { get; set; } = "PERCENTAGE";

        [Column("BillingTrendToleranceAmount", TypeName = "decimal(18,2)")]
        public decimal? BillingTrendToleranceAmount { get; set; }

        [Column("BillingTrendTolerancePercent", TypeName = "decimal(9,4)")]
        public decimal? BillingTrendTolerancePercent { get; set; } = 10m;

        // Nominal monthly billing budget. BudgetKWh remains an energy budget and is not replaced.
        [Column("MonthlyBillingBudgetAmount", TypeName = "decimal(18,2)")]
        public decimal? MonthlyBillingBudgetAmount { get; set; }

        [Column("MonthlyBillingBudgetSource", TypeName = "varchar(20)")]
        [MaxLength(20)]
        public string MonthlyBillingBudgetSource { get; set; } = "MANUAL";

        [Column("MonthlyBillingBudgetManualOverride")]
        public bool MonthlyBillingBudgetManualOverride { get; set; } = false;

        [Column("MonthlyBillingBudgetTrendProfileId")]
        public long? MonthlyBillingBudgetTrendProfileId { get; set; }

        [Column("MonthlyBillingBudgetBaselineVersion")]
        public int? MonthlyBillingBudgetBaselineVersion { get; set; }

        [Column("MonthlyBillingBudgetUpdatedAt", TypeName = "datetime2")]
        public DateTime? MonthlyBillingBudgetUpdatedAt { get; set; }

        // Optional PLN billing cycle. Disabled/null keeps the existing calendar-month behavior.
        [Column("BillingCutoffEnabled")]
        public bool BillingCutoffEnabled { get; set; } = false;

        [Column("BillingCutoffDay")]
        public int? BillingCutoffDay { get; set; }

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

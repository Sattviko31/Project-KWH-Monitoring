using System;
using System.Collections.Generic;

namespace KWHMonitoring.Models
{
    public class PanelViewModel
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string DeviceCategory { get; set; } = "Billboard";
        public DateTime Waktu_Server { get; set; }
        public decimal Volt_R { get; set; }
        public decimal? Volt_S { get; set; }
        public decimal? Volt_T { get; set; }
        public decimal Amp_R { get; set; }
        public decimal? Amp_S { get; set; }
        public decimal? Amp_T { get; set; }
        public decimal Cos_Phi { get; set; }
        public decimal Daya_Watt { get; set; }
        public decimal TotalW1M_Wh { get; set; }
        public decimal Energi_Aktif_Wh { get; set; }
        public decimal Total_Energy_Wh { get; set; }
        public decimal Frekuensi_Hz { get; set; }

        public bool IsThreePhase => Volt_S.HasValue && Volt_T.HasValue && Amp_S.HasValue && Amp_T.HasValue;

        public string PhaseTypeLabel => IsThreePhase ? "3 Phase" : "1 Phase";
        public string PhaseTypeBadge => IsThreePhase ? "bg-purple" : "bg-orange";

        public decimal AvgVoltage => IsThreePhase
            ? (Volt_R + Volt_S.Value + Volt_T.Value) / 3
            : Volt_R;

        public decimal AvgAmpere => IsThreePhase
            ? (Amp_R + Amp_S.Value + Amp_T.Value) / 3
            : Amp_R;

        public string Status => GetStatus();
        public string StatusColor => GetStatusColor();
        public string PhaseRColor => GetPhaseColor(Volt_R, Amp_R);
        public string PhaseSColor => IsThreePhase ? GetPhaseColor(Volt_S.Value, Amp_S.Value) : "secondary";
        public string PhaseTColor => IsThreePhase ? GetPhaseColor(Volt_T.Value, Amp_T.Value) : "secondary";
        // LoadPercent dihitung di frontend berdasarkan systemSettings.maxCapacity
        public string LoadColor => GetLoadColor();
        public string CosPhiColor => GetCosPhiColor();

        private string GetStatus()
        {
            // Gunakan LoadPercent agar sinkron dengan load bar
            // LoadPercent = (Daya_Watt / 30000) * 100
            // HIGH: >70%, MEDIUM: >30%, NORMAL: ≤30%
            const decimal maxCapacity = 100000m; // Default value, should be loaded from database
            var loadPercent = Math.Min((Daya_Watt / maxCapacity) * 100, 100);
            if (loadPercent > 70) return "HIGH";
            if (loadPercent > 30) return "MEDIUM";
            return "NORMAL";
        }

        private string GetStatusColor()
        {
            const decimal maxCapacity = 100000m; // Default value, should be loaded from database
            var loadPercent = Math.Min((Daya_Watt / maxCapacity) * 100, 100);
            if (loadPercent > 70) return "danger";
            if (loadPercent > 30) return "warning";
            return "success";
        }

        private string GetPhaseColor(decimal volt, decimal amp)
        {
            if (volt < 200 || amp > 80) return "danger";
            if (volt < 220 || amp > 70) return "warning";
            return "success";
        }

        private string GetLoadColor()
        {
            const decimal maxCapacity = 100000m; // Default value, should be loaded from database
            var loadPercent = Math.Min((Daya_Watt / maxCapacity) * 100, 100);
            if (loadPercent > 80) return "danger";
            if (loadPercent > 60) return "warning";
            return "info";
        }

        private string GetCosPhiColor()
        {
            if (Cos_Phi >= 0.8m) return "success";
            if (Cos_Phi >= 0.6m) return "warning";
            return "danger";
        }
    }

    public class DashboardViewModel
    {
        public List<PanelViewModel> Panels { get; set; } = new List<PanelViewModel>();
        public AppSettings Settings { get; set; } = new AppSettings();
        public TotalStatistics TotalStats { get; set; } = new TotalStatistics();
    }

    public class TotalStatistics
    {
        public decimal TotalDaya { get; set; }
        public decimal TotalEnergy { get; set; }
        public decimal TotalW1M { get; set; }
        public decimal TotalEnergiAktif { get; set; }
        public int ActivePanels { get; set; }
        public decimal AvgPowerFactor { get; set; }
        public decimal AvgVoltage { get; set; }
        public decimal AvgFrequency { get; set; }
    }
}

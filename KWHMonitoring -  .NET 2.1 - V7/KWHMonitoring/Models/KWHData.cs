using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("KWHData")]
    public class KWHData
    {
        [Key]
        [Column("Id")]
        public long Id { get; set; }

        [Column("DeviceKey")]
        public string DeviceKey { get; set; } = string.Empty;

        [Column("DeviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [Column("GroupName")]
        public string GroupName { get; set; } = string.Empty;

        [Column("TerminalTime")]
        public DateTime Waktu_Device { get; set; }

        [Column("ReceivedTime")]
        public DateTime Waktu_Server { get; set; }

        [Column("PHASE_R")]
        public decimal Volt_R { get; set; }

        [Column("PHASE_S")]
        public decimal? Volt_S { get; set; }

        [Column("PHASE_T")]
        public decimal? Volt_T { get; set; }

        [Column("AMPERE_R")]
        public decimal Amp_R { get; set; }

        [Column("AMPERE_S")]
        public decimal? Amp_S { get; set; }

        [Column("AMPERE_T")]
        public decimal? Amp_T { get; set; }

        [Column("CosPhi")]
        public decimal Cos_Phi { get; set; }

        [Column("W")]
        public decimal Daya_Watt { get; set; }

        [Column("TotalW1M")]
        public decimal TotalW1M_Wh { get; set; }

        [Column("Aktif_Power")]
        public decimal Energi_Aktif_Wh { get; set; }

        [Column("TotalW")]
        public decimal Total_Energy_Wh { get; set; }

        [Column("F")]
        public decimal Frekuensi_Hz { get; set; }

        [NotMapped]
        public string DeviceCategory { get; set; } = "Billboard";

        [NotMapped]
        public bool IsThreePhase => Volt_S.HasValue && Volt_T.HasValue && Amp_S.HasValue && Amp_T.HasValue;

        [NotMapped]
        public decimal AvgVoltage => IsThreePhase
            ? (Volt_R + Volt_S.Value + Volt_T.Value) / 3
            : Volt_R;

        [NotMapped]
        public decimal AvgAmpere => IsThreePhase
            ? (Amp_R + Amp_S.Value + Amp_T.Value) / 3
            : Amp_R;

        [NotMapped]
        public string Status => GetStatus();

        [NotMapped]
        public string StatusColor => GetStatusColor();

        [NotMapped]
        public string PhaseRColor => GetPhaseColor(Volt_R, Amp_R);

        [NotMapped]
        public string PhaseSColor => IsThreePhase ? GetPhaseColor(Volt_S.Value, Amp_S.Value) : "secondary";

        [NotMapped]
        public string PhaseTColor => IsThreePhase ? GetPhaseColor(Volt_T.Value, Amp_T.Value) : "secondary";

        private string GetStatus()
        {
            const decimal maxCapacity = 30000m;
            var loadPercent = Math.Min((Daya_Watt / maxCapacity) * 100, 100);
            if (loadPercent > 70) return "HIGH";
            if (loadPercent > 30) return "MEDIUM";
            return "NORMAL";
        }

        private string GetStatusColor()
        {
            const decimal maxCapacity = 30000m;
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
    }
}

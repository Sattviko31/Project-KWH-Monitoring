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

        [Column("DeviceKey")]
        public string DeviceKey { get; set; } = string.Empty;

        [Column("DeviceId")]
        public string DeviceId { get; set; } = string.Empty;

        [Column("AnomalyType")]
        public string AnomalyType { get; set; } = string.Empty;

        [Column("PowerValue")]
        public decimal PowerValue { get; set; }

        [Column("ThresholdValue")]
        public decimal ThresholdValue { get; set; }

        [Column("Deviation")]
        public decimal Deviation { get; set; }

        [Column("DetectedTime")]
        public DateTime DetectedTime { get; set; } = DateTime.Now;

        [Column("EMAValue")]
        public decimal? EMAValue { get; set; }

        [Column("ThresholdMode")]
        public string ThresholdMode { get; set; } = "manual";

        [Column("Acknowledged")]
        public bool Acknowledged { get; set; } = false;

        [Column("AcknowledgedTime")]
        public DateTime? AcknowledgedTime { get; set; }

        [Column("Notes")]
        // [BUG] Property ini tidak nullable (string) tapi tidak ada default value,
        // akan bernilai null saat runtime. Sebaiknya ubah ke string? atau tambah default.
        public string Notes { get; set; }
    }
}

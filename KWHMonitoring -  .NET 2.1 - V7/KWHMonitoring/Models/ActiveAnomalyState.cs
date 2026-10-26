using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("ActiveAnomalyStates")]
    public class ActiveAnomalyState
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        [Column("DeviceKey", TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string DeviceKey { get; set; } = string.Empty;

        [Required]
        [Column("AnomalyType", TypeName = "nvarchar(20)")]
        [MaxLength(20)]
        public string AnomalyType { get; set; } = string.Empty;

        [Column("ConfirmationCount")]
        public int ConfirmationCount { get; set; }

        [Column("MaxConfirmations")]
        public int MaxConfirmations { get; set; }

        [Column("IsConfirmed")]
        public bool IsConfirmed { get; set; }

        [Column("AnomalyLogId")]
        public long? AnomalyLogId { get; set; }

        [Column("FirstDetectedAt", TypeName = "datetime2")]
        public DateTime FirstDetectedAt { get; set; }

        [Column("LastDetectedAt", TypeName = "datetime2")]
        public DateTime LastDetectedAt { get; set; }

        [Column("UpdatedAt", TypeName = "datetime2")]
        public DateTime UpdatedAt { get; set; }
    }
}
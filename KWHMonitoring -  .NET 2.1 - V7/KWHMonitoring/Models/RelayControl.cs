using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KWHMonitoring.Models
{
    [Table("RelayControl")]
    public class RelayControl
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column(TypeName = "nvarchar(100)")]
        [MaxLength(100)]
        public string DeviceKey { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(10)")]
        [MaxLength(10)]
        public string RCI { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(50)")]
        [MaxLength(50)]
        public string DeviceId { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(100)")]
        [MaxLength(100)]
        public string GroupName { get; set; } = string.Empty;

        [Column(TypeName = "datetime2")]
        public DateTime ReceivedTime { get; set; }
    }
}

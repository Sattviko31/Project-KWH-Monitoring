using System;
using System.Collections.Generic;

namespace KWHMonitoring.Models
{
    public class ErpBillingRecord
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string KodeLokasi { get; set; } = string.Empty;
        public DateTime Periode { get; set; }
        public decimal JumlahTagihan { get; set; }
        public long TagihanListrikID { get; set; }
    }

    public class UsageCostComparisonRequest
    {
        public List<string> DeviceKeys { get; set; } = new List<string>();
        public string StartDate { get; set; }
        public string DeviceKey { get; set; }
    }
}
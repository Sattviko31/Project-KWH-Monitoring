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
        // Usage Statistics only needs the ERP comparison values. Keep anomaly
        // evaluation opt-in so the page does not wait for an expensive billing
        // analysis for every device and historical month.
        public bool? IncludeBillingAnomalyDetails { get; set; }
    }
}
using System;
using System.Collections.Generic;

// [UNUSED] - Seluruh file ini tidak digunakan. Controller mengembalikan anonymous objects
// alih-alih model ini. Pertimbangkan untuk menghapus jika tidak akan digunakan.
namespace KWHMonitoring.Models
{
    public class UsageStatistics
    {
        public List<HourlyUsage> HourlyData { get; set; } = new List<HourlyUsage>();
        public decimal TotalToday { get; set; }
        public decimal AvgPerHour { get; set; }
        public decimal PeakHour { get; set; }
        public string PeakHourTime { get; set; } = string.Empty;

        public List<DailyUsage> DailyData { get; set; } = new List<DailyUsage>();
        public decimal TotalThisMonth { get; set; }
        public decimal AvgPerDay { get; set; }
        public decimal PeakDay { get; set; }
        public string PeakDayDate { get; set; } = string.Empty;

        public List<MonthlyUsage> MonthlyData { get; set; } = new List<MonthlyUsage>();
        public decimal TotalThisYear { get; set; }
        public decimal AvgPerMonth { get; set; }
        public decimal PeakMonth { get; set; }
        public string PeakMonthName { get; set; } = string.Empty;

        public RecapStatistics Recap { get; set; } = new RecapStatistics();
    }

    public class HourlyUsage
    {
        public int Hour { get; set; }
        public decimal Energy { get; set; }
        public decimal AvgPower { get; set; }
        public string TimeLabel => string.Format("{0:D2}:00", Hour);
    }

    public class DailyUsage
    {
        public int Day { get; set; }
        public DateTime Date { get; set; }
        public decimal Energy { get; set; }
        public decimal AvgPower { get; set; }
        public string DateLabel => Date.ToString("dd/MM");
    }

    public class MonthlyUsage
    {
        public int Month { get; set; }
        public decimal Energy { get; set; }
        public decimal AvgPower { get; set; }
        public string MonthName => GetMonthName(Month);

        private static string GetMonthName(int month)
        {
            string[] months = { "Jan", "Feb", "Mar", "Apr", "Mei", "Jun",
                               "Jul", "Agu", "Sep", "Okt", "Nov", "Des" };
            return month >= 1 && month <= 12 ? months[month - 1] : "";
        }
    }

    public class RecapStatistics
    {
        public decimal TotalEnergy { get; set; }
        public decimal TotalCost { get; set; }
        public decimal CostPerKWh { get; set; } = 1500;
        public int TotalHours { get; set; }
        public decimal AvgPowerFactor { get; set; }
        public string Efficiency => GetEfficiencyLabel(AvgPowerFactor);

        private static string GetEfficiencyLabel(decimal pf)
        {
            if (pf >= 0.9m) return "Excellent";
            if (pf >= 0.8m) return "Good";
            if (pf >= 0.7m) return "Fair";
            return "Poor";
        }
    }
}

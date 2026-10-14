using System;

namespace KWHMonitoring.Services
{
    public sealed class BillingPeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime AsOf { get; set; }
        public DateTime EffectiveCutoffDate { get; set; }
        public int? ConfiguredCutoffDay { get; set; }
        public bool IsDeviceCutoff { get; set; }
        public string Mode { get; set; }
        public string ResolutionStatus { get; set; }

        public int PeriodDays => Math.Max(0, (End - Start).Days);
        public int ElapsedDays => Math.Max(0, Math.Min(PeriodDays, (AsOf - Start).Days));
        public int DaysRemaining => Math.Max(0, (End - AsOf).Days);
    }

    public static class BillingPeriodCalculator
    {
        public static BillingPeriod Resolve(DateTime asOf, bool cutoffEnabled, int? cutoffDay)
        {
            var localAsOf = asOf;
            var calendarStart = new DateTime(localAsOf.Year, localAsOf.Month, 1);
            var calendarEnd = calendarStart.AddMonths(1);

            if (!cutoffEnabled || !cutoffDay.HasValue)
            {
                return new BillingPeriod
                {
                    Start = calendarStart,
                    End = calendarEnd,
                    AsOf = localAsOf,
                    EffectiveCutoffDate = calendarEnd.AddDays(-1),
                    ConfiguredCutoffDay = null,
                    IsDeviceCutoff = false,
                    Mode = "calendar_month",
                    ResolutionStatus = "default"
                };
            }

            var day = cutoffDay.Value;
            if (day < 25 || day > 31)
                throw new ArgumentOutOfRangeException(nameof(cutoffDay), "Tanggal cutoff harus berada pada rentang 25 sampai 31.");

            var currentCutoff = CreateClampedDate(localAsOf.Year, localAsOf.Month, day);
            DateTime periodEnd;
            DateTime periodStart;

            if (localAsOf < currentCutoff)
            {
                periodEnd = currentCutoff;
                var previousMonth = localAsOf.AddMonths(-1);
                periodStart = CreateClampedDate(previousMonth.Year, previousMonth.Month, day).AddDays(1);
            }
            else
            {
                var nextMonth = localAsOf.AddMonths(1);
                periodStart = currentCutoff.AddDays(1);
                periodEnd = CreateClampedDate(nextMonth.Year, nextMonth.Month, day);
            }

            return new BillingPeriod
            {
                Start = periodStart,
                End = periodEnd,
                AsOf = localAsOf,
                EffectiveCutoffDate = periodEnd,
                ConfiguredCutoffDay = day,
                IsDeviceCutoff = true,
                Mode = "device_cutoff",
                ResolutionStatus = "configured"
            };
        }

        private static DateTime CreateClampedDate(int year, int month, int requestedDay)
        {
            return new DateTime(year, month, Math.Min(requestedDay, DateTime.DaysInMonth(year, month)));
        }
    }
}
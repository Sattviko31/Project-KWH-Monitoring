using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public class EnergyAggregationBackgroundService : BackgroundService
    {
        private readonly ILogger<EnergyAggregationBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public EnergyAggregationBackgroundService(
            ILogger<EnergyAggregationBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Energy Aggregation Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryAggregateAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in energy aggregation");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task TryAggregateAsync()
        {
            if (!await _lock.WaitAsync(0)) return;

            try
            {
                var now = DateTime.Now;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Hourly: aggregate previous hour
                    var previousHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-1);
                    await AggregateHourlyAsync(context, previousHour);

                    // Daily: at minute 0-2, aggregate previous day
                    if (now.Hour == 0 && now.Minute <= 2)
                    {
                        var yesterday = now.Date.AddDays(-1);
                        await AggregateDailyAsync(context, yesterday);
                    }

                    // Monthly: at day 1, hour 0, minute 0-2
                    if (now.Day == 1 && now.Hour == 0 && now.Minute <= 2)
                    {
                        var prevMonth = now.AddMonths(-1);
                        await AggregateMonthlyAsync(context, prevMonth.Year, prevMonth.Month);
                    }

                    // Yearly: at Jan 1, hour 0, minute 0-2
                    if (now.Month == 1 && now.Day == 1 && now.Hour == 0 && now.Minute <= 2)
                    {
                        await AggregateYearlyAsync(context, now.Year - 1);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        // ============================================
        // HOURLY: Trapezoidal rule from raw KWHData (per device)
        // ============================================
        public static async Task AggregateHourlyAsync(ApplicationDbContext context, DateTime hour)
        {
            var hourStart = hour;
            var hourEnd = hour.AddHours(1);

            var deviceKeys = await context.KWH_Monitoring
                .Where(x => x.Waktu_Server >= hourStart && x.Waktu_Server < hourEnd)
                .Select(x => x.DeviceKey)
                .Distinct()
                .ToListAsync();

            if (!deviceKeys.Any()) return;

            var deviceEnergies = new Dictionary<string, decimal>();

            foreach (var deviceKey in deviceKeys)
            {
                var firstBefore = await context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey && x.Waktu_Server < hourStart)
                    .OrderByDescending(x => x.Waktu_Server)
                    .FirstOrDefaultAsync();

                var readings = await context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey && x.Waktu_Server >= hourStart && x.Waktu_Server < hourEnd)
                    .OrderBy(x => x.Waktu_Server)
                    .ToListAsync();

                var firstAfter = await context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey && x.Waktu_Server >= hourEnd)
                    .OrderBy(x => x.Waktu_Server)
                    .FirstOrDefaultAsync();

                var allReadings = new List<KWHData>();
                if (firstBefore != null) allReadings.Add(firstBefore);
                allReadings.AddRange(readings);
                if (firstAfter != null) allReadings.Add(firstAfter);

                if (allReadings.Count < 2) continue;

                decimal totalEnergyWh = 0;

                for (int i = 1; i < allReadings.Count; i++)
                {
                    var prev = allReadings[i - 1];
                    var curr = allReadings[i];

                    var intervalStart = prev.Waktu_Server;
                    var intervalEnd = curr.Waktu_Server;

                    var overlapStart = intervalStart > hourStart ? intervalStart : hourStart;
                    var overlapEnd = intervalEnd < hourEnd ? intervalEnd : hourEnd;

                    if (overlapStart >= overlapEnd) continue;

                    var totalHours = (decimal)(intervalEnd - intervalStart).TotalHours;
                    var overlapHours = (decimal)(overlapEnd - overlapStart).TotalHours;
                    if (totalHours <= 0) continue;

                    var avgPower = (prev.Daya_Watt + curr.Daya_Watt) / 2m;
                    totalEnergyWh += avgPower * overlapHours;
                }

                if (totalEnergyWh > 0)
                    deviceEnergies[deviceKey] = totalEnergyWh;
            }

            foreach (var kvp in deviceEnergies)
            {
                var energyKWh = Math.Round(kvp.Value / 1000m, 4);

                var existing = await context.HourlyEnergy
                    .FirstOrDefaultAsync(x => x.DeviceKey == kvp.Key && x.Hour == hourStart);

                if (existing != null)
                {
                    existing.EnergyKWh = energyKWh;
                    existing.CalculatedAt = DateTime.Now;
                }
                else
                {
                    context.HourlyEnergy.Add(new HourlyEnergy
                    {
                        DeviceKey = kvp.Key,
                        Hour = hourStart,
                        EnergyKWh = energyKWh,
                        CalculatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // ============================================
        // DAILY: Sum of hourly records
        // ============================================
        public static async Task AggregateDailyAsync(ApplicationDbContext context, DateTime date)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var hourlyData = await context.HourlyEnergy
                .Where(x => x.Hour >= dayStart && x.Hour < dayEnd)
                .ToListAsync();

            var deviceTotals = hourlyData
                .GroupBy(x => x.DeviceKey)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

            foreach (var kvp in deviceTotals)
            {
                var existing = await context.DailyEnergy
                    .FirstOrDefaultAsync(x => x.DeviceKey == kvp.Key && x.Date == dayStart);

                if (existing != null)
                {
                    existing.EnergyKWh = Math.Round(kvp.Value, 4);
                    existing.CalculatedAt = DateTime.Now;
                }
                else
                {
                    context.DailyEnergy.Add(new DailyEnergy
                    {
                        DeviceKey = kvp.Key,
                        Date = dayStart,
                        EnergyKWh = Math.Round(kvp.Value, 4),
                        CalculatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // ============================================
        // MONTHLY: Sum of daily records
        // ============================================
        public static async Task AggregateMonthlyAsync(ApplicationDbContext context, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var dailyData = await context.DailyEnergy
                .Where(x => x.Date >= monthStart && x.Date < monthEnd)
                .ToListAsync();

            var deviceTotals = dailyData
                .GroupBy(x => x.DeviceKey)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

            foreach (var kvp in deviceTotals)
            {
                var existing = await context.MonthlyEnergy
                    .FirstOrDefaultAsync(x => x.DeviceKey == kvp.Key && x.Year == year && x.Month == month);

                if (existing != null)
                {
                    existing.EnergyKWh = Math.Round(kvp.Value, 4);
                    existing.CalculatedAt = DateTime.Now;
                }
                else
                {
                    context.MonthlyEnergy.Add(new MonthlyEnergy
                    {
                        DeviceKey = kvp.Key,
                        Year = year,
                        Month = month,
                        EnergyKWh = Math.Round(kvp.Value, 4),
                        CalculatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // ============================================
        // YEARLY: Sum of monthly records
        // ============================================
        public static async Task AggregateYearlyAsync(ApplicationDbContext context, int year)
        {
            var monthlyData = await context.MonthlyEnergy
                .Where(x => x.Year == year)
                .ToListAsync();

            var deviceTotals = monthlyData
                .GroupBy(x => x.DeviceKey)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));

            foreach (var kvp in deviceTotals)
            {
                var existing = await context.YearlyEnergy
                    .FirstOrDefaultAsync(x => x.DeviceKey == kvp.Key && x.Year == year);

                if (existing != null)
                {
                    existing.EnergyKWh = Math.Round(kvp.Value, 4);
                    existing.CalculatedAt = DateTime.Now;
                }
                else
                {
                    context.YearlyEnergy.Add(new YearlyEnergy
                    {
                        DeviceKey = kvp.Key,
                        Year = year,
                        EnergyKWh = Math.Round(kvp.Value, 4),
                        CalculatedAt = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // ============================================
        // BACKFILL: Aggregate all historical data
        // ============================================
        public static async Task<string> BackfillAllAsync(ApplicationDbContext context)
        {
            var minDate = await context.KWH_Monitoring
                .MinAsync(x => (DateTime?)x.Waktu_Server);

            if (minDate == null)
                return "No data found in KWHData table";

            var startDate = minDate.Value.Date;
            var now = DateTime.Now;
            int hourlyCount = 0, dailyCount = 0, monthlyCount = 0, yearlyCount = 0;

            var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            for (var hour = startDate; hour <= currentHour; hour = hour.AddHours(1))
            {
                await AggregateHourlyAsync(context, hour);
                hourlyCount++;
            }

            for (var day = startDate; day < now.Date; day = day.AddDays(1))
            {
                await AggregateDailyAsync(context, day);
                dailyCount++;
            }

            var monthCursor = new DateTime(startDate.Year, startDate.Month, 1);
            var lastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            while (monthCursor <= lastMonth)
            {
                await AggregateMonthlyAsync(context, monthCursor.Year, monthCursor.Month);
                monthlyCount++;
                monthCursor = monthCursor.AddMonths(1);
            }

            for (var year = startDate.Year; year < now.Year; year++)
            {
                await AggregateYearlyAsync(context, year);
                yearlyCount++;
            }

            return string.Format("Backfill complete: {0} hours, {1} days, {2} months, {3} years aggregated",
                hourlyCount, dailyCount, monthlyCount, yearlyCount);
        }
    }
}

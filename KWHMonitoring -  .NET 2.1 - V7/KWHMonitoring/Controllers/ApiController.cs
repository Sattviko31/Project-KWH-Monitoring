using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KWHMonitoring.Models;
using KWHMonitoring.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;

namespace KWHMonitoring.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApiController> _logger;

        public ApiController(ApplicationDbContext context, IMemoryCache cache, IServiceProvider serviceProvider, ILogger<ApiController> logger)
        {
            _context = context;
            _cache = cache;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        // ============================================
        // GET ALL PANELS
        // ============================================
        [HttpGet("panels")]
        public async Task<IActionResult> GetPanels([FromQuery] string search, [FromQuery] string status, [FromQuery] string phase)
        {
            try
            {
                var latestData = await _context.KWH_Monitoring
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                    .ToListAsync();

                // Load device categories from AppSettings
                var categorySettings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("DeviceCategory."))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                var validData = latestData.Where(x => x != null);

                // Filter by search text (groupName or deviceKey)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    validData = validData.Where(x =>
                        x.GroupName.ToLower().Contains(s) ||
                        x.DeviceKey.ToLower().Contains(s) ||
                        x.DeviceId.ToLower().Contains(s));
                }

                // Filter by status
                if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
                {
                    var st = status.Trim().ToUpper();
                    if (st == "HIGH")
                        validData = validData.Where(x => x.Daya_Watt > 20000);
                    else if (st == "MEDIUM")
                        validData = validData.Where(x => x.Daya_Watt > 10000 && x.Daya_Watt <= 20000);
                    else if (st == "NORMAL")
                        validData = validData.Where(x => x.Daya_Watt <= 10000);
                }

                // Filter by phase type
                if (!string.IsNullOrWhiteSpace(phase) && phase.ToLower() != "all")
                {
                    if (phase.ToLower() == "3phase")
                        validData = validData.Where(x => x.Volt_S.HasValue && x.Volt_T.HasValue && x.Amp_S.HasValue && x.Amp_T.HasValue);
                    else if (phase.ToLower() == "1phase")
                        validData = validData.Where(x => !(x.Volt_S.HasValue && x.Volt_T.HasValue && x.Amp_S.HasValue && x.Amp_T.HasValue));
                }

                var panels = validData.Select(data => new
                {
                    deviceKey = data.DeviceKey,
                    deviceId = data.DeviceId,
                    groupName = data.GroupName,
                    deviceCategory = categorySettings.ContainsKey("DeviceCategory." + data.DeviceKey)
                        ? categorySettings["DeviceCategory." + data.DeviceKey]
                        : "Billboard",
                    isThreePhase = data.IsThreePhase,
                    r = data.Volt_R,
                    s = data.Volt_S,
                    t = data.Volt_T,
                    ampR = data.Amp_R,
                    ampS = data.Amp_S,
                    ampT = data.Amp_T,
                    cosPhi = data.Cos_Phi,
                    dayaWatt = data.Daya_Watt,
                    totalW1M = data.TotalW1M_Wh,
                    energiAktif = data.Energi_Aktif_Wh,
                    totalEnergy = data.Total_Energy_Wh,
                    frekuensi = data.Frekuensi_Hz,
                    avgVoltage = data.AvgVoltage,
                    avgAmpere = data.AvgAmpere,
                    // Gunakan Status dari model agar konsisten dengan load bar
                    status = data.Status
                }).ToList();

                return Ok(panels);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // GET CHART DATA
        // ============================================
        [HttpGet("panels/{deviceKey}/chart")]
        public async Task<IActionResult> GetChartData(string deviceKey, int points = 20)
        {
            try
            {
                var data = await _context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey)
                    .OrderByDescending(x => x.Waktu_Server)
                    .Take(points)
                    .OrderBy(x => x.Waktu_Server)
                    .ToListAsync();

                var labels = data.Select(x => x.Waktu_Server.ToString("HH:mm:ss")).ToList();
                var voltageR = data.Select(x => (double)x.Volt_R).ToList();
                var voltageS = data.Select(x => x.Volt_S.HasValue ? (double?)x.Volt_S.Value : null).ToList();
                var voltageT = data.Select(x => x.Volt_T.HasValue ? (double?)x.Volt_T.Value : null).ToList();
                var ampR = data.Select(x => (double)x.Amp_R).ToList();
                var ampS = data.Select(x => x.Amp_S.HasValue ? (double?)x.Amp_S.Value : null).ToList();
                var ampT = data.Select(x => x.Amp_T.HasValue ? (double?)x.Amp_T.Value : null).ToList();
                var power = data.Select(x => (double)x.Daya_Watt).ToList();

                var isThreePhase = data.Any(x => x.Volt_S.HasValue && x.Volt_T.HasValue && x.Amp_S.HasValue && x.Amp_T.HasValue);

                return Ok(new
                {
                    deviceKey = deviceKey,
                    labels = labels,
                    isThreePhase = isThreePhase,
                    voltage = new { r = voltageR, s = voltageS, t = voltageT },
                    current = new { r = ampR, s = ampS, t = ampT },
                    power = power
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // GET STATISTICS
        // ============================================
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var latestData = await _context.KWH_Monitoring
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                    .ToListAsync();

                var validData = latestData.Where(x => x != null).ToList();

                var stats = new
                {
                    totalDaya = validData.Sum(x => x.Daya_Watt),
                    totalEnergy = validData.Sum(x => x.Total_Energy_Wh),
                    totalW1M = validData.Sum(x => x.TotalW1M_Wh),
                    totalEnergiAktif = validData.Sum(x => x.Energi_Aktif_Wh),
                    activePanels = validData.Count,
                    avgPowerFactor = validData.Count > 0 ? validData.Average(x => x.Cos_Phi) : 0,
                    timestamp = DateTime.Now.ToString("dd/MM/yyyy, HH:mm:ss")
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // GET USAGE STATISTICS (from aggregated tables)
        // ============================================
        [HttpPost("usage-statistics")]
        public async Task<IActionResult> GetUsageStatistics([FromBody] DateFilterRequest filter)
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                var jakartaOffset = TimeSpan.FromHours(7);
                var serverNow = new DateTimeOffset(utcNow, TimeSpan.Zero).ToOffset(jakartaOffset).DateTime;
                var serverToday = serverNow.Date;

                DateTime startDate;
                if (!string.IsNullOrWhiteSpace(filter?.StartDate) &&
                    DateTime.TryParse(filter.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    startDate = parsedDate.Date;
                }
                else
                {
                    startDate = serverToday;
                }

                var dayStart = startDate;
                var dayEnd = startDate.AddDays(1);
                var monthStart = new DateTime(startDate.Year, startDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var yearStart = new DateTime(startDate.Year, 1, 1);
                var yearEnd = yearStart.AddYears(1);

                // Hourly
                var hourlyRecords = await _context.HourlyEnergy
                    .Where(x => x.Hour >= dayStart && x.Hour < dayEnd)
                    .GroupBy(x => x.Hour.Hour)
                    .Select(g => new { Hour = g.Key, EnergyKWh = g.Sum(x => x.EnergyKWh) })
                    .ToListAsync();

                var hourlyData = Enumerable.Range(0, 24).Select(h => new
                {
                    timeLabel = string.Format("{0:D2}:00", h),
                    energy = Math.Round(hourlyRecords.FirstOrDefault(x => x.Hour == h)?.EnergyKWh ?? 0, 2),
                    sortKey = h
                }).ToList();

                // Daily
                var dailyRecords = await _context.DailyEnergy
                    .Where(x => x.Date >= monthStart && x.Date < monthEnd)
                    .GroupBy(x => x.Date.Day)
                    .Select(g => new { Day = g.Key, EnergyKWh = g.Sum(x => x.EnergyKWh) })
                    .ToListAsync();

                var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                var dailyData = Enumerable.Range(1, daysInMonth).Select(d => new
                {
                    dateLabel = string.Format("{0}/{1}/{2}", d, startDate.Month, startDate.Year),
                    energy = Math.Round(dailyRecords.FirstOrDefault(x => x.Day == d)?.EnergyKWh ?? 0, 2),
                    sortKey = d
                }).ToList();

                // Monthly
                var monthlyRecords = await _context.MonthlyEnergy
                    .Where(x => x.Year == startDate.Year)
                    .GroupBy(x => x.Month)
                    .Select(g => new { Month = g.Key, EnergyKWh = g.Sum(x => x.EnergyKWh) })
                    .ToListAsync();

                var monthlyData = Enumerable.Range(1, 12).Select(m => new
                {
                    monthName = GetMonthName(m),
                    energy = Math.Round(monthlyRecords.FirstOrDefault(x => x.Month == m)?.EnergyKWh ?? 0, 2),
                    sortKey = m
                }).ToList();

                // Real-time kWh if viewing today
                var isToday = startDate.Date == serverToday;
                decimal realtimeKWh = 0;
                int secondsToNextHour = 0;
                string currentHourLabel = "";
                decimal todayKWh = 0, monthKWh = 0, yearKWh = 0;

                if (isToday)
                {
                    var currentHourStart = new DateTime(serverNow.Year, serverNow.Month, serverNow.Day, serverNow.Hour, 0, 0);
                    var currentHourReadings = await _context.KWH_Monitoring
                        .Where(x => x.Waktu_Server >= currentHourStart)
                        .OrderBy(x => x.DeviceKey)
                        .ThenBy(x => x.Waktu_Server)
                        .ToListAsync();

                    if (currentHourReadings.Any())
                    {
                        var rtDeviceKeys = currentHourReadings.Select(x => x.DeviceKey).Distinct().ToList();
                        foreach (var dk in rtDeviceKeys)
                        {
                            var baseline = await _context.KWH_Monitoring
                                .Where(x => x.DeviceKey == dk && x.Waktu_Server < currentHourStart)
                                .OrderByDescending(x => x.Waktu_Server)
                                .FirstOrDefaultAsync();

                            var seq = new List<KWHData>();
                            if (baseline != null) seq.Add(baseline);
                            seq.AddRange(currentHourReadings.Where(x => x.DeviceKey == dk));

                            if (seq.Count < 2) continue;
                            decimal wh = 0;
                            for (int i = 1; i < seq.Count; i++)
                            {
                                var h = (decimal)(seq[i].Waktu_Server - seq[i - 1].Waktu_Server).TotalHours;
                                if (h <= 0) continue;
                                wh += (seq[i - 1].Daya_Watt + seq[i].Daya_Watt) / 2m * h;
                            }
                            realtimeKWh += wh / 1000m;
                        }
                    }

                    var currentHourIndex = serverNow.Hour;
                    currentHourLabel = string.Format("{0:D2}:00", currentHourIndex);
                    secondsToNextHour = (int)(currentHourStart.AddHours(1) - serverNow).TotalSeconds;

                    if (currentHourIndex >= 0 && currentHourIndex < hourlyData.Count)
                    {
                        hourlyData[currentHourIndex] = new
                        {
                            timeLabel = currentHourLabel,
                            energy = Math.Round(realtimeKWh, 2),
                            sortKey = currentHourIndex
                        };
                    }

                    todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);
                    var todayOldInDaily = dailyData.FirstOrDefault(x => x.sortKey == serverNow.Day)?.energy ?? 0;
                    monthKWh = Math.Round(dailyData.Sum(x => x.energy) - todayOldInDaily + todayKWh, 2);
                    var monthOldInMonthly = monthlyData.FirstOrDefault(x => x.sortKey == serverNow.Month)?.energy ?? 0;
                    yearKWh = Math.Round(monthlyData.Sum(x => x.energy) - monthOldInMonthly + monthKWh, 2);

                    // Update daily chart for today
                    var todayDay = serverNow.Day;
                    var todayDailyIdx = dailyData.FindIndex(x => x.sortKey == todayDay);
                    if (todayDailyIdx >= 0)
                    {
                        dailyData[todayDailyIdx] = new
                        {
                            dateLabel = string.Format("{0}/{1}/{2}", todayDay, startDate.Month, startDate.Year),
                            energy = todayKWh,
                            sortKey = todayDay
                        };
                    }

                    // Update monthly chart for current month
                    var currentMonthIdx = monthlyData.FindIndex(x => x.sortKey == serverNow.Month);
                    if (currentMonthIdx >= 0)
                    {
                        monthlyData[currentMonthIdx] = new
                        {
                            monthName = GetMonthName(serverNow.Month),
                            energy = monthKWh,
                            sortKey = serverNow.Month
                        };
                    }
                }
                else
                {
                    todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);
                    monthKWh = Math.Round(dailyData.Sum(x => x.energy), 2);
                    yearKWh = Math.Round(monthlyData.Sum(x => x.energy), 2);
                }

                // Statistics
                var avgPerHour = Math.Round(hourlyData.Average(x => x.energy), 2);
                var peakHour = Math.Round(hourlyData.Max(x => x.energy), 2);
                var peakHourTime = hourlyData.First(x => x.energy == hourlyData.Max(y => y.energy)).sortKey;
                var peakHourTimeStr = string.Format("{0:D2}:00", peakHourTime);

                var avgPerDay = Math.Round(dailyData.Average(x => x.energy), 2);
                var peakDay = Math.Round(dailyData.Max(x => x.energy), 2);
                var peakDayDate = dailyData.First(x => x.energy == dailyData.Max(y => y.energy)).sortKey;
                var peakDayDateStr = string.Format("{0}/{1}", peakDayDate, startDate.Month);

                var avgPerMonth = Math.Round(monthlyData.Average(x => x.energy), 2);
                var peakMonth = Math.Round(monthlyData.Max(x => x.energy), 2);
                var peakMonthName = monthlyData.First(x => x.energy == monthlyData.Max(y => y.energy)).monthName;

                var tariffPerKWh = await GetTariffPerKWh();
                var estimatedCost = Math.Round(monthKWh * tariffPerKWh, 2);

                return Ok(new
                {
                    totalToday = todayKWh,
                    avgPerHour, peakHour, peakHourTime = peakHourTimeStr,
                    totalThisMonth = monthKWh,
                    avgPerDay, peakDay, peakDayDate = peakDayDateStr,
                    totalThisYear = yearKWh,
                    avgPerMonth, peakMonth, peakMonthName,
                    hourlyData, dailyData, monthlyData,
                    tariffPerKWh, estimatedCost,
                    realtimeKWh = Math.Round(realtimeKWh, 4),
                    currentHourLabel = currentHourLabel,
                    secondsToNextHour = secondsToNextHour,
                    isToday = isToday,
                    serverDate = serverToday.ToString("yyyy-MM-dd"),
                    serverHour = serverNow.Hour,
                    serverDay = serverNow.Day,
                    serverMonth = serverNow.Month
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // USAGE STATISTICS PER DEVICE
        // ============================================
        [HttpPost("usage-statistics/{deviceKey}")]
        public async Task<IActionResult> GetDeviceUsageStatistics(string deviceKey, [FromBody] DateFilterRequest filter)
        {
            try
            {
                var utcNow = DateTime.UtcNow;
                var jakartaOffset = TimeSpan.FromHours(7);
                var serverNow = new DateTimeOffset(utcNow, TimeSpan.Zero).ToOffset(jakartaOffset).DateTime;
                var serverToday = serverNow.Date;

                DateTime startDate;
                if (!string.IsNullOrWhiteSpace(filter?.StartDate) &&
                    DateTime.TryParse(filter.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    startDate = parsedDate.Date;
                }
                else
                {
                    startDate = serverToday;
                }

                var dayStart = startDate;
                var dayEnd = startDate.AddDays(1);
                var monthStart = new DateTime(startDate.Year, startDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                // Hourly
                var hourlyRecords = await _context.HourlyEnergy
                    .Where(x => x.DeviceKey == deviceKey && x.Hour >= dayStart && x.Hour < dayEnd)
                    .ToListAsync();

                var hourlyData = Enumerable.Range(0, 24).Select(h => new
                {
                    timeLabel = string.Format("{0:D2}:00", h),
                    energy = Math.Round(hourlyRecords.FirstOrDefault(x => x.Hour.Hour == h)?.EnergyKWh ?? 0, 2),
                    sortKey = h
                }).ToList();

                // Daily
                var dailyRecords = await _context.DailyEnergy
                    .Where(x => x.DeviceKey == deviceKey && x.Date >= monthStart && x.Date < monthEnd)
                    .ToListAsync();

                var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                var dailyData = Enumerable.Range(1, daysInMonth).Select(d => new
                {
                    dateLabel = string.Format("{0}/{1}/{2}", d, startDate.Month, startDate.Year),
                    energy = Math.Round(dailyRecords.FirstOrDefault(x => x.Date.Day == d)?.EnergyKWh ?? 0, 2),
                    sortKey = d
                }).ToList();

                // Monthly
                var monthlyRecords = await _context.MonthlyEnergy
                    .Where(x => x.DeviceKey == deviceKey && x.Year == startDate.Year)
                    .ToListAsync();

                var monthlyData = Enumerable.Range(1, 12).Select(m => new
                {
                    monthName = GetMonthName(m),
                    energy = Math.Round(monthlyRecords.FirstOrDefault(x => x.Month == m)?.EnergyKWh ?? 0, 2),
                    sortKey = m
                }).ToList();

                // Totals
                var todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);
                var monthKWh = Math.Round(dailyData.Sum(x => x.energy), 2);
                var yearKWh = Math.Round(monthlyData.Sum(x => x.energy), 2);

                // All-time total
                var allTimeKWh = Math.Round(await _context.YearlyEnergy
                    .Where(x => x.DeviceKey == deviceKey)
                    .SumAsync(x => x.EnergyKWh), 2);

                // Statistics
                var avgPerHour = Math.Round(hourlyData.Average(x => x.energy), 2);
                var peakHour = Math.Round(hourlyData.Max(x => x.energy), 2);
                var peakHourTime = hourlyData.First(x => x.energy == hourlyData.Max(y => y.energy)).sortKey;

                var avgPerDay = Math.Round(dailyData.Average(x => x.energy), 2);
                var peakDay = Math.Round(dailyData.Max(x => x.energy), 2);
                var peakDayDate = dailyData.First(x => x.energy == dailyData.Max(y => y.energy)).sortKey;

                var avgPerMonth = Math.Round(monthlyData.Average(x => x.energy), 2);
                var peakMonth = Math.Round(monthlyData.Max(x => x.energy), 2);
                var peakMonthName = monthlyData.First(x => x.energy == monthlyData.Max(y => y.energy)).monthName;

                var tariffPerKWh = await GetTariffPerKWh();
                var estimatedCost = Math.Round(monthKWh * tariffPerKWh, 2);

                // Real-time kWh if viewing today
                // [BUG] Timezone tidak konsisten: GetUsageStatistics() pakai UTC+7 (Jakarta offset),
                // tapi GetDeviceUsageStatistics() pakai DateTime.Now untuk real-time section.
                // Jika server tidak di timezone Jakarta, hasil akan berbeda.
                var serverNowDev = DateTime.Now;
                var serverTodayDev = serverNowDev.Date;
                var isTodayDevice = startDate.Date == serverTodayDev;
                decimal realtimeKWh = 0;
                int secondsToNextHour = 0;
                string currentHourLabel = "";

                if (isTodayDevice)
                {
                    var currentHourStart = new DateTime(serverNow.Year, serverNow.Month, serverNow.Day, serverNow.Hour, 0, 0);
                    var baseline = await _context.KWH_Monitoring
                        .Where(x => x.DeviceKey == deviceKey && x.Waktu_Server < currentHourStart)
                        .OrderByDescending(x => x.Waktu_Server)
                        .FirstOrDefaultAsync();

                    var hourReadings = await _context.KWH_Monitoring
                        .Where(x => x.DeviceKey == deviceKey && x.Waktu_Server >= currentHourStart)
                        .OrderBy(x => x.Waktu_Server)
                        .ToListAsync();

                    var seq = new List<KWHData>();
                    if (baseline != null) seq.Add(baseline);
                    seq.AddRange(hourReadings);

                    if (seq.Count >= 2)
                    {
                        for (int i = 1; i < seq.Count; i++)
                        {
                            var h = (decimal)(seq[i].Waktu_Server - seq[i - 1].Waktu_Server).TotalHours;
                            if (h <= 0) continue;
                            realtimeKWh += (seq[i - 1].Daya_Watt + seq[i].Daya_Watt) / 2m * h / 1000m;
                        }
                    }

                    var currentHourIndex = serverNow.Hour;
                    currentHourLabel = string.Format("{0:D2}:00", currentHourIndex);
                    secondsToNextHour = (int)(currentHourStart.AddHours(1) - serverNow).TotalSeconds;

                    if (currentHourIndex >= 0 && currentHourIndex < hourlyData.Count)
                    {
                        hourlyData[currentHourIndex] = new
                        {
                            timeLabel = currentHourLabel,
                            energy = Math.Round(realtimeKWh, 2),
                            sortKey = currentHourIndex
                        };
                    }

                    todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);
                    var todayOldInDaily = dailyData.FirstOrDefault(x => x.sortKey == serverNow.Day)?.energy ?? 0;
                    monthKWh = Math.Round(dailyData.Sum(x => x.energy) - todayOldInDaily + todayKWh, 2);
                    var monthOldInMonthly = monthlyData.FirstOrDefault(x => x.sortKey == serverNow.Month)?.energy ?? 0;
                    yearKWh = Math.Round(monthlyData.Sum(x => x.energy) - monthOldInMonthly + monthKWh, 2);

                    avgPerHour = Math.Round(hourlyData.Average(x => x.energy), 2);
                    peakHour = Math.Round(hourlyData.Max(x => x.energy), 2);
                    peakHourTime = hourlyData.First(x => x.energy == hourlyData.Max(y => y.energy)).sortKey;

                    // Update daily chart for today
                    var todayDay = serverNow.Day;
                    var todayDailyIdx = dailyData.FindIndex(x => x.sortKey == todayDay);
                    if (todayDailyIdx >= 0)
                    {
                        dailyData[todayDailyIdx] = new
                        {
                            dateLabel = string.Format("{0}/{1}/{2}", todayDay, startDate.Month, startDate.Year),
                            energy = todayKWh,
                            sortKey = todayDay
                        };
                    }

                    // Update monthly chart for current month
                    var currentMonthIdx = monthlyData.FindIndex(x => x.sortKey == serverNow.Month);
                    if (currentMonthIdx >= 0)
                    {
                        monthlyData[currentMonthIdx] = new
                        {
                            monthName = GetMonthName(serverNow.Month),
                            energy = monthKWh,
                            sortKey = serverNow.Month
                        };
                    }

                    avgPerMonth = Math.Round(monthlyData.Average(x => x.energy), 2);
                    peakMonth = Math.Round(monthlyData.Max(x => x.energy), 2);
                    peakMonthName = monthlyData.First(x => x.energy == monthlyData.Max(y => y.energy)).monthName;
                    estimatedCost = Math.Round(monthKWh * tariffPerKWh, 2);
                }

                return Ok(new
                {
                    success = true, deviceKey = deviceKey,
                    today = new { total = todayKWh, avgPerHour = avgPerHour, peakHour = peakHour, peakHourTime = string.Format("{0:D2}:00", peakHourTime), hourlyData = hourlyData },
                    month = new { total = monthKWh, avgPerDay = avgPerDay, peakDay = peakDay, peakDayDate = string.Format("{0}/{1}", peakDayDate, startDate.Month), dailyData = dailyData },
                    year = new { total = yearKWh, avgPerMonth = avgPerMonth, peakMonth = peakMonth, peakMonthName = peakMonthName, monthlyData = monthlyData },
                    totalAllTime = allTimeKWh,
                    tariffPerKWh = tariffPerKWh, estimatedCost = estimatedCost,
                    realtimeKWh = Math.Round(realtimeKWh, 4),
                    currentHourLabel = currentHourLabel,
                    secondsToNextHour = secondsToNextHour,
                    isToday = isTodayDevice,
                    serverDate = serverTodayDev.ToString("yyyy-MM-dd"),
                    serverHour = serverNowDev.Hour
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // REAL-TIME KWH
        // ============================================
        [HttpGet("realtime-kwh")]
        public async Task<IActionResult> GetRealTimeKwh()
        {
            try
            {
                var now = DateTime.Now;
                var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

                var readings = await _context.KWH_Monitoring
                    .Where(x => x.Waktu_Server >= hourStart)
                    .OrderBy(x => x.DeviceKey)
                    .ThenBy(x => x.Waktu_Server)
                    .ToListAsync();

                if (!readings.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        currentHourKWh = 0,
                        currentHour = hourStart.ToString("yyyy-MM-dd HH:00"),
                        deviceCount = 0,
                        readingCount = 0,
                        timestamp = now.ToString("HH:mm:ss")
                    });
                }

                var deviceKeys = readings.Select(x => x.DeviceKey).Distinct().ToList();
                var baselines = new Dictionary<string, KWHData>();

                foreach (var dk in deviceKeys)
                {
                    var baseline = await _context.KWH_Monitoring
                        .Where(x => x.DeviceKey == dk && x.Waktu_Server < hourStart)
                        .OrderByDescending(x => x.Waktu_Server)
                        .FirstOrDefaultAsync();

                    if (baseline != null)
                        baselines[dk] = baseline;
                }

                decimal totalKWh = 0;
                int readingCount = readings.Count;

                foreach (var dk in deviceKeys)
                {
                    var deviceReadings = readings.Where(x => x.DeviceKey == dk).ToList();
                    var sequence = new List<KWHData>();
                    if (baselines.TryGetValue(dk, out var bl))
                        sequence.Add(bl);
                    sequence.AddRange(deviceReadings);

                    if (sequence.Count < 2) continue;

                    decimal energyWh = 0;
                    for (int i = 1; i < sequence.Count; i++)
                    {
                        var prev = sequence[i - 1];
                        var curr = sequence[i];
                        var hours = (decimal)(curr.Waktu_Server - prev.Waktu_Server).TotalHours;
                        if (hours <= 0) continue;
                        var avgPower = (prev.Daya_Watt + curr.Daya_Watt) / 2m;
                        energyWh += avgPower * hours;
                    }

                    totalKWh += energyWh / 1000m;
                }

                return Ok(new
                {
                    success = true,
                    currentHourKWh = Math.Round(totalKWh, 4),
                    currentHour = hourStart.ToString("yyyy-MM-dd HH:00"),
                    nextHour = hourStart.AddHours(1).ToString("HH:00"),
                    deviceCount = deviceKeys.Count,
                    readingCount = readingCount,
                    timestamp = now.ToString("HH:mm:ss"),
                    secondsToNextHour = (int)(hourStart.AddHours(1) - now).TotalSeconds
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // CHECK ENERGY TABLES
        // ============================================
        [HttpGet("energy-tables")]
        public async Task<IActionResult> GetEnergyTables()
        {
            try
            {
                var hourlyCount = await _context.HourlyEnergy.CountAsync();
                var dailyCount = await _context.DailyEnergy.CountAsync();
                var monthlyCount = await _context.MonthlyEnergy.CountAsync();
                var yearlyCount = await _context.YearlyEnergy.CountAsync();

                var hourlySample = await _context.HourlyEnergy
                    .OrderByDescending(x => x.Hour)
                    .Take(10)
                    .Select(x => new { x.DeviceKey, x.Hour, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();

                var dailySample = await _context.DailyEnergy
                    .OrderByDescending(x => x.Date)
                    .Take(10)
                    .Select(x => new { x.DeviceKey, x.Date, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();

                var monthlySample = await _context.MonthlyEnergy
                    .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                    .Take(10)
                    .Select(x => new { x.DeviceKey, x.Year, x.Month, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();

                var yearlySample = await _context.YearlyEnergy
                    .OrderByDescending(x => x.Year)
                    .Take(10)
                    .Select(x => new { x.DeviceKey, x.Year, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();

                var hourlyTotal = await _context.HourlyEnergy.SumAsync(x => x.EnergyKWh);
                var dailyTotal = await _context.DailyEnergy.SumAsync(x => x.EnergyKWh);
                var monthlyTotal = await _context.MonthlyEnergy.SumAsync(x => x.EnergyKWh);
                var yearlyTotal = await _context.YearlyEnergy.SumAsync(x => x.EnergyKWh);

                return Ok(new
                {
                    success = true,
                    summary = new
                    {
                        hourly = new { count = hourlyCount, totalKWh = Math.Round(hourlyTotal, 4) },
                        daily = new { count = dailyCount, totalKWh = Math.Round(dailyTotal, 4) },
                        monthly = new { count = monthlyCount, totalKWh = Math.Round(monthlyTotal, 4) },
                        yearly = new { count = yearlyCount, totalKWh = Math.Round(yearlyTotal, 4) }
                    },
                    samples = new
                    {
                        hourly = hourlySample,
                        daily = dailySample,
                        monthly = monthlySample,
                        yearly = yearlySample
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TRIGGER ENERGY AGGREGATION (Manual)
        // ============================================
        [HttpPost("trigger-aggregation")]
        public async Task<IActionResult> TriggerAggregation([FromBody] AggregationRequest request)
        {
            try
            {
                var context = HttpContext.RequestServices.GetService(typeof(ApplicationDbContext)) as ApplicationDbContext;

                if (request != null && request.BackfillAll)
                {
                    var result = await EnergyAggregationBackgroundService.BackfillAllAsync(context);
                    return Ok(new { success = true, message = result });
                }

                var messages = new List<string>();
                var now = DateTime.Now;

                if (request?.Hour != null)
                {
                    await EnergyAggregationBackgroundService.AggregateHourlyAsync(context, request.Hour.Value);
                    messages.Add(string.Format("Hourly aggregated for {0:yyyy-MM-dd HH:00}", request.Hour.Value));
                }
                else
                {
                    var prevHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(-1);
                    await EnergyAggregationBackgroundService.AggregateHourlyAsync(context, prevHour);
                    messages.Add(string.Format("Hourly aggregated for {0:yyyy-MM-dd HH:00}", prevHour));
                }

                if (request?.Date != null)
                {
                    await EnergyAggregationBackgroundService.AggregateDailyAsync(context, request.Date.Value.Date);
                    messages.Add(string.Format("Daily aggregated for {0:yyyy-MM-dd}", request.Date.Value.Date));
                }

                if (request?.Year != null && request?.Month != null)
                {
                    await EnergyAggregationBackgroundService.AggregateMonthlyAsync(context, request.Year.Value, request.Month.Value);
                    messages.Add(string.Format("Monthly aggregated for {0}-{1:D2}", request.Year.Value, request.Month.Value));
                }

                // [BUG] Jika Year != null DAN Month != null, AggregateYearlyAsync dipanggil 2x:
                // sekali dari blok if(Year != null && Month != null) di atas, dan sekali lagi di sini.
                // Seharusnya pakai else if.
                if (request?.Year != null)
                {
                    await EnergyAggregationBackgroundService.AggregateYearlyAsync(context, request.Year.Value);
                    messages.Add(string.Format("Yearly aggregated for {0}", request.Year.Value));
                }

                return Ok(new { success = true, messages = messages });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // [BUG] Tidak ada bounds check. Jika month < 1 atau month > 12 akan throw IndexOutOfRangeException.
        // Model UsageStatistics.GetMonthName() punya bounds check, tapi method ini tidak.
        private string GetMonthName(int month)
        {
            var months = new[] { "", "Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Ags", "Sep", "Okt", "Nov", "Des" };
            return months[month];
        }

        // ============================================
        // GET TARIFF PER KWH
        // ============================================
        private async Task<decimal> GetTariffPerKWh()
        {
            try
            {
                var tariffRecord = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.Contains("Tariff") || x.SettingKey.Contains("tariff"))
                    .ToListAsync();

                var specificTariff = tariffRecord.FirstOrDefault(x =>
                    x.SettingKey == "Tariff.PerKWh" ||
                    x.SettingKey == "TariffPerKWh" ||
                    string.Equals(x.SettingKey, "Tariff.PerKWh", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.SettingKey, "TariffPerKWh", StringComparison.OrdinalIgnoreCase));

                if (specificTariff != null && decimal.TryParse(specificTariff.SettingValue, out var result))
                {
                    return result;
                }

                foreach (var record in tariffRecord)
                {
                    if (decimal.TryParse(record.SettingValue, out var value))
                        return value;
                }

                return 1500m;
            }
            catch
            {
                return 1500m;
            }
        }

        // ============================================
        // GET TARIFF
        // ============================================
        [HttpGet("get-tariff")]
        public async Task<IActionResult> GetTariff()
        {
            try
            {
                var tariffRecord = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh");

                if (tariffRecord != null && decimal.TryParse(tariffRecord.SettingValue, out var tariff))
                {
                    return Ok(new { tariffPerKWh = tariff });
                }

                return Ok(new { tariffPerKWh = 1500m });
            }
            catch (Exception)
            {
                return Ok(new { tariffPerKWh = 1500m });
            }
        }

        // ============================================
        // SAVE TARIFF PER KWH
        // [BUG] Request.Body stream mungkin sudah dibaca oleh [FromBody] model binding,
        // sehingga ReadToEndAsync() bisa return empty string.
        // ============================================
        [HttpPost("save-tariff")]
        public async Task<IActionResult> SaveTariff([FromBody] Dictionary<string, string> data)
        {
            try
            {
                string body = null;
                if (data == null || data.Count == 0)
                {
                    using (var reader = new StreamReader(Request.Body))
                    {
                        body = await reader.ReadToEndAsync();
                    }
                    if (!string.IsNullOrEmpty(body))
                    {
                        data = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                    }
                }

                string tariffValue = null;
                if (data != null)
                {
                    data.TryGetValue("tariffPerKWh", out tariffValue);
                    if (string.IsNullOrEmpty(tariffValue))
                        data.TryGetValue("Tariff.PerKWh", out tariffValue);
                }

                if (!string.IsNullOrEmpty(tariffValue))
                {
                    var allTariffRecords = await _context.AppSettingsRecords
                        .Where(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh")
                        .ToListAsync();

                    if (allTariffRecords.Count > 1)
                    {
                        for (int i = 1; i < allTariffRecords.Count; i++)
                        {
                            _context.AppSettingsRecords.Remove(allTariffRecords[i]);
                        }
                        await _context.SaveChangesAsync();

                        allTariffRecords = await _context.AppSettingsRecords
                            .Where(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh")
                            .ToListAsync();
                    }

                    var existing = allTariffRecords.FirstOrDefault();
                    if (existing != null)
                    {
                        existing.SettingKey = "Tariff.PerKWh";
                        existing.SettingValue = tariffValue;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = "Tariff.PerKWh",
                            SettingValue = tariffValue,
                            UpdatedAt = DateTime.Now
                        });
                    }

                    var savedChanges = await _context.SaveChangesAsync();

                    var verifyRecord = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh");

                    return Ok(new
                    {
                        success = true,
                        message = "Tariff saved successfully",
                        tariffPerKWh = tariffValue,
                        verifyValue = verifyRecord?.SettingValue,
                        savedChanges = savedChanges
                    });
                }

                return BadRequest(new { error = "No tariff value provided", receivedData = data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        // ============================================
        // HISTORY DATA API
        // ============================================
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(string deviceKey, string fromDate, string toDate, int page = 1, int pageSize = 100)
        {
            try
            {
                DateTime? fromDateParsed = null;
                DateTime? toDateParsed = null;

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
                    fromDateParsed = parsedFrom;

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedTo))
                    toDateParsed = parsedTo;

                var query = _context.KWH_Monitoring.AsQueryable();

                if (!string.IsNullOrEmpty(deviceKey))
                    query = query.Where(x => x.DeviceKey == deviceKey);

                if (fromDateParsed.HasValue)
                    query = query.Where(x => x.Waktu_Server >= fromDateParsed.Value);

                if (toDateParsed.HasValue)
                    query = query.Where(x => x.Waktu_Server < toDateParsed.Value.AddDays(1));

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var data = await query
                    .OrderByDescending(x => x.Waktu_Server)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new
                {
                    data = data,
                    totalCount = totalCount,
                    page = page,
                    pageSize = pageSize,
                    totalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // HISTORY DATA API - DevExpress DataGrid Server-Side
        // ============================================
        [HttpGet("history-grid")]
        public async Task<IActionResult> GetHistoryGrid([FromQuery] DevExtremeDataGridRequest request)
        {
            try
            {
                var query = _context.KWH_Monitoring.AsQueryable();

                // Apply device key filter
                if (!string.IsNullOrEmpty(request.DeviceKey))
                    query = query.Where(x => x.DeviceKey == request.DeviceKey);

                // Apply date range filter
                if (!string.IsNullOrEmpty(request.FromDate) && DateTime.TryParse(request.FromDate, out var parsedFrom))
                    query = query.Where(x => x.Waktu_Server >= parsedFrom);

                if (!string.IsNullOrEmpty(request.ToDate) && DateTime.TryParse(request.ToDate, out var parsedTo))
                    query = query.Where(x => x.Waktu_Server < parsedTo.AddDays(1));

                // Apply dxDataGrid filter
                if (!string.IsNullOrEmpty(request.Filter))
                {
                    query = ApplyDataGridFilter(query, request.Filter);
                }

                // Apply dxDataGrid sort
                if (!string.IsNullOrEmpty(request.Sort))
                {
                    query = ApplyDataGridSort(query, request.Sort);
                }
                else
                {
                    query = query.OrderByDescending(x => x.Waktu_Server);
                }

                var totalCount = await query.CountAsync();

                // Calculate summaries
                List<DevExtremeSummaryItem> summary = null;
                if (request.TotalSummary != null)
                {
                    summary = await CalculateSummaries(query, request.TotalSummary);
                }

                // Apply paging
                var data = await query
                    .Skip(request.Skip)
                    .Take(request.Take)
                    .ToListAsync();

                var result = new
                {
                    data = data.Select(item => new
                    {
                        id = item.Id,
                        deviceKey = item.DeviceKey,
                        deviceId = item.DeviceId,
                        groupName = item.GroupName,
                        waktuDevice = item.Waktu_Device.ToString("dd/MM/yyyy HH:mm:ss"),
                        waktuServer = item.Waktu_Server.ToString("dd/MM/yyyy HH:mm:ss"),
                        voltR = item.Volt_R,
                        voltS = item.Volt_S,
                        voltT = item.Volt_T,
                        ampR = item.Amp_R,
                        ampS = item.Amp_S,
                        ampT = item.Amp_T,
                        cosPhi = item.Cos_Phi,
                        dayaWatt = item.Daya_Watt,
                        totalW1M = item.TotalW1M_Wh,
                        energiAktif = item.Energi_Aktif_Wh,
                        totalEnergy = item.Total_Energy_Wh,
                        frekuensi = item.Frekuensi_Hz,
                        status = item.Status,
                        statusColor = item.StatusColor
                    }).ToList(),
                    totalCount = totalCount
                };

                if (summary != null)
                {
                    return Ok(new
                    {
                        result.data,
                        result.totalCount,
                        summary
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // HISTORY EXPORT - DevExpress DataGrid
        // ============================================
        [HttpGet("history-export")]
        public async Task<IActionResult> ExportHistory([FromQuery] string deviceKey, [FromQuery] string fromDate, [FromQuery] string toDate, [FromQuery] string format = "csv")
        {
            try
            {
                var query = _context.KWH_Monitoring.AsQueryable();

                if (!string.IsNullOrEmpty(deviceKey))
                    query = query.Where(x => x.DeviceKey == deviceKey);

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
                    query = query.Where(x => x.Waktu_Server >= parsedFrom);

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedTo))
                    query = query.Where(x => x.Waktu_Server < parsedTo.AddDays(1));

                var data = await query
                    .OrderByDescending(x => x.Waktu_Server)
                    .Take(100000)
                    .ToListAsync();

                if (format == "excel")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("<?xml version=\"1.0\"?>");
                    sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
                    sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                    sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
                    sb.AppendLine("<Worksheet ss:Name=\"KWH History\"><Table>");
                    sb.AppendLine("<Row>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Id</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">DeviceKey</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">DeviceId</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">GroupName</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Waktu Device</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Waktu Server</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Volt R</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Volt S</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Volt T</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Amp R</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Amp S</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Amp T</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Cos Phi</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Daya Watt</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">TotalW1M</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Energi Aktif</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Total Energy</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Frekuensi</Data></Cell>");
                    sb.AppendLine("</Row>");

                    foreach (var item in data)
                    {
                        sb.AppendLine("<Row>");
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Id);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0}</Data></Cell>", item.DeviceKey);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0}</Data></Cell>", item.DeviceId);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0}</Data></Cell>", item.GroupName);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.Waktu_Device);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.Waktu_Server);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Volt_R);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Volt_S ?? 0);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Volt_T ?? 0);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Amp_R);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Amp_S ?? 0);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Amp_T ?? 0);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Cos_Phi);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Daya_Watt);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.TotalW1M_Wh);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Energi_Aktif_Wh);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Total_Energy_Wh);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Frekuensi_Hz);
                        sb.AppendLine("</Row>");
                    }

                    sb.AppendLine("</Table></Worksheet></Workbook>");

                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    return File(bytes, "application/vnd.ms-excel", string.Format("KWH_History_{0:yyyyMMdd_HHmmss}.xls", DateTime.Now));
                }

                // CSV format (default)
                var csv = new StringBuilder();
                csv.AppendLine("Id,DeviceKey,DeviceId,GroupName,Waktu_Device,Waktu_Server,Volt_R,Volt_S,Volt_T,Amp_R,Amp_S,Amp_T,Cos_Phi,Daya_Watt,TotalW1M_Wh,Energi_Aktif_Wh,Total_Energy_Wh,Frekuensi_Hz");

                foreach (var item in data)
                {
                    csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4:yyyy-MM-dd HH:mm:ss},{5:yyyy-MM-dd HH:mm:ss},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}",
                        item.Id, item.DeviceKey, item.DeviceId, item.GroupName,
                        item.Waktu_Device, item.Waktu_Server,
                        item.Volt_R, item.Volt_S, item.Volt_T, item.Amp_R, item.Amp_S, item.Amp_T,
                        item.Cos_Phi, item.Daya_Watt, item.TotalW1M_Wh, item.Energi_Aktif_Wh, item.Total_Energy_Wh, item.Frekuensi_Hz));
                }

                var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(csvBytes, "text/csv", string.Format("KWH_History_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private IQueryable<KWHData> ApplyDataGridFilter(IQueryable<KWHData> query, string filterJson)
        {
            try
            {
                var filter = JsonConvert.DeserializeObject<object[]>(filterJson);
                return ApplyFilterRecursive(query, filter);
            }
            catch
            {
                return query;
            }
        }

        private IQueryable<KWHData> ApplyFilterRecursive(IQueryable<KWHData> query, object[] filter)
        {
            if (filter.Length == 2 && filter[0] is string && filter[1] is string)
            {
                // Simple filter: ["field", "operator", "value"] but with 2 elements means it's a boolean
                return query;
            }

            if (filter.Length >= 3 && filter[0] is string fieldName)
            {
                var op = filter[1] as string;
                var value = filter[2];

                if (op == "and" || op == "or")
                {
                    // This is a group: [condition1, "and", condition2]
                    var left = filter[0] as object[];
                    var right = filter[2] as object[];

                    if (left != null && right != null)
                    {
                        if (op == "and")
                        {
                            query = ApplyFilterRecursive(query, left);
                            query = ApplyFilterRecursive(query, right);
                        }
                        // For "or", we'd need more complex expression trees - handle simple cases
                    }
                    return query;
                }

                // Simple condition: ["field", "operator", value]
                return ApplySimpleFilter(query, fieldName, op, value);
            }

            return query;
        }

        private IQueryable<KWHData> ApplySimpleFilter(IQueryable<KWHData> query, string fieldName, string op, object value)
        {
            var strValue = value?.ToString() ?? "";

            switch (fieldName)
            {
                case "deviceKey":
                case "DeviceKey":
                    if (op == "contains") query = query.Where(x => x.DeviceKey.Contains(strValue));
                    else if (op == "=") query = query.Where(x => x.DeviceKey == strValue);
                    else if (op == "startswith") query = query.Where(x => x.DeviceKey.StartsWith(strValue));
                    break;
                case "deviceId":
                case "DeviceId":
                    if (op == "contains") query = query.Where(x => x.DeviceId.Contains(strValue));
                    else if (op == "=") query = query.Where(x => x.DeviceId == strValue);
                    break;
                case "groupName":
                case "GroupName":
                    if (op == "contains") query = query.Where(x => x.GroupName.Contains(strValue));
                    else if (op == "=") query = query.Where(x => x.GroupName == strValue);
                    break;
                case "dayaWatt":
                case "Daya_Watt":
                    if (decimal.TryParse(strValue, out var wattVal))
                    {
                        if (op == "=") query = query.Where(x => x.Daya_Watt == wattVal);
                        else if (op == ">") query = query.Where(x => x.Daya_Watt > wattVal);
                        else if (op == "<") query = query.Where(x => x.Daya_Watt < wattVal);
                        else if (op == ">=") query = query.Where(x => x.Daya_Watt >= wattVal);
                        else if (op == "<=") query = query.Where(x => x.Daya_Watt <= wattVal);
                    }
                    break;
                case "totalEnergy":
                case "Total_Energy_Wh":
                    if (decimal.TryParse(strValue, out var energyVal))
                    {
                        if (op == "=") query = query.Where(x => x.Total_Energy_Wh == energyVal);
                        else if (op == ">") query = query.Where(x => x.Total_Energy_Wh > energyVal);
                        else if (op == "<") query = query.Where(x => x.Total_Energy_Wh < energyVal);
                        else if (op == ">=") query = query.Where(x => x.Total_Energy_Wh >= energyVal);
                        else if (op == "<=") query = query.Where(x => x.Total_Energy_Wh <= energyVal);
                    }
                    break;
                case "voltR":
                case "Volt_R":
                    if (decimal.TryParse(strValue, out var voltVal))
                    {
                        if (op == "=") query = query.Where(x => x.Volt_R == voltVal);
                        else if (op == ">") query = query.Where(x => x.Volt_R > voltVal);
                        else if (op == "<") query = query.Where(x => x.Volt_R < voltVal);
                    }
                    break;
                case "ampR":
                case "Amp_R":
                    if (decimal.TryParse(strValue, out var ampVal))
                    {
                        if (op == "=") query = query.Where(x => x.Amp_R == ampVal);
                        else if (op == ">") query = query.Where(x => x.Amp_R > ampVal);
                        else if (op == "<") query = query.Where(x => x.Amp_R < ampVal);
                    }
                    break;
            }

            return query;
        }

        // [BUG] EF.Property<object> bisa gagal saat runtime karena type harus diketahui
        // saat compile time untuk ordering. Sorting decimal vs string akan error.
        private IQueryable<KWHData> ApplyDataGridSort(IQueryable<KWHData> query, string sortJson)
        {
            try
            {
                var sorts = JsonConvert.DeserializeObject<DevExtremeSortItem[]>(sortJson);
                if (sorts == null || sorts.Length == 0) return query;

                IOrderedQueryable<KWHData> orderedQuery = null;
                foreach (var sort in sorts)
                {
                    var propName = MapSortField(sort.Selector);
                    if (orderedQuery == null)
                    {
                        orderedQuery = sort.Desc
                            ? query.OrderByDescending(x => EF.Property<object>(x, propName))
                            : query.OrderBy(x => EF.Property<object>(x, propName));
                    }
                    else
                    {
                        orderedQuery = sort.Desc
                            ? orderedQuery.ThenByDescending(x => EF.Property<object>(x, propName))
                            : orderedQuery.ThenBy(x => EF.Property<object>(x, propName));
                    }
                }

                return orderedQuery ?? query;
            }
            catch
            {
                return query.OrderByDescending(x => x.Waktu_Server);
            }
        }

        private string MapSortField(string selector)
        {
            if (selector == "deviceKey") return "DeviceKey";
            if (selector == "deviceId") return "DeviceId";
            if (selector == "groupName") return "GroupName";
            if (selector == "waktuDevice") return "Waktu_Device";
            if (selector == "waktuServer") return "Waktu_Server";
            if (selector == "voltR") return "Volt_R";
            if (selector == "voltS") return "Volt_S";
            if (selector == "voltT") return "Volt_T";
            if (selector == "ampR") return "Amp_R";
            if (selector == "ampS") return "Amp_S";
            if (selector == "ampT") return "Amp_T";
            if (selector == "cosPhi") return "Cos_Phi";
            if (selector == "dayaWatt") return "Daya_Watt";
            if (selector == "totalW1M") return "TotalW1M_Wh";
            if (selector == "energiAktif") return "Energi_Aktif_Wh";
            if (selector == "totalEnergy") return "Total_Energy_Wh";
            if (selector == "frekuensi") return "Frekuensi_Hz";
            if (selector == "id") return "Id";
            return "Waktu_Server";
        }

        private async Task<List<DevExtremeSummaryItem>> CalculateSummaries(IQueryable<KWHData> query, string summaryJson)
        {
            try
            {
                var summaries = JsonConvert.DeserializeObject<DevExtremeSummaryRequest[]>(summaryJson);
                var result = new List<DevExtremeSummaryItem>();

                if (summaries == null) return result;

                foreach (var s in summaries)
                {
                    object value = null;

                    if (s.Type == "count")
                    {
                        value = await query.CountAsync();
                    }
                    else if (s.Type == "sum")
                    {
                        if (s.Selector == "dayaWatt") value = await query.SumAsync(x => x.Daya_Watt);
                        else if (s.Selector == "totalEnergy") value = await query.SumAsync(x => x.Total_Energy_Wh);
                        else if (s.Selector == "energiAktif") value = await query.SumAsync(x => x.Energi_Aktif_Wh);
                        else if (s.Selector == "totalW1M") value = await query.SumAsync(x => x.TotalW1M_Wh);
                    }
                    else if (s.Type == "avg")
                    {
                        if (s.Selector == "voltR") value = Math.Round((double)(await query.AverageAsync(x => x.Volt_R)), 1);
                        else if (s.Selector == "ampR") value = Math.Round((double)(await query.AverageAsync(x => x.Amp_R)), 3);
                        else if (s.Selector == "cosPhi") value = Math.Round((double)(await query.AverageAsync(x => x.Cos_Phi)), 3);
                        else if (s.Selector == "dayaWatt") value = Math.Round((double)(await query.AverageAsync(x => x.Daya_Watt)), 0);
                        else if (s.Selector == "frekuensi") value = Math.Round((double)(await query.AverageAsync(x => x.Frekuensi_Hz)), 2);
                    }
                    else if (s.Type == "min")
                    {
                        if (s.Selector == "voltR") value = await query.MinAsync(x => x.Volt_R);
                        else if (s.Selector == "dayaWatt") value = await query.MinAsync(x => x.Daya_Watt);
                    }
                    else if (s.Type == "max")
                    {
                        if (s.Selector == "voltR") value = await query.MaxAsync(x => x.Volt_R);
                        else if (s.Selector == "dayaWatt") value = await query.MaxAsync(x => x.Daya_Watt);
                    }

                    result.Add(new DevExtremeSummaryItem
                    {
                        Selector = s.Selector,
                        Type = s.Type,
                        Value = value
                    });
                }

                return result;
            }
            catch
            {
                return new List<DevExtremeSummaryItem>();
            }
        }

        // ============================================
        // SAVE SYSTEM SETTINGS
        // ============================================
        [HttpPost("save-system-settings")]
        public async Task<IActionResult> SaveSystemSettings([FromBody] Dictionary<string, string> settings)
        {
            try
            {
                foreach (var kvp in settings)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Settings saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST DATABASE CONNECTION
        // ============================================
        [HttpPost("test-database-connection")]
        public async Task<IActionResult> TestDatabaseConnection([FromBody] DatabaseConnectionData data)
        {
            try
            {
                var connectionString = string.Format("Server={0},{1};Database={2};User Id={3};Password={4};TrustServerCertificate=True;",
                    data.server, data.port, data.database, data.user, data.password);

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                }

                return Ok(new { success = true, message = "Connection successful" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ============================================
        // GET SYSTEM SETTINGS
        // ============================================
        [HttpGet("get-system-settings")]
        public async Task<IActionResult> GetSystemSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
                    success = true,
                    settings = settings
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // EMA SETTINGS - GET
        // ============================================
        [HttpGet("get-ema-settings")]
        public async Task<IActionResult> GetEmaSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("ema") || 
                               x.SettingKey.StartsWith("refresh") || 
                               x.SettingKey.StartsWith("chart") ||
                               x.SettingKey == "useInitial100ForEma")
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
                    emaPeriod = GetInt(settings, "emaPeriod", 20),
                    emaMode = GetString(settings, "emaMode", "manual"),
                    emaUpperThreshold = GetInt(settings, "emaUpperThreshold", 30),
                    emaLowerThreshold = GetInt(settings, "emaLowerThreshold", 50),
                    emaFibUpper = GetDouble(settings, "emaFibUpper", 1.618),
                    emaFibLower = GetDouble(settings, "emaFibLower", 0.618),
                    emaShowLine = GetBool(settings, "emaShowLine", true),
                    emaShowThresholds = GetBool(settings, "emaShowThresholds", true),
                    useInitial100ForEma = GetBool(settings, "useInitial100ForEma", false),
                    refreshInterval = GetInt(settings, "refreshInterval", 10),
                    chartDataPoints = GetInt(settings, "chartDataPoints", 20)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // EMA SETTINGS - SAVE
        // ============================================
        [HttpPost("save-ema-settings")]
        public async Task<IActionResult> SaveEmaSettings([FromBody] EmaSettingsData data)
        {
            try
            {
                var settingsToSave = new Dictionary<string, string>
                {
                    { "emaPeriod", data.emaPeriod.ToString() },
                    { "emaMode", data.emaMode },
                    { "emaUpperThreshold", data.emaUpperThreshold.ToString() },
                    { "emaLowerThreshold", data.emaLowerThreshold.ToString() },
                    { "emaFibUpper", data.emaFibUpper.ToString() },
                    { "emaFibLower", data.emaFibLower.ToString() },
                    { "emaShowLine", data.emaShowLine.ToString() },
                    { "emaShowThresholds", data.emaShowThresholds.ToString() },
                    { "useInitial100ForEma", data.useInitial100ForEma.ToString() },
                    { "refreshInterval", data.refreshInterval.ToString() },
                    { "chartDataPoints", data.chartDataPoints.ToString() }
                };

                foreach (var kvp in settingsToSave)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // ANOMALY LOGS - GET
        // ============================================
        [HttpGet("anomaly-logs/{deviceKey}")]
        public async Task<IActionResult> GetAnomalyLogs(string deviceKey, int page = 1, int pageSize = 50)
        {
            try
            {
                IQueryable<AnomalyLog> query;

                if (deviceKey.ToUpper() == "ALL")
                {
                    query = _context.AnomalyLogs.OrderByDescending(x => x.DetectedTime);
                }
                else
                {
                    query = _context.AnomalyLogs
                        .Where(x => x.DeviceKey == deviceKey)
                        .OrderByDescending(x => x.DetectedTime);
                }

                var totalCount = await query.CountAsync();
                var logs = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        id = x.Id,
                        deviceKey = x.DeviceKey,
                        deviceId = x.DeviceId,
                        anomalyType = x.AnomalyType,
                        powerValue = x.PowerValue,
                        thresholdValue = x.ThresholdValue,
                        deviation = x.Deviation,
                        detectedTime = x.DetectedTime,
                        emaValue = x.EMAValue,
                        thresholdMode = x.ThresholdMode,
                        acknowledged = x.Acknowledged,
                        acknowledgedTime = x.AcknowledgedTime,
                        notes = x.Notes
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    totalCount = totalCount,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    logs = logs
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // GET INITIAL EMA FROM FIRST 100 DATA POINTS
        // ============================================
        [HttpGet("get-initial-ema/{deviceKey}")]
        public async Task<IActionResult> GetInitialEma(string deviceKey)
        {
            try
            {
                // Ambil setting EMA period dari database, default 20 jika tidak ada
                var periodStr = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey == "emaPeriod")
                    .Select(x => x.SettingValue)
                    .FirstOrDefaultAsync();

                if (!int.TryParse(periodStr, out int period) || period <= 0)
                {
                    period = 20;
                }

                // Ambil 100 data pertama dari device ini
                var first100Data = await _context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey)
                    .OrderBy(x => x.Waktu_Server)
                    .Take(100)
                    .ToListAsync();

                if (first100Data.Count == 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "No data available for this device",
                        emaBaseline = (double?)null,
                        emaValues = new List<double>()
                    });
                }

                // Hitung SMA (Simple Moving Average) dari 100 data pertama sebagai baseline
                // Baseline = rata-rata sederhana, bukan EMA, agar EMA di chart mulai dari garis horizontal
                double smaBaseline = first100Data.Average(x => (double)x.Daya_Watt);

                return Ok(new
                {
                    success = true,
                    message = $"Calculated SMA baseline from {first100Data.Count} initial data points with period {period}",
                    emaBaseline = smaBaseline,
                    dataPointsUsed = first100Data.Count,
                    period = period,
                    firstDataTime = first100Data.First().Waktu_Server,
                    lastDataTime = first100Data.Last().Waktu_Server
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // ANOMALY STATUS
        // ============================================
        [HttpGet("anomaly-status")]
        public async Task<IActionResult> GetAnomalyStatus()
        {
            try
            {
                var recentLogs = await _context.AnomalyLogs
                    .Where(x => x.DetectedTime >= DateTime.Now.AddMinutes(-5))
                    .OrderByDescending(x => x.DetectedTime)
                    .ToListAsync();

                var deviceStatus = recentLogs
                    .GroupBy(x => x.DeviceKey)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().AnomalyType
                    );

                return Ok(new { success = true, deviceStatus = deviceStatus });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DOWNTIME PERIOD CHECK
        // Cek apakah sekarang berada dalam periode jam mati (listrik sengaja dimatikan)
        // ============================================
        private async Task<DowntimeCheckResult> CheckDowntimePeriodAsync(string category = null)
        {
            var result = new DowntimeCheckResult { IsDowntime = false, StartHour = 0, EndHour = 0 };

            try
            {
                var validCategories = await GetValidCategoriesAsync();

                // If category is specified, check per-category downtime first
                if (!string.IsNullOrWhiteSpace(category) && validCategories.Contains(category))
                {
                    var prefix = "Downtime." + category + ".";
                    var catSettings = await _context.AppSettingsRecords
                        .Where(x => x.SettingKey.StartsWith(prefix))
                        .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                    string catEnabled;
                    if (catSettings.TryGetValue(prefix + "Enabled", out catEnabled) && catEnabled.ToLower() == "true")
                    {
                        result.StartHour = GetInt(catSettings, prefix + "StartHour", 22);
                        result.EndHour = GetInt(catSettings, prefix + "EndHour", 6);

                        var now = DateTime.Now;
                        var currentHour = now.Hour;

                        if (result.StartHour < result.EndHour)
                            result.IsDowntime = currentHour >= result.StartHour && currentHour < result.EndHour;
                        else
                            result.IsDowntime = currentHour >= result.StartHour || currentHour < result.EndHour;

                        return result;
                    }
                }

                // Fallback: check global downtime settings (exclude per-category keys)
                var globalQuery = _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Downtime"));
                foreach (var cat in validCategories)
                {
                    var catPrefix = "Downtime." + cat;
                    globalQuery = globalQuery.Where(x => !x.SettingKey.StartsWith(catPrefix));
                }
                var settings = await globalQuery.ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                string enabledVal;
                if (!settings.TryGetValue("Downtime.Enabled", out enabledVal))
                    enabledVal = "false";
                if (enabledVal.ToLower() != "true") return result;

                result.StartHour = GetInt(settings, "Downtime.StartHour", 22);
                result.EndHour = GetInt(settings, "Downtime.EndHour", 6);

                var now2 = DateTime.Now;
                var currentHour2 = now2.Hour;

                if (result.StartHour < result.EndHour)
                    result.IsDowntime = currentHour2 >= result.StartHour && currentHour2 < result.EndHour;
                else
                    result.IsDowntime = currentHour2 >= result.StartHour || currentHour2 < result.EndHour;
            }
            catch (Exception)
            {
                // Jika error, anggap bukan downtime
            }

            return result;
        }

        // ============================================
        // LOG ANOMALY (dengan downtime logic & server-side deduplication)
        // ============================================
        [HttpPost("log-anomaly")]
        public async Task<IActionResult> LogAnomaly([FromBody] AnomalyLogRequest data)
        {
            try
            {
                // ============================================
                // SERVER-SIDE DEDUPLICATION:
                // Cek apakah device ini sudah punya anomali aktif (belum di-reset)
                // dengan tipe yang sama. Jika sudah ada, skip notifikasi.
                // Anomali hanya bisa di-reset saat power kembali normal.
                // ============================================
                var activeAlertKey = "AnomalyAlert.Active." + data.DeviceKey;
                var activeAlertSetting = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == activeAlertKey);

                if (activeAlertSetting != null)
                {
                    // Parse: format = "ANOMALYTYPE|yyyy-MM-dd HH:mm:ss"
                    var parts = activeAlertSetting.SettingValue.Split('|');
                    if (parts.Length >= 2 && parts[0] == data.AnomalyType)
                    {
                        // Anomali tipe yang sama masih aktif, skip notifikasi
                        _logger.LogInformation("Anomaly alert for {DeviceKey} ({AnomalyType}) already active, skipping duplicate", data.DeviceKey, data.AnomalyType);
                        return Ok(new
                        {
                            success = true,
                            suppressed = true,
                            reason = "Anomaly alert already active for this device and type - waiting for reset",
                            logId = (long?)null
                        });
                    }
                }

                // Resolve device category for category-aware downtime check
                var categorySetting = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "DeviceCategory." + data.DeviceKey);
                var deviceCategory = categorySetting?.SettingValue ?? "Billboard";

                var downtime = await CheckDowntimePeriodAsync(deviceCategory);

                // ============================================
                // DOWNTIME LOGIC:
                // - Jika periode jam mati & anomali DROP → suppress (jangan log, jangan notifikasi)
                //   Karena listrik sengaja dimatikan, DROP adalah normal
                // - Jika periode jam mati & anomali OVERLOAD → LOG dengan tipe khusus
                //   Trigger: Power > EMA (bukan Upper Line) - menandakan listrik masih menyala normal
                //   Karena listrik seharusnya mati tapi masih menyala = anomali serius
                // - Jika bukan periode jam mati → proses normal (trigger: Power > Upper Line)
                // ============================================
                if (downtime.IsDowntime && data.AnomalyType == "DROP")
                {
                    // Suppressed: listrik sengaja dimatikan, DROP adalah expected
                    return Ok(new
                    {
                        success = true,
                        suppressed = true,
                        reason = string.Format("Downtime period ({0}:00-{1}:00) - DROP anomaly suppressed", downtime.StartHour, downtime.EndHour),
                        logId = (long?)null
                    });
                }

                var log = new AnomalyLog
                {
                    DeviceKey = data.DeviceKey,
                    DeviceId = data.DeviceId ?? "",
                    AnomalyType = data.AnomalyType,
                    PowerValue = data.PowerValue,
                    ThresholdValue = data.ThresholdValue,
                    Deviation = data.Deviation,
                    DetectedTime = DateTime.Now,
                    EMAValue = data.EMAValue,
                    ThresholdMode = data.ThresholdMode ?? "manual",
                    Acknowledged = false
                };

                // Jika downtime & OVERLOAD → tandai sebagai anomali pada jam mati
                if (downtime.IsDowntime && data.AnomalyType == "OVERLOAD")
                {
                    log.Notes = string.Format("ALERT: Power detected during downtime period ({0}:00-{1}:00). Expected OFF but power is {2:N0}W",
                        downtime.StartHour, downtime.EndHour, data.PowerValue);
                }

                _context.AnomalyLogs.Add(log);
                await _context.SaveChangesAsync();

                // Kirim notifikasi
                if (downtime.IsDowntime && data.AnomalyType == "OVERLOAD")
                {
                    // Notifikasi khusus: listrik seharusnya mati tapi masih menyala
                    using (var notifScope = _serviceProvider.CreateScope())
                    {
                        var notificationService = notifScope.ServiceProvider.GetRequiredService<NotificationService>();
                        await notificationService.SendDowntimePowerAlertAsync(
                            data.DeviceKey,
                            data.PowerValue,
                            downtime.StartHour,
                            downtime.EndHour
                        );
                    }
                }
                else
                {
                    // Notifikasi normal - menggunakan format rich sama seperti test instant alert
                    using (var notifScope = _serviceProvider.CreateScope())
                    {
                        var notificationService = notifScope.ServiceProvider.GetRequiredService<NotificationService>();
                        await notificationService.SendRealtimeInstantAlertAsync(
                            data.DeviceKey,
                            data.AnomalyType,
                            data.PowerValue,
                            data.ThresholdValue,
                            data.Deviation,
                            isTest: false
                        );
                    }
                }

                // Tandai anomali sebagai aktif (belum di-reset) di database
                // Format: "ANOMALYTYPE|yyyy-MM-dd HH:mm:ss"
                // activeAlertKey already declared at top of method
                var activeAlert = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == activeAlertKey);
                var alertValue = data.AnomalyType + "|" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                if (activeAlert == null)
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = activeAlertKey,
                        SettingValue = alertValue,
                        UpdatedAt = DateTime.Now
                    });
                }
                else
                {
                    activeAlert.SettingValue = alertValue;
                    activeAlert.UpdatedAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    suppressed = false,
                    downtime = downtime.IsDowntime,
                    logId = log.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // RESET ANOMALY ALERT
        // Dipanggil saat power kembali normal untuk meng-clear active alert state
        // Setelah reset, anomali baru bisa dikirim lagi untuk device tersebut
        // ============================================
        [HttpPost("reset-anomaly-alert")]
        public async Task<IActionResult> ResetAnomalyAlert([FromBody] ResetAnomalyAlertRequest data)
        {
            try
            {
                if (string.IsNullOrEmpty(data.DeviceKey))
                    return BadRequest(new { error = "DeviceKey is required" });

                var activeAlertKey = "AnomalyAlert.Active." + data.DeviceKey;
                var activeAlert = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == activeAlertKey);

                if (activeAlert != null)
                {
                    _context.AppSettingsRecords.Remove(activeAlert);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Anomaly alert reset for {DeviceKey}", data.DeviceKey);
                }

                return Ok(new { success = true, deviceKey = data.DeviceKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting anomaly alert for {DeviceKey}", data.DeviceKey);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DOWNTIME SETTINGS - GET
        // ============================================
        [HttpGet("get-downtime-settings")]
        public async Task<IActionResult> GetDowntimeSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Downtime"))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
                    enabled = GetBool(settings, "Downtime.Enabled", false),
                    startHour = GetInt(settings, "Downtime.StartHour", 22),
                    endHour = GetInt(settings, "Downtime.EndHour", 6),
                    description = GetString(settings, "Downtime.Description", "Periode listrik sengaja dimatikan")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DOWNTIME SETTINGS - SAVE
        // ============================================
        [HttpPost("save-downtime-settings")]
        public async Task<IActionResult> SaveDowntimeSettings([FromBody] DowntimeSettingsData data)
        {
            try
            {
                var settingsToSave = new Dictionary<string, string>
                {
                    { "Downtime.Enabled", data.enabled.ToString() },
                    { "Downtime.StartHour", data.startHour.ToString() },
                    { "Downtime.EndHour", data.endHour.ToString() },
                    { "Downtime.Description", data.description ?? "" }
                };

                foreach (var kvp in settingsToSave)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Downtime settings saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DOWNTIME STATUS (untuk frontend)
        // Frontend memanggil ini untuk tahu apakah lower line harus dimatikan
        // ============================================
        [HttpGet("downtime-status")]
        public async Task<IActionResult> GetDowntimeStatus([FromQuery] string category)
        {
            try
            {
                var downtime = await CheckDowntimePeriodAsync(category ?? null);

                return Ok(new
                {
                    success = true,
                    isDowntime = downtime.IsDowntime,
                    startHour = downtime.StartHour,
                    endHour = downtime.EndHour,
                    currentHour = DateTime.Now.Hour,
                    category = category ?? "Global",
                    message = downtime.IsDowntime
                        ? string.Format("Downtime period active ({0}:00-{1}:00) - Lower threshold disabled", downtime.StartHour, downtime.EndHour)
                        : "Normal operation - All thresholds active"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // PER-CATEGORY DOWNTIME SETTINGS - GET
        // ============================================
        [HttpGet("downtime-settings/{category}")]
        public async Task<IActionResult> GetCategoryDowntimeSettings(string category)
        {
            try
            {
                var validCategories = await GetValidCategoriesAsync();
                if (!validCategories.Contains(category))
                    return BadRequest(new { error = "Invalid category. Valid: " + string.Join(", ", validCategories) });

                var prefix = "Downtime." + category + ".";
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith(prefix))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
                    category = category,
                    enabled = GetBool(settings, prefix + "Enabled", false),
                    startHour = GetInt(settings, prefix + "StartHour", 22),
                    endHour = GetInt(settings, prefix + "EndHour", 6),
                    description = GetString(settings, prefix + "Description", category + " - Periode listrik sengaja dimatikan")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // PER-CATEGORY DOWNTIME SETTINGS - SAVE
        // ============================================
        [HttpPost("downtime-settings/{category}")]
        public async Task<IActionResult> SaveCategoryDowntimeSettings(string category, [FromBody] CategoryDowntimeSettingsData data)
        {
            try
            {
                var validCategories = await GetValidCategoriesAsync();
                if (!validCategories.Contains(category))
                    return BadRequest(new { error = "Invalid category. Valid: " + string.Join(", ", validCategories) });

                var prefix = "Downtime." + category + ".";
                var settingsToSave = new Dictionary<string, string>
                {
                    { prefix + "Enabled", data.enabled.ToString() },
                    { prefix + "StartHour", data.startHour.ToString() },
                    { prefix + "EndHour", data.endHour.ToString() },
                    { prefix + "Description", data.description ?? "" }
                };

                foreach (var kvp in settingsToSave)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = category + " downtime settings saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // ALL CATEGORIES DOWNTIME SETTINGS - GET
        // ============================================
        [HttpGet("all-downtime-settings")]
        public async Task<IActionResult> GetAllDowntimeSettings()
        {
            try
            {
                var categories = await GetValidCategoriesAsync();
                var result = new Dictionary<string, object>();

                // Global settings (exclude per-category keys)
                var globalQuery = _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Downtime"));
                foreach (var cat in categories)
                {
                    var catPrefix = "Downtime." + cat;
                    globalQuery = globalQuery.Where(x => !x.SettingKey.StartsWith(catPrefix));
                }
                var globalSettings = await globalQuery.ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                result["global"] = new
                {
                    enabled = GetBool(globalSettings, "Downtime.Enabled", false),
                    startHour = GetInt(globalSettings, "Downtime.StartHour", 22),
                    endHour = GetInt(globalSettings, "Downtime.EndHour", 6),
                    description = GetString(globalSettings, "Downtime.Description", "Periode listrik sengaja dimatikan")
                };

                // Per-category settings
                foreach (var cat in categories)
                {
                    var prefix = "Downtime." + cat + ".";
                    var catSettings = await _context.AppSettingsRecords
                        .Where(x => x.SettingKey.StartsWith(prefix))
                        .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                    result[cat] = new
                    {
                        enabled = GetBool(catSettings, prefix + "Enabled", false),
                        startHour = GetInt(catSettings, prefix + "StartHour", 22),
                        endHour = GetInt(catSettings, prefix + "EndHour", 6),
                        description = GetString(catSettings, prefix + "Description", cat + " - Periode listrik sengaja dimatikan")
                    };
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DEVICE CATEGORY - GET (single device)
        // ============================================
        [HttpGet("device-category/{deviceKey}")]
        public async Task<IActionResult> GetDeviceCategory(string deviceKey)
        {
            try
            {
                var setting = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "DeviceCategory." + deviceKey);

                return Ok(new
                {
                    deviceKey = deviceKey,
                    category = setting?.SettingValue ?? "Billboard"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DEVICE CATEGORY - SAVE (single device)
        // ============================================
        [HttpPost("device-category")]
        public async Task<IActionResult> SaveDeviceCategory([FromBody] DeviceCategoryData data)
        {
            try
            {
                var validCategories = await GetValidCategoriesAsync();
                if (!validCategories.Contains(data.category))
                    return BadRequest(new { error = "Invalid category. Valid: " + string.Join(", ", validCategories) });

                var key = "DeviceCategory." + data.deviceKey;
                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == key);

                if (existing != null)
                {
                    existing.SettingValue = data.category;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = key,
                        SettingValue = data.category,
                        UpdatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Device category saved", deviceKey = data.deviceKey, category = data.category });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // HELPER: Get all valid categories from database
        // ============================================
        private async Task<List<string>> GetValidCategoriesAsync()
        {
            var catSettings = await _context.AppSettingsRecords
                .Where(x => x.SettingKey.StartsWith("Category."))
                .OrderBy(x => x.SettingKey)
                .ToListAsync();

            if (catSettings.Count == 0)
            {
                // Seed default categories if none exist
                var defaults = new[] {
                    new AppSettingsRecord { SettingKey = "Category.Billboard", SettingValue = "{\"icon\":\"🔵\",\"color\":\"#2196f3\",\"description\":\"Perangkat kategori Billboard — Digunakan untuk panel iklan billboard.\"}", UpdatedAt = DateTime.Now },
                    new AppSettingsRecord { SettingKey = "Category.Megatron", SettingValue = "{\"icon\":\"🟠\",\"color\":\"#ff9800\",\"description\":\"Perangkat kategori Megatron — Digunakan untuk panel megatron / LED display.\"}", UpdatedAt = DateTime.Now },
                    new AppSettingsRecord { SettingKey = "Category.NeonBox", SettingValue = "{\"icon\":\"🟣\",\"color\":\"#9c27b0\",\"description\":\"Perangkat kategori Neon Box — Digunakan untuk box neon sign.\"}", UpdatedAt = DateTime.Now }
                };
                _context.AppSettingsRecords.AddRange(defaults);
                await _context.SaveChangesAsync();
                return new List<string> { "Billboard", "Megatron", "NeonBox" };
            }

            return catSettings
                .Select(x => x.SettingKey.Replace("Category.", ""))
                .ToList();
        }

        // ============================================
        // CATEGORIES - GET ALL (with metadata)
        // ============================================
        [HttpGet("categories")]
        public async Task<IActionResult> GetAllCategories()
        {
            try
            {
                var catSettings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Category."))
                    .OrderBy(x => x.SettingKey)
                    .ToListAsync();

                // Seed defaults if empty
                if (catSettings.Count == 0)
                {
                    await GetValidCategoriesAsync();
                    catSettings = await _context.AppSettingsRecords
                        .Where(x => x.SettingKey.StartsWith("Category."))
                        .OrderBy(x => x.SettingKey)
                        .ToListAsync();
                }

                var result = catSettings.Select(x =>
                {
                    var name = x.SettingKey.Replace("Category.", "");
                    string icon = "⚪", color = "#607d8b", description = "";
                    try
                    {
                        if (!string.IsNullOrEmpty(x.SettingValue))
                        {
                            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(x.SettingValue);
                            icon = parsed.icon?.ToString() ?? "⚪";
                            color = parsed.color?.ToString() ?? "#607d8b";
                            description = parsed.description?.ToString() ?? "";
                        }
                    }
                    catch { }

                    return new { name, icon, color, description };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // CATEGORY - ADD
        // ============================================
        [HttpPost("categories")]
        public async Task<IActionResult> AddCategory([FromBody] CategoryData data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data.name))
                    return BadRequest(new { error = "Category name is required" });

                // Clean name: remove spaces, keep alphanumeric
                var cleanName = data.name.Trim();
                if (cleanName.Length > 50)
                    return BadRequest(new { error = "Category name too long (max 50 chars)" });

                var key = "Category." + cleanName;
                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == key);

                if (existing != null)
                    return BadRequest(new { error = "Category '" + cleanName + "' already exists" });

                var meta = new
                {
                    icon = string.IsNullOrWhiteSpace(data.icon) ? "⚪" : data.icon,
                    color = string.IsNullOrWhiteSpace(data.color) ? "#607d8b" : data.color,
                    description = data.description ?? ""
                };

                _context.AppSettingsRecords.Add(new AppSettingsRecord
                {
                    SettingKey = key,
                    SettingValue = Newtonsoft.Json.JsonConvert.SerializeObject(meta),
                    UpdatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Category '" + cleanName + "' added successfully", name = cleanName, icon = meta.icon, color = meta.color, description = meta.description });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // CATEGORY - UPDATE
        // ============================================
        [HttpPut("categories/{name}")]
        public async Task<IActionResult> UpdateCategory(string name, [FromBody] CategoryData data)
        {
            try
            {
                var key = "Category." + name;
                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == key);

                if (existing == null)
                    return NotFound(new { error = "Category '" + name + "' not found" });

                var meta = new
                {
                    icon = string.IsNullOrWhiteSpace(data.icon) ? "⚪" : data.icon,
                    color = string.IsNullOrWhiteSpace(data.color) ? "#607d8b" : data.color,
                    description = data.description ?? ""
                };

                existing.SettingValue = Newtonsoft.Json.JsonConvert.SerializeObject(meta);
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Category '" + name + "' updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // CATEGORY - DELETE
        // ============================================
        [HttpDelete("categories/{name}")]
        public async Task<IActionResult> DeleteCategory(string name)
        {
            try
            {
                var key = "Category." + name;
                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == key);

                if (existing == null)
                    return NotFound(new { error = "Category '" + name + "' not found" });

                // Reassign devices from this category to Billboard
                var deviceCategoryKeys = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("DeviceCategory.") && x.SettingValue == name)
                    .ToListAsync();

                foreach (var dc in deviceCategoryKeys)
                {
                    dc.SettingValue = "Billboard";
                    dc.UpdatedAt = DateTime.Now;
                }

                // Delete downtime settings for this category
                var downtimeSettings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Downtime." + name + "."))
                    .ToListAsync();
                _context.AppSettingsRecords.RemoveRange(downtimeSettings);

                // Delete the category itself
                _context.AppSettingsRecords.Remove(existing);

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Category '" + name + "' deleted. " + deviceCategoryKeys.Count + " device(s) reassigned to Billboard.", reassignedCount = deviceCategoryKeys.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DEVICE CATEGORIES - GET ALL
        // ============================================
        [HttpGet("device-categories")]
        public async Task<IActionResult> GetAllDeviceCategories()
        {
            try
            {
                var validCategories = await GetValidCategoriesAsync();

                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("DeviceCategory."))
                    .ToDictionaryAsync(x => x.SettingKey.Replace("DeviceCategory.", ""), x => x.SettingValue);

                return Ok(new
                {
                    categories = validCategories,
                    devices = settings
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // NOTIFICATION SETTINGS - GET
        // ============================================
        [HttpGet("get-notification-settings")]
        public async Task<IActionResult> GetNotificationSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification"))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
                    smtpServer = GetString(settings, "Notification.SmtpServer", "smtp.gmail.com"),
                    smtpPort = GetInt(settings, "Notification.SmtpPort", 587),
                    senderEmail = GetString(settings, "Notification.SenderEmail", ""),
                    senderPassword = GetString(settings, "Notification.SenderPassword", ""),
                    recipientEmail = GetString(settings, "Notification.RecipientEmail", ""),
                    whatsappGatewayUrl = GetString(settings, "Notification.WhatsAppGatewayUrl", "https://api.fonnte.com/send"),
                    whatsappToken = GetString(settings, "Notification.WhatsAppToken", ""),
                    whatsappPhone = GetString(settings, "Notification.WhatsAppPhone", ""),
                    enableEmail = GetBool(settings, "Notification.EnableEmail", false),
                    enableWhatsApp = GetBool(settings, "Notification.EnableWhatsApp", false),
                    sendInstantAlert = GetBool(settings, "Notification.SendInstantAlert", true),
                    sendHourlyReport = GetBool(settings, "Notification.SendHourlyReport", true),
                    sendDailyReport = GetBool(settings, "Notification.SendDailyReport", false),
                    sendMonthlyReport = GetBool(settings, "Notification.SendMonthlyReport", false),
                    hourlyReportTime = 0,
                    dailyReportTime = GetString(settings, "Notification.DailyReportTime", "08:00"),
                    monthlyReportDay = GetInt(settings, "Notification.MonthlyReportDay", 1),
                    monthlyReportTime = GetString(settings, "Notification.MonthlyReportTime", "08:00"),
                    // Anomaly settings
                    anomalyCheckInterval = GetInt(settings, "Anomaly.CheckInterval", 30),
                    anomalyMaxConfirmations = GetInt(settings, "Anomaly.MaxConfirmations", 3),
                    anomalyCooldownTime = GetInt(settings, "Anomaly.CooldownTime", 60),
                    settingsReloadInterval = GetInt(settings, "Anomaly.SettingsReloadInterval", 60)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GET-NOTIF] Error: {0}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // ANOMALY SETTINGS - SAVE
        // ============================================
        [HttpPost("save-anomaly-settings")]
        public async Task<IActionResult> SaveAnomalySettings([FromBody] AnomalySettingsData data)
        {
            try
            {
                var settingsToSave = new Dictionary<string, string>
                {
                    { "Anomaly.CheckInterval", data.checkInterval.ToString() },
                    { "Anomaly.MaxConfirmations", data.maxConfirmations.ToString() },
                    { "Anomaly.CooldownTime", data.cooldownTime.ToString() },
                    { "Anomaly.SettingsReloadInterval", data.settingsReloadInterval.ToString() }
                };

                foreach (var kvp in settingsToSave)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Anomaly settings saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError("[SAVE-ANOMALY] Error: {0}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // ANOMALY STATE - GET (persist confirmation counts & cooldown state)
        // ============================================
        [HttpGet("get-anomaly-state")]
        public async Task<IActionResult> GetAnomalyState()
        {
            try
            {
                var confirmationCountsStr = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey == "AnomalyState.ConfirmationCounts")
                    .Select(x => x.SettingValue)
                    .FirstOrDefaultAsync() ?? "{}";

                var cooldownStateStr = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey == "AnomalyState.CooldownState")
                    .Select(x => x.SettingValue)
                    .FirstOrDefaultAsync() ?? "{}";

                return Ok(new
                {
                    success = true,
                    confirmationCounts = confirmationCountsStr,
                    cooldownState = cooldownStateStr
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GET-ANOMALY-STATE] Error: {0}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // ANOMALY STATE - SAVE (persist confirmation counts & cooldown state)
        // ============================================
        [HttpPost("save-anomaly-state")]
        public async Task<IActionResult> SaveAnomalyState([FromBody] AnomalyStateData data)
        {
            try
            {
                // Save confirmation counts
                var existingCounts = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "AnomalyState.ConfirmationCounts");
                if (existingCounts != null)
                {
                    existingCounts.SettingValue = data.confirmationCounts ?? "{}";
                    existingCounts.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = "AnomalyState.ConfirmationCounts",
                        SettingValue = data.confirmationCounts ?? "{}",
                        UpdatedAt = DateTime.Now
                    });
                }

                // Save cooldown state
                var existingCooldown = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "AnomalyState.CooldownState");
                if (existingCooldown != null)
                {
                    existingCooldown.SettingValue = data.cooldownState ?? "{}";
                    existingCooldown.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = "AnomalyState.CooldownState",
                        SettingValue = data.cooldownState ?? "{}",
                        UpdatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError("[SAVE-ANOMALY-STATE] Error: {0}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DEBUG - SHOW ALL NOTIFICATION SETTINGS IN DB
        // ============================================
        [HttpGet("debug/notification-settings-db")]
        public async Task<IActionResult> DebugNotificationSettingsDb()
        {
            try
            {
                var allSettings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification"))
                    .OrderBy(x => x.SettingKey)
                    .Select(x => new { x.SettingKey, x.SettingValue, x.UpdatedAt })
                    .ToListAsync();

                return Ok(new { count = allSettings.Count, settings = allSettings });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // DELETE SINGLE ANOMALY LOG
        // ============================================
        [HttpDelete("anomaly-logs/{id}")]
        public async Task<IActionResult> DeleteAnomalyLog(long id)
        {
            try
            {
                var log = await _context.AnomalyLogs.FindAsync(id);
                if (log == null)
                    return NotFound(new { error = "Log not found" });

                _context.AnomalyLogs.Remove(log);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Log deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // CLEAR ALL ANOMALY LOGS
        // ============================================
        [HttpDelete("anomaly-logs/clear-all")]
        public async Task<IActionResult> ClearAllAnomalyLogs()
        {
            try
            {
                var logs = await _context.AnomalyLogs.ToListAsync();
                _context.AnomalyLogs.RemoveRange(logs);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = string.Format("Cleared {0} logs", logs.Count) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // CLEAR ANOMALY LOGS BY DEVICE
        // ============================================
        [HttpDelete("anomaly-logs/clear/{deviceKey}")]
        public async Task<IActionResult> ClearAnomalyLogsByDevice(string deviceKey)
        {
            try
            {
                var logs = await _context.AnomalyLogs
                    .Where(x => x.DeviceKey == deviceKey)
                    .ToListAsync();

                _context.AnomalyLogs.RemoveRange(logs);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = string.Format("Cleared {0} logs for {1}", logs.Count, deviceKey) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // NOTIFICATION SETTINGS - SAVE
        // ============================================
        [HttpPost("save-notification-settings")]
        public async Task<IActionResult> SaveNotificationSettings([FromBody] NotificationSettingsData data)
        {
            try
            {
                _logger.LogInformation("[SAVE-NOTIF] Received settings - SendInstantAlert={0}, SendHourlyReport={1}, SendDailyReport={2}, SendMonthlyReport={3}, HourlyReport=every hour at :00, DailyTime={4}, MonthlyDay={5}, MonthlyTime={6}",
                    data.sendInstantAlert, data.sendHourlyReport, data.sendDailyReport, data.sendMonthlyReport,
                    data.dailyReportTime, data.monthlyReportDay, data.monthlyReportTime);

                var settingsToSave = new Dictionary<string, string>
                {
                    { "Notification.SmtpServer", data.smtpServer ?? "smtp.gmail.com" },
                    { "Notification.SmtpPort", data.smtpPort.ToString() },
                    { "Notification.SenderEmail", data.senderEmail ?? "" },
                    { "Notification.SenderPassword", data.senderPassword ?? "" },
                    { "Notification.RecipientEmail", data.recipientEmail ?? "" },
                    { "Notification.WhatsAppGatewayUrl", data.whatsappGatewayUrl ?? "" },
                    { "Notification.WhatsAppToken", data.whatsappToken ?? "" },
                    { "Notification.WhatsAppPhone", data.whatsappPhone ?? "" },
                    { "Notification.EnableEmail", data.enableEmail.ToString() },
                    { "Notification.EnableWhatsApp", data.enableWhatsApp.ToString() },
                    { "Notification.SendInstantAlert", data.sendInstantAlert.ToString() },
                    { "Notification.SendHourlyReport", data.sendHourlyReport.ToString() },
                    { "Notification.SendDailyReport", data.sendDailyReport.ToString() },
                    { "Notification.SendMonthlyReport", data.sendMonthlyReport.ToString() },
                    { "Notification.HourlyReportTime", "0" },
                    { "Notification.DailyReportTime", data.dailyReportTime ?? "08:00" },
                    { "Notification.MonthlyReportDay", data.monthlyReportDay.ToString() },
                    { "Notification.MonthlyReportTime", data.monthlyReportTime ?? "08:00" }
                };

                foreach (var kvp in settingsToSave)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                        _logger.LogInformation("[SAVE-NOTIF] Updated key: {0} = {1}", kvp.Key, kvp.Value);
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                        _logger.LogInformation("[SAVE-NOTIF] Added key: {0} = {1}", kvp.Key, kvp.Value);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("[SAVE-NOTIF] DB save successful. Total keys: {0}", settingsToSave.Count);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError("[SAVE-NOTIF] Error saving: {0}", ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST EMAIL
        // ============================================
        [HttpPost("test-email-notification")]
        public async Task<IActionResult> TestEmailNotification()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification"))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                var smtpServer = GetString(settings, "Notification.SmtpServer", "smtp.gmail.com");
                var smtpPort = GetInt(settings, "Notification.SmtpPort", 587);
                var senderEmail = GetString(settings, "Notification.SenderEmail", "");
                var senderPassword = GetString(settings, "Notification.SenderPassword", "");
                var recipientEmail = GetString(settings, "Notification.RecipientEmail", "");

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword) || string.IsNullOrEmpty(recipientEmail))
                {
                    return BadRequest(new { error = "Email settings not configured" });
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = "Test Email - KWH Monitoring",
                        Body = "<h2>Test Email</h2><p>This is a test email from KWH Monitoring System. If you receive this, email notifications are working correctly!</p>",
                        IsBodyHtml = true
                    };

                    var recipients = recipientEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var recipient in recipients)
                    {
                        mailMessage.To.Add(recipient.Trim());
                    }

                    await client.SendMailAsync(mailMessage);
                }

                return Ok(new { success = true, message = "Test email sent successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST WHATSAPP NOTIFICATION
        // ============================================
        [HttpPost("test-whatsapp-notification")]
        public async Task<IActionResult> TestWhatsAppNotification()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification"))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                var gatewayUrl = GetString(settings, "Notification.WhatsAppGatewayUrl", "");
                var token = GetString(settings, "Notification.WhatsAppToken", "");
                var phone = GetString(settings, "Notification.WhatsAppPhone", "");

                if (string.IsNullOrEmpty(gatewayUrl) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(phone))
                {
                    return BadRequest(new { error = "WhatsApp settings not configured" });
                }

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", token);

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("target", phone),
                        new KeyValuePair<string, string>("message", "Test WhatsApp from KWH Monitoring System. If you receive this, WhatsApp notifications are working correctly!")
                    });

                    var response = await httpClient.PostAsync(gatewayUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        return Ok(new { success = true, message = "Test WhatsApp sent successfully" });
                    }
                    else
                    {
                        return BadRequest(new { error = string.Format("WhatsApp API error: {0}", response.StatusCode) });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // RESCAN PANELS
        // ============================================
        [HttpPost("rescan-panels")]
        public async Task<IActionResult> RescanPanels()
        {
            try
            {
                var panels = await _context.KWH_Monitoring
                    .Select(x => x.DeviceKey)
                    .Distinct()
                    .ToListAsync();

                return Ok(new { success = true, count = panels.Count, panels = panels });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST INSTANT ALERT (Realtime)
        // Mengirim instant alert contoh berdasarkan anomali terbaru
        // ============================================
        [HttpPost("test-instant-alert")]
        public async Task<IActionResult> TestInstantAlert()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

                    // Ambil anomali terbaru untuk contoh
                    var latestAnomaly = await _context.AnomalyLogs
                        .OrderByDescending(x => x.DetectedTime)
                        .FirstOrDefaultAsync();

                    if (latestAnomaly == null)
                    {
                        // Jika tidak ada anomali, kirim contoh dummy
                        await notificationService.SendRealtimeInstantAlertAsync(
                            "DEVICE-TEST-001",
                            "OVERLOAD",
                            32500m,
                            30000m,
                            8.3m);
                        return Ok(new { success = true, message = "Test instant alert sent successfully (dummy data)" });
                    }

                    // Kirim dengan data anomali terbaru
                    await notificationService.SendRealtimeInstantAlertAsync(
                        latestAnomaly.DeviceKey,
                        latestAnomaly.AnomalyType,
                        latestAnomaly.PowerValue,
                        latestAnomaly.ThresholdValue,
                        latestAnomaly.Deviation);

                    return Ok(new { success = true, message = $"Test instant alert sent for {latestAnomaly.AnomalyType} ({latestAnomaly.DeviceKey})" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST HOURLY REPORT (Realtime)
        // Mengirim laporan jam ini (1 jam terakhir dari saat ini)
        // ============================================
        [HttpPost("test-hourly-report")]
        public async Task<IActionResult> TestHourlyReport()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                    await notificationService.SendRealtimeHourlyReportAsync();
                    return Ok(new { success = true, message = "Test hourly report sent successfully (real-time data from last 1 hour)" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST DAILY REPORT (Realtime)
        // Mengirim laporan hari ini (dari jam 00:00 sampai sekarang)
        // ============================================
        [HttpPost("test-daily-report")]
        public async Task<IActionResult> TestDailyReport()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                    await notificationService.SendRealtimeDailyReportAsync();
                    return Ok(new { success = true, message = "Test daily report sent successfully (real-time data from today)" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // TEST MONTHLY REPORT (Realtime)
        // Mengirim laporan bulan ini (dari tanggal 1 sampai sekarang)
        // ============================================
        [HttpPost("test-monthly-report")]
        public async Task<IActionResult> TestMonthlyReport()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                    await notificationService.SendRealtimeMonthlyReportAsync();
                    return Ok(new { success = true, message = "Test monthly report sent successfully (real-time data from this month)" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================
        private int GetInt(Dictionary<string, string> dict, string key, int defaultValue)
        {
            string value;
            return dict.TryGetValue(key, out value) && int.TryParse(value, out var result) ? result : defaultValue;
        }

        private double GetDouble(Dictionary<string, string> dict, string key, double defaultValue)
        {
            string value;
            return dict.TryGetValue(key, out value) && double.TryParse(value, out var result) ? result : defaultValue;
        }

        private bool GetBool(Dictionary<string, string> dict, string key, bool defaultValue)
        {
            string value;
            return dict.TryGetValue(key, out value) && bool.TryParse(value, out var result) ? result : defaultValue;
        }

        private string GetString(Dictionary<string, string> dict, string key, string defaultValue)
        {
            string value;
            return dict.TryGetValue(key, out value) ? value : defaultValue;
        }

        // ============================================
        // WABLAS - GET SETTINGS
        // ============================================
        [HttpGet("wablas/settings")]
        public async Task<IActionResult> GetWablasSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification.Wablas") || x.SettingKey == "Notification.EnableWhatsApp")
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
                    serverUrl = GetString(settings, "Notification.WablasServerUrl", ""),
                    token = GetString(settings, "Notification.WablasToken", ""),
                    secretKey = GetString(settings, "Notification.WablasSecretKey", ""),
                    phoneNumbers = GetString(settings, "Notification.WablasPhoneNumbers", ""),
                    enableWhatsApp = GetBool(settings, "Notification.EnableWhatsApp", false)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // WABLAS - SAVE SETTINGS
        // ============================================
        [HttpPost("wablas/settings")]
        public async Task<IActionResult> SaveWablasSettings([FromBody] WablasSettingsRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Invalid request body" });

                var settingsToSave = new Dictionary<string, string>
                {
                    { "Notification.WablasServerUrl", request.ServerUrl ?? "" },
                    { "Notification.WablasToken", request.Token ?? "" },
                    { "Notification.WablasSecretKey", request.SecretKey ?? "" },
                    { "Notification.WablasPhoneNumbers", request.PhoneNumbers != null ? string.Join(",", request.PhoneNumbers) : "" },
                    { "Notification.EnableWhatsApp", request.EnableWhatsApp.ToString().ToLower() }
                };

                foreach (var kvp in settingsToSave)
                {
                    var existing = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == kvp.Key);

                    if (existing != null)
                    {
                        existing.SettingValue = kvp.Value;
                        existing.UpdatedAt = DateTime.Now;
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Wablas settings saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ============================================
        // WABLAS - TEST CONNECTION
        // ============================================
        [HttpPost("wablas/test")]
        public async Task<IActionResult> TestWablasConnection([FromBody] WablasTestRequest request)
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification.Wablas") || x.SettingKey == "Notification.EnableWhatsApp")
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                var serverUrl = GetString(settings, "Notification.WablasServerUrl", "");
                var token = GetString(settings, "Notification.WablasToken", "");
                var secretKey = GetString(settings, "Notification.WablasSecretKey", "");
                var phoneNumbers = GetString(settings, "Notification.WablasPhoneNumbers", "");

                if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(token))
                    return BadRequest(new { error = "Wablas Server URL dan Token harus diisi terlebih dahulu" });

                // Build auth header: token.secret_key (or just token if no secret key)
                var authHeader = !string.IsNullOrEmpty(secretKey)
                    ? string.Format("{0}.{1}", token, secretKey)
                    : token;

                var phone = request.Phone;
                if (string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(phoneNumbers))
                    phone = phoneNumbers.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                if (string.IsNullOrEmpty(phone))
                    return BadRequest(new { error = "Nomor WhatsApp tujuan harus diisi" });

                var message = request.Message ?? "Test message dari KWH Monitoring System - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                // Format phone number
                phone = phone.Trim().Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "");
                if (phone.StartsWith("08"))
                    phone = "62" + phone.Substring(1);
                else if (phone.StartsWith("+62"))
                    phone = phone.Substring(1);

                var formattedUrl = serverUrl.TrimEnd('/');

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    // Try V2 API first (Authorization header)
                    try
                    {
                        httpClient.DefaultRequestHeaders.Add("Authorization", authHeader);

                        var payload = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    phone = phone,
                                    message = message,
                                    @type = "text"
                                }
                            }
                        };

                        var json = JsonConvert.SerializeObject(payload);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await httpClient.PostAsync(string.Format("{0}/api/v2/send-message", formattedUrl), content);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            return Ok(new
                            {
                                success = true,
                                message = string.Format("Test message berhasil dikirim ke {0}", phone),
                                response = responseBody
                            });
                        }

                        // If V2 fails with auth error, try V1 API
                        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            httpClient.DefaultRequestHeaders.Remove("Authorization");
                            // V1 uses query parameter (V1 hanya butuh token, bukan token.secret_key)
                            var v1Url = string.Format("{0}/api/send-message?token={1}", formattedUrl, token);
                            var formData = new Dictionary<string, string>
                            {
                                { "phone", phone },
                                { "message", message }
                            };
                            var formContent = new FormUrlEncodedContent(formData);
                            var v1Response = await httpClient.PostAsync(v1Url, formContent);
                            var v1ResponseBody = await v1Response.Content.ReadAsStringAsync();

                            if (v1Response.IsSuccessStatusCode)
                            {
                                return Ok(new
                                {
                                    success = true,
                                    message = string.Format("Test message berhasil dikirim ke {0} (via V1 API)", phone),
                                    response = v1ResponseBody
                                });
                            }

                            return BadRequest(new
                            {
                                error = string.Format("Wablas API error (V2: {0}, V1: {1})", response.StatusCode, v1Response.StatusCode),
                                v2Response = responseBody,
                                v1Response = v1ResponseBody
                            });
                        }

                        return BadRequest(new
                        {
                            error = string.Format("Wablas API error: {0}", response.StatusCode),
                            response = responseBody
                        });
                    }
                    catch
                    {
                        // Fallback to V1 if V2 fails entirely
                        httpClient.DefaultRequestHeaders.Remove("Authorization");
                        var v1Url = string.Format("{0}/api/send-message?token={1}", formattedUrl, authHeader);
                        var formData = new Dictionary<string, string>
                        {
                            { "phone", phone },
                            { "message", message }
                        };
                        var formContent = new FormUrlEncodedContent(formData);
                        var v1Response = await httpClient.PostAsync(v1Url, formContent);
                        var v1ResponseBody = await v1Response.Content.ReadAsStringAsync();

                        if (v1Response.IsSuccessStatusCode)
                        {
                            return Ok(new
                            {
                                success = true,
                                message = string.Format("Test message berhasil dikirim ke {0} (via V1 API)", phone),
                                response = v1ResponseBody
                            });
                        }

                        return BadRequest(new
                        {
                            error = string.Format("Wablas API error: {0}", v1Response.StatusCode),
                            response = v1ResponseBody,
                            debug = new { serverUrl = formattedUrl, phone = phone, hasToken = true, authHeaderLength = authHeader.Length }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        // ============================================
        // WABLAS - CHECK DEVICE STATUS
        // ============================================
        [HttpGet("wablas/device-status")]
        public async Task<IActionResult> GetWablasDeviceStatus()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification.Wablas") || x.SettingKey == "Notification.EnableWhatsApp")
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                var serverUrl = GetString(settings, "Notification.WablasServerUrl", "");
                var token = GetString(settings, "Notification.WablasToken", "");
                var secretKey = GetString(settings, "Notification.WablasSecretKey", "");
                var phones = GetString(settings, "Notification.WablasPhoneNumbers", "");
                var enabled = GetBool(settings, "Notification.EnableWhatsApp", false);

                // Diagnostic: show what we have
                if (string.IsNullOrEmpty(serverUrl))
                    return BadRequest(new { error = "ServerUrl kosong", detail = "Pastikan Wablas Server URL sudah diisi dan di-Save" });
                if (string.IsNullOrEmpty(token))
                    return BadRequest(new { error = "Token kosong", detail = "Pastikan Wablas Token sudah diisi dan di-Save" });

                var authHeader = !string.IsNullOrEmpty(secretKey)
                    ? string.Format("{0}.{1}", token, secretKey)
                    : token;

                var formattedUrl = serverUrl.TrimEnd('/');

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    // V1 endpoint with query parameter token
                    var url = string.Format("{0}/api/device/info?token={1}", formattedUrl, authHeader);

                    var response = await httpClient.GetAsync(url);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var result = JObject.Parse(responseBody);
                        var data = result["data"];
                        return Ok(new
                        {
                            success = true,
                            data = data,
                            raw = responseBody
                        });
                    }
                    else
                    {
                        return BadRequest(new
                        {
                            error = string.Format("Wablas API error: {0}", response.StatusCode),
                            response = responseBody,
                            debug = new { serverUrl = formattedUrl, hasToken = !string.IsNullOrEmpty(token), hasSecretKey = !string.IsNullOrEmpty(secretKey), authHeaderLength = authHeader.Length }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // ============================================
    // REQUEST MODELS
    // ============================================
    public class DatabaseConnectionData
    {
        public string server { get; set; } = string.Empty;
        public string port { get; set; } = "1433";
        public string user { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public string database { get; set; } = string.Empty;
    }

    public class EmaSettingsData
    {
        public int emaPeriod { get; set; } = 20;
        public string emaMode { get; set; } = "manual";
        public int emaUpperThreshold { get; set; } = 30;
        public int emaLowerThreshold { get; set; } = 50;
        public double emaFibUpper { get; set; } = 1.618;
        public double emaFibLower { get; set; } = 0.618;
        public bool emaShowLine { get; set; } = true;
        public bool emaShowThresholds { get; set; } = true;
        public bool useInitial100ForEma { get; set; } = false;
        public int refreshInterval { get; set; } = 10;
        public int chartDataPoints { get; set; } = 20;
    }

    public class NotificationSettingsData
    {
        public string smtpServer { get; set; }
        public int smtpPort { get; set; } = 587;
        public string senderEmail { get; set; }
        public string senderPassword { get; set; }
        public string recipientEmail { get; set; }
        public string whatsappGatewayUrl { get; set; }
        public string whatsappToken { get; set; }
        public string whatsappPhone { get; set; }
        public bool enableEmail { get; set; }
        public bool enableWhatsApp { get; set; }
        public bool sendInstantAlert { get; set; } = true;
        public bool sendHourlyReport { get; set; } = true;
        public bool sendDailyReport { get; set; }
        public bool sendMonthlyReport { get; set; }
        public int hourlyReportTime { get; set; } = 0;
        public string dailyReportTime { get; set; } = "08:00";
        public int monthlyReportDay { get; set; } = 1;
        public string monthlyReportTime { get; set; } = "08:00";
    }

    // Anomaly detection settings
    public class AnomalySettingsData
    {
        public int checkInterval { get; set; } = 30;
        public int maxConfirmations { get; set; } = 3;
        public int cooldownTime { get; set; } = 60;
        public int settingsReloadInterval { get; set; } = 60;
    }

    public class AnomalyStateData
    {
        public string confirmationCounts { get; set; } = "{}";
        public string cooldownState { get; set; } = "{}";
    }

    public class AnomalyLogRequest
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string DeviceId { get; set; }
        public string AnomalyType { get; set; } = string.Empty;
        public decimal PowerValue { get; set; }
        public decimal ThresholdValue { get; set; }
        public decimal Deviation { get; set; }
        public decimal? EMAValue { get; set; }
        public string ThresholdMode { get; set; }
    }

    public class DateFilterRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }

    public class AggregationRequest
    {
        public bool BackfillAll { get; set; }
        public DateTime? Hour { get; set; }
        public DateTime? Date { get; set; }
        public int? Year { get; set; }
        public int? Month { get; set; }
    }

    public class DowntimeCheckResult
    {
        public bool IsDowntime { get; set; }
        public int StartHour { get; set; }
        public int EndHour { get; set; }
    }

    public class DowntimeSettingsData
    {
        public bool enabled { get; set; }
        public int startHour { get; set; } = 22;
        public int endHour { get; set; } = 6;
        public string description { get; set; } = "Periode listrik sengaja dimatikan";
    }

    public class DevExtremeDataGridRequest
    {
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        public string Sort { get; set; }
        public string Filter { get; set; }
        public string TotalSummary { get; set; }
        public string Group { get; set; }
        public string DeviceKey { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public bool RequireTotalCount { get; set; } = true;
    }

    public class DevExtremeSortItem
    {
        public string Selector { get; set; }
        public bool Desc { get; set; }
    }

    public class DevExtremeSummaryRequest
    {
        public string Selector { get; set; }
        public string Type { get; set; }
    }

    public class DevExtremeSummaryItem
    {
        public string Selector { get; set; }
        public string Type { get; set; }
        public object Value { get; set; }
    }

    // ============================================
    // WABLAS REQUEST MODELS
    // ============================================
    public class WablasTestRequest
    {
        public string Phone { get; set; }
        public string Message { get; set; } = "Test message dari KWH Monitoring System";
    }

    public class WablasSettingsRequest
    {
        public string ServerUrl { get; set; }
        public string Token { get; set; }
        public string SecretKey { get; set; }
        public List<string> PhoneNumbers { get; set; }
        public bool EnableWhatsApp { get; set; }
    }

    public class CategoryDowntimeSettingsData
    {
        public bool enabled { get; set; }
        public int startHour { get; set; } = 22;
        public int endHour { get; set; } = 6;
        public string description { get; set; } = "";
    }

    public class DeviceCategoryData
    {
        public string deviceKey { get; set; } = string.Empty;
        public string category { get; set; } = "Billboard";
    }

    public class CategoryData
    {
        public string name { get; set; } = string.Empty;
        public string icon { get; set; } = "⚪";
        public string color { get; set; } = "#607d8b";
        public string description { get; set; } = "";
    }

    public class ResetAnomalyAlertRequest
    {
        public string DeviceKey { get; set; } = string.Empty;
    }
}

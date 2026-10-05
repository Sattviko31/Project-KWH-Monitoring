using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using KWHMonitoring.Models;
using KWHMonitoring.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Diagnostics;

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
        private readonly AesEncryptionService _encryption;
        private readonly MqttService _mqttService;
        private readonly IHostingEnvironment _environment;
        private readonly IEmailService _emailService;
        private readonly IAnomalyAnalysisService _analysisService;
        private readonly IDeviceSettingsService _deviceSettingsService;
        private readonly ITitikLokasiService _titikLokasiService;

        public ApiController(ApplicationDbContext context, IMemoryCache cache, IServiceProvider serviceProvider, ILogger<ApiController> logger, AesEncryptionService encryption, MqttService mqttService, IHostingEnvironment environment, IEmailService emailService, IAnomalyAnalysisService analysisService, IDeviceSettingsService deviceSettingsService, ITitikLokasiService titikLokasiService)
        {
            _context = context;
            _cache = cache;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _encryption = encryption;
            _mqttService = mqttService;
            _environment = environment;
            _emailService = emailService;
            _analysisService = analysisService;
            _deviceSettingsService = deviceSettingsService;
            _titikLokasiService = titikLokasiService;
        }

        // Revenue loss is an estimate based on configured hourly revenue and the
        // duration of a DROP. Keep this calculation shared by list, detail and summaries.
        private static decimal EstimateRevenueLoss(string anomalyType, DateTime detectedTime, bool isResolved, DateTime? resolvedTime, decimal revenuePerHour, DateTime now)
        {
            if (anomalyType != "DROP" || revenuePerHour <= 0m)
                return 0m;

            // A resolved record without a resolution timestamp has no defensible
            // duration, so report zero instead of continuing to accrue it.
            var endTime = isResolved
                ? (resolvedTime.HasValue ? resolvedTime.Value : detectedTime)
                : now;
            var elapsedHours = (decimal)(endTime - detectedTime).TotalHours;
            if (elapsedHours <= 0m)
                return 0m;

            return Math.Round(elapsedHours * revenuePerHour, 0, MidpointRounding.AwayFromZero);
        }

        private async Task<string> GetSettingValueAsync(string key, string defaultValue = "")
        {
            var record = await _context.AppSettingsRecords
                .FirstOrDefaultAsync(x => x.SettingKey == key);
            return record != null ? record.SettingValue : defaultValue;
        }

        private async Task LogSecurityActionAsync(SecurityAction action, string targetDevice, string details, bool success)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? userId = int.TryParse(userIdClaim, out var parsedId) ? (int?)parsedId : null;
                var email = User.Identity.Name ?? "unknown";
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                _context.SecurityAuditLogs.Add(new SecurityAuditLog
                {
                    UserId = userId,
                    Email = email,
                    Action = action,
                    TargetDevice = targetDevice ?? string.Empty,
                    Details = details,
                    Success = success,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Audit log failure should not break the main flow
            }
        }

        private IActionResult SafeError(Exception ex, string context = null)
        {
            var msg = !string.IsNullOrEmpty(context) ? $"[{context}] " : "";
            _logger.LogError(ex, msg + "Unhandled exception");
            return StatusCode(500, new { error = "Terjadi kesalahan internal. Silakan hubungi administrator." });
        }

        // Rate limiting: max 10 requests per 60 seconds per user for relay control
        private bool IsRelayControlRateLimited(string userIdentifier)
        {
            var key = "RelayRateLimit_" + userIdentifier;
            var now = DateTime.UtcNow;
            var windowStart = now.AddSeconds(-60);

            if (!_cache.TryGetValue(key, out List<DateTime> timestamps))
            {
                timestamps = new List<DateTime>();
            }

            timestamps = timestamps.Where(t => t > windowStart).ToList();

            if (timestamps.Count >= 10)
            {
                return true;
            }

            timestamps.Add(now);
            _cache.Set(key, timestamps, TimeSpan.FromMinutes(1));
            return false;
        }

        // ============================================
        // GET ALL PANELS
        // ============================================
        [HttpGet("panels")]
        [AllowAnonymous] // Feed dashboard publik (read-only)
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

                // Load per-device settings for status calculation
                var deviceSettingsDict = await _deviceSettingsService.GetAllEffectiveAsync();
                var erpCapacityMap = await _titikLokasiService.GetByDeviceKeysAsync(
                    latestData.Where(x => x != null).Select(x => x.DeviceKey));

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
                        validData = validData.Where(x => (x.Daya_Watt ?? 0m) > 20000);
                    else if (st == "MEDIUM")
                        validData = validData.Where(x => (x.Daya_Watt ?? 0m) > 10000 && (x.Daya_Watt ?? 0m) <= 20000);
                    else if (st == "NORMAL")
                        validData = validData.Where(x => (x.Daya_Watt ?? 0m) <= 10000);
                }

                // Filter by phase type
                if (!string.IsNullOrWhiteSpace(phase) && phase.ToLower() != "all")
                {
                    if (phase.ToLower() == "3phase")
                        validData = validData.Where(x => x.IsThreePhase);
                    else if (phase.ToLower() == "1phase")
                        validData = validData.Where(x => !x.IsThreePhase);
                }

                var panels = validData.Select(data =>
                {
                    var installedCapacityVA = erpCapacityMap.TryGetValue(data.DeviceKey, out var erp)
                        ? erp.DayaVA
                        : 0m;
                    var maxCapacity = PanelViewModel.CalculateMaxCapacityWatt(
                        installedCapacityVA,
                        data.Cos_Phi ?? 0m);

                    return new
                    {
                        deviceKey = data.DeviceKey,
                        deviceId = data.DeviceId,
                        groupName = data.GroupName,
                        deviceCategory = categorySettings.ContainsKey("DeviceCategory." + data.DeviceKey)
                            ? categorySettings["DeviceCategory." + data.DeviceKey]
                            : "Billboard",
                        isThreePhase = data.IsThreePhase,
                        r = data.Volt_R ?? 0m,
                        s = data.Volt_S ?? 0m,
                        t = data.Volt_T ?? 0m,
                        ampR = data.Amp_R ?? 0m,
                        ampS = data.Amp_S ?? 0m,
                        ampT = data.Amp_T ?? 0m,
                        cosPhi = data.Cos_Phi ?? 0m,
                        dayaWatt = data.Daya_Watt ?? 0m,
                        totalW1M = data.TotalW1M_Wh ?? 0m,
                        energiAktif = data.Energi_Aktif_Wh ?? 0m,
                        totalEnergy = data.Total_Energy_Wh ?? 0m,
                        frekuensi = data.Frekuensi_Hz ?? 0m,
                        avgVoltage = data.AvgVoltage,
                        avgAmpere = data.AvgAmpere,
                        phaseRColor = data.PhaseRColor,
                        phaseSColor = data.PhaseSColor,
                        phaseTColor = data.PhaseTColor,
                        installedCapacityVA,
                        maxCapacity,
                        status = GetDeviceStatus(data, deviceSettingsDict, erpCapacityMap)
                    };
                }).ToList();

                return Ok(panels);
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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
                var timestamps = data.Select(x => x.Waktu_Server).ToList();
                var voltageR = data.Select(x => (double)(x.Volt_R ?? 0m)).ToList();
                var voltageS = data.Select(x => x.Volt_S.HasValue ? (double?)x.Volt_S.Value : null).ToList();
                var voltageT = data.Select(x => x.Volt_T.HasValue ? (double?)x.Volt_T.Value : null).ToList();
                var ampR = data.Select(x => (double)(x.Amp_R ?? 0m)).ToList();
                var ampS = data.Select(x => x.Amp_S.HasValue ? (double?)x.Amp_S.Value : null).ToList();
                var ampT = data.Select(x => x.Amp_T.HasValue ? (double?)x.Amp_T.Value : null).ToList();
                var power = data.Select(x => (double)(x.Daya_Watt ?? 0m)).ToList();
                var powerValid = data.Select(x => x.Daya_Watt.HasValue).ToList();

                var isThreePhase = data.Any(x => x.IsThreePhase);
                var erpCapacityMap = await _titikLokasiService.GetByDeviceKeysAsync(new[] { deviceKey });
                var installedCapacityVA = erpCapacityMap.TryGetValue(deviceKey, out var erpCapacity)
                    ? erpCapacity.DayaVA
                    : 0m;
                var maxCapacity = PanelViewModel.CalculateMaxCapacityWatt(
                    installedCapacityVA,
                    data.LastOrDefault()?.Cos_Phi ?? 0m);

                return Ok(new
                {
                    deviceKey = deviceKey,
                    labels = labels,
                    timestamps = timestamps,
                    powerValid = powerValid,
                    isThreePhase = isThreePhase,
                    voltage = new { r = voltageR, s = voltageS, t = voltageT },
                    current = new { r = ampR, s = ampS, t = ampT },
                    power = power,
                    installedCapacityVA,
                    maxCapacity
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // GET STATISTICS
        // ============================================
        [HttpGet("statistics")]
        [AllowAnonymous] // Ringkasan statistik dashboard (read-only)
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
                    totalDaya = validData.Sum(x => x.Daya_Watt) ?? 0m,
                    totalEnergy = validData.Sum(x => x.Total_Energy_Wh) ?? 0m,
                    totalW1M = validData.Sum(x => x.TotalW1M_Wh) ?? 0m,
                    totalEnergiAktif = validData.Sum(x => x.Energi_Aktif_Wh) ?? 0m,
                    activePanels = validData.Count,
                    avgPowerFactor = validData.Count > 0 ? validData.Average(x => x.Cos_Phi) ?? 0m : 0m,
                    timestamp = DateTime.Now.ToString("dd/MM/yyyy, HH:mm:ss")
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // DATA VERSION (deteksi perubahan data realtime)
        // ============================================
        // Endpoint ringan: hanya mengembalikan "token perubahan", BUKAN payload data.
        // Halaman Panel Monitoring, Charts, Usage Statistics, Anomaly Logs dan Details
        // memanggil endpoint ini secara berkala; UI hanya melakukan refresh ketika
        // token berubah, sehingga tampilan benar-benar mengikuti data realtime dan
        // tidak lagi bergantung pada interval tetap 5/10 detik.
        [HttpGet("data-version")]
        [AllowAnonymous] // Token perubahan saja (read-only), dipakai juga halaman publik
        public async Task<IActionResult> GetDataVersion()
        {
            const string cacheKey = "DataVersion_Latest";
            try
            {
                // Diparse per detik agar banyak tab/polling tetap murah.
                if (_cache.TryGetValue(cacheKey, out object cachedVersion))
                {
                    return Ok(cachedVersion);
                }

                // Data pengukuran terbaru.
                // max(Id)  -> PK seek, naik setiap ada baris baru (termasuk bulk insert
                //             yang ReceivedTime-nya bisa sama/lebih tua).
                // max(ReceivedTime) -> memakai index IX_KWHData_ReceivedTime.
                var kwhMaxId = await _context.KWH_Monitoring.AsNoTracking()
                    .MaxAsync(x => (long?)x.Id) ?? 0;
                var kwhLatest = await _context.KWH_Monitoring.AsNoTracking()
                    .MaxAsync(x => (DateTime?)x.Waktu_Server);
                var kwhVersion = string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}", kwhMaxId, kwhLatest?.Ticks ?? 0);

                // Status relay terakhir (perubahan ON/OFF dari device maupun user).
                var relayMaxId = await _context.RelayControls.AsNoTracking()
                    .MaxAsync(x => (long?)x.Id) ?? 0;
                var relayLatest = await _context.RelayControls.AsNoTracking()
                    .OrderByDescending(x => x.ReceivedTime)
                    .Select(x => (DateTime?)x.ReceivedTime)
                    .FirstOrDefaultAsync();
                var relayVersion = string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}", relayMaxId, relayLatest?.Ticks ?? 0);

                // Agregasi energi. Daily/Hourly relatif kecil sehingga MAX aman.
                // Token juga memuat token KWH karena agregasi dihitung ulang dari data
                // pengukuran - halaman Usage Statistics ikut ter-update begitu ada data
                // pengukuran baru, walau agregator eksternal belum memperbarui CalculatedAt.
                string energyVersion;
                try
                {
                    var daily = await _context.DailyEnergy.AsNoTracking()
                        .MaxAsync(x => (DateTime?)x.CalculatedAt);
                    var hourly = await _context.HourlyEnergy.AsNoTracking()
                        .MaxAsync(x => (DateTime?)x.CalculatedAt);
                    energyVersion = string.Format(CultureInfo.InvariantCulture,
                        "{0}|{1}|{2}", daily?.Ticks ?? 0, hourly?.Ticks ?? 0, kwhVersion);
                }
                catch
                {
                    // Tabel agregat belum ada/sementara bermasalah - pakai token KWH.
                    energyVersion = kwhVersion;
                }

                // Log anomali:
                //  - count + max(Id)  -> insert / delete / clear-all
                //  - event terbaru    -> deteksi baru (DetectedTime), acknowledge,
                //                        maupun resolve (nilai terbesar dari ketiganya)
                var anomalyCount = await _context.AnomalyLogs.AsNoTracking().CountAsync();
                var anomalyMaxId = await _context.AnomalyLogs.AsNoTracking().MaxAsync(x => (long?)x.Id) ?? 0;
                var anomalyLastEvent = await _context.AnomalyLogs.AsNoTracking()
                    .MaxAsync(x => (DateTime?)(x.AcknowledgedTime ?? x.ResolvedTime ?? x.DetectedTime));
                var anomalyVersion = string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1}|{2}", anomalyCount, anomalyMaxId, anomalyLastEvent?.Ticks ?? 0);

                var payload = new
                {
                    success = true,
                    versions = new
                    {
                        kwh = kwhVersion,
                        relay = relayVersion,
                        energy = energyVersion,
                        anomaly = anomalyVersion
                    },
                    serverTime = DateTime.Now.ToString("HH:mm:ss")
                };

                _cache.Set(cacheKey, payload, TimeSpan.FromSeconds(1));
                return Ok(payload);
            }
            catch (Exception ex)
            {
                return SafeError(ex, "DataVersion");
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
                // Gunakan local server time agar konsisten dengan listener (DateTime.Now)
                var serverNow = DateTime.Now;
                var serverToday = DateTime.Today;

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
                    .Select(g => new { Hour = g.Key, EnergyKWh = g.Sum(x => x.EnergyKWh), UpdatedAt = g.Max(x => x.CalculatedAt) })
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
                    .Select(g => new { Day = g.Key, EnergyKWh = g.Sum(x => x.EnergyKWh), UpdatedAt = g.Max(x => x.CalculatedAt) })
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
                    .Select(g => new { Month = g.Key, EnergyKWh = g.Sum(x => x.EnergyKWh), UpdatedAt = g.Max(x => x.CalculatedAt) })
                    .ToListAsync();

                var latestAggregateAt = hourlyRecords.Select(x => (DateTime?)x.UpdatedAt)
                    .Concat(dailyRecords.Select(x => (DateTime?)x.UpdatedAt))
                    .Concat(monthlyRecords.Select(x => (DateTime?)x.UpdatedAt))
                    .Max();

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
                    // Data jam berjalan sudah ada di HourlyEnergy karena listener flush tiap 30 detik
                    var currentHourIndex = serverNow.Hour;
                    currentHourLabel = string.Format("{0:D2}:00", currentHourIndex);
                    var currentHourStart = new DateTime(serverNow.Year, serverNow.Month, serverNow.Day, serverNow.Hour, 0, 0);
                    secondsToNextHour = (int)(currentHourStart.AddHours(1) - serverNow).TotalSeconds;

                    // Ambil nilai current hour dari HourlyEnergy untuk realtimeKWh
                    if (currentHourIndex >= 0 && currentHourIndex < hourlyData.Count)
                    {
                        realtimeKWh = hourlyData[currentHourIndex].energy;
                    }

                    // Total hari ini dari HourlyEnergy
                    todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);

                    // Total bulan ini = HourlyEnergy hari ini + DailyEnergy hari-hari sebelumnya di bulan ini
                    var previousDaysTotal = await _context.DailyEnergy
                        .Where(x => x.Date >= monthStart && x.Date < serverToday)
                        .SumAsync(x => x.EnergyKWh);
                    monthKWh = Math.Round(todayKWh + previousDaysTotal, 2);

                    // Total tahun ini = HourlyEnergy hari ini + DailyEnergy bulan ini sebelum hari ini + MonthlyEnergy bulan-bulan sebelumnya
                    var currentMonthUpToYesterday = await _context.DailyEnergy
                        .Where(x => x.Date >= monthStart && x.Date < serverToday)
                        .SumAsync(x => x.EnergyKWh);
                    var previousMonthsTotal = await _context.MonthlyEnergy
                        .Where(x => x.Year == startDate.Year && x.Month < startDate.Month)
                        .SumAsync(x => x.EnergyKWh);
                    yearKWh = Math.Round(todayKWh + currentMonthUpToYesterday + previousMonthsTotal, 2);

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

                // --- Financial calculations (aggregate) ---
                // Get a representative device settings for financial calculations (use first device or default)
                var allDevSettings = await _deviceSettingsService.GetAllEffectiveAsync();
                var representativeSettings = allDevSettings.Values.FirstOrDefault() ?? new DeviceSettings();

                var hourlyKwhByHour = new decimal[24];
                for (int i = 0; i < hourlyData.Count && i < 24; i++)
                    hourlyKwhByHour[i] = hourlyData[i].energy;

                var wbpResult = CalculateWbpLwbp(hourlyKwhByHour, representativeSettings);
                var wasteResult = CalculateWaste(hourlyKwhByHour, representativeSettings);

                // Budget vs Actual
                decimal budgetKWh = representativeSettings.BudgetKWh;
                decimal budgetVariance = budgetKWh > 0 ? Math.Round(monthKWh - budgetKWh, 2) : 0;
                decimal budgetVariancePct = budgetKWh > 0 ? Math.Round((monthKWh - budgetKWh) / budgetKWh * 100, 1) : 0;
                decimal budgetCost = budgetKWh > 0 ? Math.Round(budgetKWh * tariffPerKWh, 0) : 0;
                decimal actualCost = Math.Round(monthKWh * tariffPerKWh, 0);

                // Period comparison (MoM, YoY)
                var lastMonth = startDate.AddMonths(-1);
                var lastMonthKWh = await _context.MonthlyEnergy
                    .Where(x => x.Year == lastMonth.Year && x.Month == lastMonth.Month)
                    .SumAsync(x => x.EnergyKWh);
                lastMonthKWh = Math.Round(lastMonthKWh, 2);
                var momChange = Math.Round(monthKWh - lastMonthKWh, 2);
                var momChangePercent = lastMonthKWh > 0 ? Math.Round((monthKWh - lastMonthKWh) / lastMonthKWh * 100, 1) : (monthKWh > 0 ? 100m : 0m);

                var lastYearSameMonthKWh = await _context.MonthlyEnergy
                    .Where(x => x.Year == (startDate.Year - 1) && x.Month == startDate.Month)
                    .SumAsync(x => x.EnergyKWh);
                lastYearSameMonthKWh = Math.Round(lastYearSameMonthKWh, 2);
                var yoyChange = Math.Round(monthKWh - lastYearSameMonthKWh, 2);
                var yoyChangePercent = lastYearSameMonthKWh > 0 ? Math.Round((monthKWh - lastYearSameMonthKWh) / lastYearSameMonthKWh * 100, 1) : (monthKWh > 0 ? 100m : 0m);

                // Bill projection
                var dayOfMonth = serverNow.Day;
                var daysInMonthProj = DateTime.DaysInMonth(serverNow.Year, serverNow.Month);
                var projectedMonthKWh = dayOfMonth > 0 ? Math.Round(monthKWh / dayOfMonth * daysInMonthProj, 2) : monthKWh;
                var projectedCost = Math.Round(projectedMonthKWh * tariffPerKWh, 0);
                var daysRemaining = daysInMonthProj - dayOfMonth;

                // Anomaly cost impact
                var monthStartForAnomaly = new DateTime(startDate.Year, startDate.Month, 1);
                var overloadAnomalies = await _context.AnomalyLogs
                    .Where(x => x.DetectedTime >= monthStartForAnomaly && x.DetectedTime < startDate.AddMonths(1))
                    .Where(x => x.AnomalyType == "OVERLOAD")
                    .OrderByDescending(x => x.Deviation)
                    .Take(20).ToListAsync();
                decimal anomalyExcessKWh = 0;
                foreach (var a in overloadAnomalies) { anomalyExcessKWh += Math.Max(a.PowerValue - a.ThresholdValue, 0) * 0.25m / 1000m; }
                anomalyExcessKWh = Math.Round(anomalyExcessKWh, 2);
                var anomalyCostImpact = Math.Round(anomalyExcessKWh * tariffPerKWh, 0);
                var top5Anomalies = overloadAnomalies.Take(5).Select(a => new { a.DeviceKey, a.AnomalyType, a.PowerValue, a.ThresholdValue, a.Deviation, a.Severity, a.DetectedTime, estimatedCost = Math.Round(Math.Max(a.PowerValue - a.ThresholdValue, 0) * 0.25m / 1000m * tariffPerKWh, 0) }).ToList();

                // Load factor
                decimal loadFactor = 0; string loadFactorStatus = "N/A";
                var maxCap = representativeSettings.MaxCapacity;
                if (maxCap > 0 && dayOfMonth > 0) {
                    var opHours = dayOfMonth * 24;
                    loadFactor = Math.Min(Math.Round(monthKWh / (maxCap / 1000m * opHours) * 100, 1), 100m);
                    loadFactorStatus = loadFactor < 30 ? "Under-utilized" : loadFactor <= 80 ? "Optimal" : "High Risk";
                }

                // Unit economics
                var surfaceArea = representativeSettings.SurfaceArea;
                var costPerM2 = surfaceArea > 0 ? Math.Round(actualCost / surfaceArea, 0) : 0;
                var costPerHour = dayOfMonth > 0 ? Math.Round(actualCost / (dayOfMonth * 24), 0) : 0;

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
                    latestAggregateAt,
                    dataVersion = latestAggregateAt.HasValue ? latestAggregateAt.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "0",
                    serverTime = serverNow,
                    serverDate = serverToday.ToString("yyyy-MM-dd"),
                    serverHour = serverNow.Hour,
                    serverDay = serverNow.Day,
                    serverMonth = serverNow.Month,

                    // Financial: WBP/LWBP
                    wbpLwbp = new
                    {
                        configured = wbpResult.configured,
                        wbpKWh = wbpResult.wbpKWh, lwbpKWh = wbpResult.lwbpKWh,
                        wbpCost = wbpResult.wbpCost, lwbpCost = wbpResult.lwbpCost,
                        totalCostWBP = wbpResult.totalCostWBP, wbpRatio = wbpResult.wbpRatio,
                        tariffWBP = wbpResult.tariffWBP, tariffLWBP = wbpResult.tariffLWBP,
                        wbpStart = representativeSettings.WbpStartHour, wbpEnd = representativeSettings.WbpEndHour
                    },
                    // Financial: Waste detection
                    waste = new
                    {
                        configured = wasteResult.configured,
                        wasteKWh = wasteResult.wasteKWh, wasteCost = wasteResult.wasteCost,
                        wastePercent = wasteResult.wastePercent,
                        downtimeStart = wasteResult.dtStart, downtimeEnd = wasteResult.dtEnd
                    },
                    // Financial: Budget vs Actual
                    budget = new
                    {
                        configured = budgetKWh > 0,
                        budgetKWh, actualKWh = monthKWh, variance = budgetVariance, variancePercent = budgetVariancePct,
                        budgetCost, actualCost
                    },
                    // Financial: Period comparison
                    periodComparison = new
                    {
                        lastMonthKWh, momChange, momChangePercent,
                        lastYearSameMonthKWh, yoyChange, yoyChangePercent
                    },
                    // Financial: Bill projection
                    billProjection = new
                    {
                        projectedMonthKWh, projectedCost, daysElapsed = dayOfMonth, daysRemaining
                    },
                    // Financial: Anomaly cost impact
                    anomalyCostImpact = new
                    {
                        totalAnomalies = overloadAnomalies.Count, estimatedExcessKWh = anomalyExcessKWh,
                        estimatedCostImpact = anomalyCostImpact, topAnomalies = top5Anomalies
                    },
                    // Financial: Load factor
                    loadFactorInfo = new
                    {
                        configured = maxCap > 0, loadFactor, maxCapacity = maxCap, status = loadFactorStatus
                    },
                    // Financial: Unit economics
                    unitEconomics = new
                    {
                        configured = surfaceArea > 0, costPerM2, costPerHour, surfaceArea
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // USAGE STATISTICS BATCH (ALL DEVICES)
        // ============================================
        [HttpPost("usage-statistics/batch")]
        public async Task<IActionResult> GetUsageStatisticsBatch([FromBody] UsageStatisticsBatchRequest filter)
        {
            var batchTimer = Stopwatch.StartNew();
            long settingsQueryMilliseconds = 0;
            long hourlyQueryMilliseconds = 0;
            long dailyQueryMilliseconds = 0;
            long monthlyQueryMilliseconds = 0;
            long anomalyQueryMilliseconds = 0;
            var hourlyGroupCount = 0;
            var dailyGroupCount = 0;
            var monthlyGroupCount = 0;
            var anomalyRowCount = 0;
            try
            {
                var serverNow = DateTime.Now;
                var serverToday = DateTime.Today;
                DateTime startDate;
                if (!string.IsNullOrWhiteSpace(filter?.StartDate) &&
                    DateTime.TryParse(filter.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    startDate = parsedDate.Date;
                else
                    startDate = serverToday;

                var singleDeviceKey = string.IsNullOrWhiteSpace(filter?.DeviceKey) ? null : filter.DeviceKey.Trim();
                var requestedDeviceKeys = filter?.DeviceKeys ?? new List<string>();
                if (singleDeviceKey != null) requestedDeviceKeys = new List<string> { singleDeviceKey };
                var deviceKeys = requestedDeviceKeys
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (deviceKeys.Any(x => x.Length > 20))
                    return BadRequest(new { success = false, error = "Daftar perangkat tidak valid." });

                var monthStart = new DateTime(startDate.Year, startDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var isToday = startDate.Date == serverToday;
                var settingsQueryTimer = Stopwatch.StartNew();
                var settings = singleDeviceKey == null
                    ? await _deviceSettingsService.GetAllEffectiveAsync()
                    : new Dictionary<string, DeviceSettings>(StringComparer.Ordinal)
                    {
                        { singleDeviceKey, await _deviceSettingsService.GetEffectiveAsync(singleDeviceKey) }
                    };
                settingsQueryTimer.Stop();
                settingsQueryMilliseconds = settingsQueryTimer.ElapsedMilliseconds;

                // Cache hits are permitted only against the short-lived data-version token
                // produced by /data-version and the latest effective settings timestamp.
                string energyVersionToken = null;
                string anomalyVersionToken = null;
                if (_cache.TryGetValue("DataVersion_Latest", out object latestVersionPayload) && latestVersionPayload != null)
                {
                    var versionObject = JObject.FromObject(latestVersionPayload)["versions"];
                    energyVersionToken = versionObject?["energy"]?.ToString();
                    anomalyVersionToken = versionObject?["anomaly"]?.ToString();
                }
                // Include every effective setting version. A single max timestamp can stay
                // unchanged when a device with an older timestamp is edited.
                var settingsVersion = string.Join(",", settings
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => x.Key + ":" + x.Value.UpdatedAt.Ticks.ToString(CultureInfo.InvariantCulture)));
                string batchCacheKey = null;
                if (!string.IsNullOrWhiteSpace(energyVersionToken) && !string.IsNullOrWhiteSpace(anomalyVersionToken))
                {
                    var cacheIdentity = string.Join("|", new[]
                    {
                        startDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                        string.Join(",", deviceKeys),
                        energyVersionToken,
                        anomalyVersionToken,
                        settingsVersion
                    });
                    using (var sha256 = SHA256.Create())
                    {
                        batchCacheKey = "UsageStatisticsBatch:" + Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(cacheIdentity)))
                            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                    }
                    if (_cache.TryGetValue(batchCacheKey, out object cachedBatch))
                    {
                        _logger.LogInformation("Usage statistics batch cache hit for {DeviceCount} devices in {ElapsedMilliseconds} ms (settings query {SettingsQueryMilliseconds} ms).",
                            deviceKeys.Count, batchTimer.ElapsedMilliseconds, settingsQueryMilliseconds);
                        return Ok(cachedBatch);
                    }
                }

                // Each query returns grouped, narrow projections. Query count stays bounded as
                // device count grows; the browser no longer starts one HTTP request per device.
                var hourlyQuery = _context.HourlyEnergy.AsNoTracking()
                    .Where(x => x.Hour >= startDate && x.Hour < startDate.AddDays(1));
                if (singleDeviceKey != null) hourlyQuery = hourlyQuery.Where(x => x.DeviceKey == singleDeviceKey);
                var hourlyQueryTimer = Stopwatch.StartNew();
                var hourlyRecords = await hourlyQuery
                    .GroupBy(x => new { x.DeviceKey, Hour = x.Hour.Hour })
                    .Select(g => new { g.Key.DeviceKey, g.Key.Hour, EnergyKWh = g.Sum(x => x.EnergyKWh), UpdatedAt = g.Max(x => x.CalculatedAt) })
                    .ToListAsync();
                hourlyQueryTimer.Stop();
                hourlyQueryMilliseconds = hourlyQueryTimer.ElapsedMilliseconds;
                hourlyGroupCount = hourlyRecords.Count;

                var dailyQuery = _context.DailyEnergy.AsNoTracking()
                    .Where(x => x.Date >= monthStart && x.Date < monthEnd);
                if (singleDeviceKey != null) dailyQuery = dailyQuery.Where(x => x.DeviceKey == singleDeviceKey);
                var dailyQueryTimer = Stopwatch.StartNew();
                var dailyRecords = await dailyQuery
                    .GroupBy(x => new { x.DeviceKey, Day = x.Date.Day })
                    .Select(g => new { g.Key.DeviceKey, g.Key.Day, EnergyKWh = g.Sum(x => x.EnergyKWh), UpdatedAt = g.Max(x => x.CalculatedAt) })
                    .ToListAsync();
                dailyQueryTimer.Stop();
                dailyQueryMilliseconds = dailyQueryTimer.ElapsedMilliseconds;
                dailyGroupCount = dailyRecords.Count;

                var comparisonYear = startDate.Year - 1;
                var lastMonth = startDate.AddMonths(-1);
                var monthlyQuery = _context.MonthlyEnergy.AsNoTracking()
                    .Where(x => x.Year == startDate.Year ||
                        (x.Year == lastMonth.Year && x.Month == lastMonth.Month) ||
                        (x.Year == comparisonYear && x.Month == startDate.Month));
                if (singleDeviceKey != null) monthlyQuery = monthlyQuery.Where(x => x.DeviceKey == singleDeviceKey);
                var monthlyQueryTimer = Stopwatch.StartNew();
                var monthlyRecords = await monthlyQuery
                    .GroupBy(x => new { x.DeviceKey, x.Year, x.Month })
                    .Select(g => new { g.Key.DeviceKey, g.Key.Year, g.Key.Month, EnergyKWh = g.Sum(x => x.EnergyKWh), UpdatedAt = g.Max(x => x.CalculatedAt) })
                    .ToListAsync();
                monthlyQueryTimer.Stop();
                monthlyQueryMilliseconds = monthlyQueryTimer.ElapsedMilliseconds;
                monthlyGroupCount = monthlyRecords.Count;

                var anomalyStart = new DateTime(startDate.Year, startDate.Month, 1);
                var anomalySql = @"SELECT * FROM (
                        SELECT *, ROW_NUMBER() OVER (PARTITION BY [DeviceKey] ORDER BY [Deviation] DESC) AS [BatchRowNumber]
                        FROM [dbo].[AnomalyLogs]
                        WHERE [DetectedTime] >= {0} AND [DetectedTime] < {1} AND [AnomalyType] = N'OVERLOAD'
                    ) AS [RankedAnomalies]
                    WHERE [BatchRowNumber] <= 20";
                var anomalyQuery = singleDeviceKey == null
                    ? _context.AnomalyLogs.FromSql(anomalySql, anomalyStart, startDate.AddMonths(1))
                    : _context.AnomalyLogs.FromSql(
                        @"SELECT * FROM (
                            SELECT *, ROW_NUMBER() OVER (PARTITION BY [DeviceKey] ORDER BY [Deviation] DESC) AS [BatchRowNumber]
                            FROM [dbo].[AnomalyLogs]
                            WHERE [DetectedTime] >= {0} AND [DetectedTime] < {1} AND [AnomalyType] = N'OVERLOAD' AND [DeviceKey] = {2}
                        ) AS [RankedAnomalies]
                        WHERE [BatchRowNumber] <= 20",
                        anomalyStart, startDate.AddMonths(1), singleDeviceKey);
                var anomalyQueryTimer = Stopwatch.StartNew();
                var anomalyRecords = await anomalyQuery
                    .AsNoTracking()
                    .Select(x => new UsageStatisticsBatchAnomaly
                    {
                        DeviceKey = x.DeviceKey,
                        AnomalyType = x.AnomalyType,
                        PowerValue = x.PowerValue,
                        ThresholdValue = x.ThresholdValue,
                        Deviation = x.Deviation,
                        Severity = x.Severity,
                        DetectedTime = x.DetectedTime
                    })
                    .ToListAsync();
                anomalyQueryTimer.Stop();
                anomalyQueryMilliseconds = anomalyQueryTimer.ElapsedMilliseconds;
                anomalyRowCount = anomalyRecords.Count;

                var allKeys = new HashSet<string>(deviceKeys, StringComparer.Ordinal);
                if (allKeys.Count == 0)
                {
                    foreach (var key in hourlyRecords.Select(x => x.DeviceKey)
                        .Concat(dailyRecords.Select(x => x.DeviceKey))
                        .Concat(monthlyRecords.Select(x => x.DeviceKey))
                        .Concat(anomalyRecords.Select(x => x.DeviceKey))
                        .Concat(settings.Keys))
                    {
                        if (!string.IsNullOrWhiteSpace(key)) allKeys.Add(key);
                    }
                }

                var hourlyByDevice = hourlyRecords.GroupBy(x => x.DeviceKey)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Hour, x => x.EnergyKWh), StringComparer.Ordinal);
                var dailyByDevice = dailyRecords.GroupBy(x => x.DeviceKey)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Day, x => x.EnergyKWh), StringComparer.Ordinal);
                var monthlyByDevice = monthlyRecords.GroupBy(x => x.DeviceKey)
                    .ToDictionary(g => g.Key, g => g.ToDictionary(x => Tuple.Create(x.Year, x.Month), x => x.EnergyKWh), StringComparer.Ordinal);
                var anomaliesByDevice = anomalyRecords.GroupBy(x => x.DeviceKey)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Deviation).Take(20).ToList(), StringComparer.Ordinal);
                var updatedAtByDevice = new Dictionary<string, DateTime>(StringComparer.Ordinal);
                Action<string, DateTime> recordUpdatedAt = (key, value) =>
                {
                    if (string.IsNullOrWhiteSpace(key)) return;
                    if (!updatedAtByDevice.ContainsKey(key) || value > updatedAtByDevice[key]) updatedAtByDevice[key] = value;
                };
                foreach (var row in hourlyRecords) recordUpdatedAt(row.DeviceKey, row.UpdatedAt);
                foreach (var row in dailyRecords) recordUpdatedAt(row.DeviceKey, row.UpdatedAt);
                foreach (var row in monthlyRecords) recordUpdatedAt(row.DeviceKey, row.UpdatedAt);

                var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                var result = new List<object>();
                var noAggregateDeviceKeys = new List<string>();
                var orderedDeviceKeys = deviceKeys.Count > 0
                    ? deviceKeys.Concat(allKeys.Where(key => !deviceKeys.Contains(key, StringComparer.Ordinal))).Distinct(StringComparer.Ordinal).ToList()
                    : allKeys.OrderBy(key => key, StringComparer.Ordinal).ToList();
                foreach (var deviceKey in orderedDeviceKeys)
                {
                    try
                    {
                    DeviceSettings deviceSettings;
                    if (!settings.TryGetValue(deviceKey, out deviceSettings))
                        deviceSettings = new DeviceSettings { DeviceKey = deviceKey };
                    var hourlyMap = hourlyByDevice.ContainsKey(deviceKey) ? hourlyByDevice[deviceKey] : new Dictionary<int, decimal>();
                    var dailyMap = dailyByDevice.ContainsKey(deviceKey) ? dailyByDevice[deviceKey] : new Dictionary<int, decimal>();
                    var monthlyMap = monthlyByDevice.ContainsKey(deviceKey) ? monthlyByDevice[deviceKey] : new Dictionary<Tuple<int, int>, decimal>();

                    var hourlyData = Enumerable.Range(0, 24).Select(h => new
                    {
                        timeLabel = string.Format("{0:D2}:00", h), energy = Math.Round(hourlyMap.ContainsKey(h) ? hourlyMap[h] : 0m, 2), sortKey = h
                    }).ToList();
                    var todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);
                    var monthKWh = Math.Round(dailyMap.Values.Sum(x => Math.Round(x, 2)), 2);
                    var yearKWh = Math.Round(monthlyMap.Where(x => x.Key.Item1 == startDate.Year).Sum(x => Math.Round(x.Value, 2)), 2);
                    decimal realtimeKWh = 0m;
                    var currentHourLabel = "";
                    var secondsToNextHour = 0;
                    if (isToday)
                    {
                        currentHourLabel = string.Format("{0:D2}:00", serverNow.Hour);
                        secondsToNextHour = (int)(new DateTime(serverNow.Year, serverNow.Month, serverNow.Day, serverNow.Hour, 0, 0).AddHours(1) - serverNow).TotalSeconds;
                        realtimeKWh = hourlyData[serverNow.Hour].energy;
                        var previousDaysTotal = dailyMap.Where(x => x.Key < serverToday.Day).Sum(x => x.Value);
                        monthKWh = Math.Round(todayKWh + previousDaysTotal, 2);
                        var previousMonthsTotal = monthlyMap.Where(x => x.Key.Item1 == startDate.Year && x.Key.Item2 < startDate.Month).Sum(x => x.Value);
                        yearKWh = Math.Round(todayKWh + previousDaysTotal + previousMonthsTotal, 2);
                    }

                    var tariff = deviceSettings.TariffPerKWh > 0m ? deviceSettings.TariffPerKWh : 1500m;
                    var estimatedCost = Math.Round(monthKWh * tariff, 2);

                    var hourlyKwhByHour = hourlyData.Select(x => x.energy).ToArray();
                    var wbp = CalculateWbpLwbp(hourlyKwhByHour, deviceSettings);
                    var waste = CalculateWaste(hourlyKwhByHour, deviceSettings);
                    var budgetKWh = deviceSettings.BudgetKWh;
                    var budgetVariance = budgetKWh > 0 ? Math.Round(monthKWh - budgetKWh, 2) : 0m;
                    var budgetVariancePct = budgetKWh > 0 ? Math.Round((monthKWh - budgetKWh) / budgetKWh * 100, 1) : 0m;
                    var budgetCost = budgetKWh > 0 ? Math.Round(budgetKWh * tariff, 0) : 0m;
                    var actualCost = Math.Round(monthKWh * tariff, 0);
                    var lastMonthKWh = monthlyMap.ContainsKey(Tuple.Create(lastMonth.Year, lastMonth.Month)) ? monthlyMap[Tuple.Create(lastMonth.Year, lastMonth.Month)] : 0m;
                    var lastYearSameMonthKWh = monthlyMap.ContainsKey(Tuple.Create(comparisonYear, startDate.Month)) ? monthlyMap[Tuple.Create(comparisonYear, startDate.Month)] : 0m;
                    var momChange = Math.Round(monthKWh - lastMonthKWh, 2);
                    var momChangePercent = lastMonthKWh > 0 ? Math.Round((monthKWh - lastMonthKWh) / lastMonthKWh * 100, 1) : (monthKWh > 0 ? 100m : 0m);
                    var yoyChange = Math.Round(monthKWh - lastYearSameMonthKWh, 2);
                    var yoyChangePercent = lastYearSameMonthKWh > 0 ? Math.Round((monthKWh - lastYearSameMonthKWh) / lastYearSameMonthKWh * 100, 1) : (monthKWh > 0 ? 100m : 0m);
                    var dayOfMonth = serverNow.Day;
                    var daysInCurrentMonth = DateTime.DaysInMonth(serverNow.Year, serverNow.Month);
                    var projectedMonthKWh = dayOfMonth > 0 ? Math.Round(monthKWh / dayOfMonth * daysInCurrentMonth, 2) : monthKWh;
                    var projectedCost = Math.Round(projectedMonthKWh * tariff, 0);
                    var deviceAnomalies = anomaliesByDevice.ContainsKey(deviceKey) ? anomaliesByDevice[deviceKey] : new List<UsageStatisticsBatchAnomaly>();
                    decimal anomalyExcessKWh = 0m;
                    foreach (var anomaly in deviceAnomalies) anomalyExcessKWh += Math.Max(anomaly.PowerValue - anomaly.ThresholdValue, 0m) * 0.25m / 1000m;
                    anomalyExcessKWh = Math.Round(anomalyExcessKWh, 2);
                    var anomalyCostImpact = Math.Round(anomalyExcessKWh * tariff, 0);
                    var topAnomalies = deviceAnomalies.Take(5).Select(a => new { a.DeviceKey, a.AnomalyType, a.PowerValue, a.ThresholdValue, a.Deviation, a.Severity, a.DetectedTime, estimatedCost = Math.Round(Math.Max(a.PowerValue - a.ThresholdValue, 0m) * 0.25m / 1000m * tariff, 0) }).ToList();
                    var maxCapacity = deviceSettings.MaxCapacity;
                    decimal loadFactor = 0m;
                    var loadFactorStatus = "N/A";
                    if (maxCapacity > 0m && dayOfMonth > 0)
                    {
                        loadFactor = Math.Min(Math.Round(monthKWh / (maxCapacity / 1000m * dayOfMonth * 24) * 100, 1), 100m);
                        loadFactorStatus = loadFactor < 30 ? "Under-utilized" : loadFactor <= 80 ? "Optimal" : "High Risk";
                    }
                    var surfaceArea = deviceSettings.SurfaceArea;
                    var costPerM2 = surfaceArea > 0m ? Math.Round(actualCost / surfaceArea, 0) : 0m;
                    var costPerHour = dayOfMonth > 0 ? Math.Round(actualCost / (dayOfMonth * 24), 0) : 0m;
                    var deviceUpdatedAt = updatedAtByDevice.ContainsKey(deviceKey) ? (DateTime?)updatedAtByDevice[deviceKey] : null;
                    var hasPeriodData = hourlyMap.Count > 0 || dailyMap.Count > 0 || monthlyMap.Keys.Any(key => key.Item1 == startDate.Year);
                    if (!hasPeriodData) noAggregateDeviceKeys.Add(deviceKey);
                    result.Add(new
                    {
                        success = true, deviceKey, dataStatus = hasPeriodData ? "available" : "no-aggregate",
                        today = new { total = todayKWh },
                        month = new { total = monthKWh },
                        year = new { total = yearKWh },
                        tariffPerKWh = tariff, estimatedCost, realtimeKWh = Math.Round(realtimeKWh, 4), currentHourLabel, secondsToNextHour,
                        isToday, dataUpdatedAt = deviceUpdatedAt,
                        wbpLwbp = new { configured = wbp.configured, wbpKWh = wbp.wbpKWh, lwbpKWh = wbp.lwbpKWh, wbpCost = wbp.wbpCost, lwbpCost = wbp.lwbpCost, totalCostWBP = wbp.totalCostWBP, wbpRatio = wbp.wbpRatio, tariffWBP = wbp.tariffWBP, tariffLWBP = wbp.tariffLWBP, wbpStart = deviceSettings.WbpStartHour, wbpEnd = deviceSettings.WbpEndHour },
                        waste = new { configured = waste.configured, wasteKWh = waste.wasteKWh, wasteCost = waste.wasteCost, wastePercent = waste.wastePercent, downtimeStart = waste.dtStart, downtimeEnd = waste.dtEnd },
                        budget = new { configured = budgetKWh > 0m, budgetKWh, actualKWh = monthKWh, variance = budgetVariance, variancePercent = budgetVariancePct, budgetCost, actualCost },
                        periodComparison = new { lastMonthKWh, momChange, momChangePercent, lastYearSameMonthKWh, yoyChange, yoyChangePercent },
                        billProjection = new { projectedMonthKWh, projectedCost, daysElapsed = dayOfMonth, daysRemaining = daysInCurrentMonth - dayOfMonth },
                        anomalyCostImpact = new { totalAnomalies = deviceAnomalies.Count, estimatedExcessKWh = anomalyExcessKWh, estimatedCostImpact = anomalyCostImpact, topAnomalies },
                        loadFactorInfo = new { configured = maxCapacity > 0m, loadFactor, maxCapacity, status = loadFactorStatus },
                        unitEconomics = new { configured = surfaceArea > 0m, costPerM2, costPerHour, surfaceArea }
                    });
                    }
                    catch (Exception ex)
                    {
                        // Keep valid device results when one device's settings or calculations
                        // are invalid. Shared database query failures are handled by the outer catch.
                        _logger.LogWarning("Usage statistics calculation failed for one device ({ExceptionType}).", ex.GetType().Name);
                        result.Add(new
                        {
                            success = false,
                            deviceKey,
                            dataStatus = "calculation-error"
                        });
                    }
                }

                var summaryHourlyTotals = hourlyRecords.GroupBy(x => x.Hour)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));
                var summaryDailyTotals = dailyRecords.GroupBy(x => x.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));
                var summaryMonthlyTotals = monthlyRecords.Where(x => x.Year == startDate.Year).GroupBy(x => x.Month)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.EnergyKWh));
                var summaryHourlyData = Enumerable.Range(0, 24).Select(hour =>
                {
                    var energy = summaryHourlyTotals.ContainsKey(hour) ? summaryHourlyTotals[hour] : 0m;
                    return new { timeLabel = string.Format("{0:D2}:00", hour), energy = Math.Round(energy, 2), sortKey = hour };
                }).ToList();
                var summaryDailyData = Enumerable.Range(1, daysInMonth).Select(day =>
                {
                    var energy = summaryDailyTotals.ContainsKey(day) ? summaryDailyTotals[day] : 0m;
                    return new { dateLabel = string.Format("{0}/{1}/{2}", day, startDate.Month, startDate.Year), energy = Math.Round(energy, 2), sortKey = day };
                }).ToList();
                var summaryMonthlyData = Enumerable.Range(1, 12).Select(month =>
                {
                    var energy = summaryMonthlyTotals.ContainsKey(month) ? summaryMonthlyTotals[month] : 0m;
                    return new { monthName = GetMonthName(month), energy = Math.Round(energy, 2), sortKey = month };
                }).ToList();
                var summaryTodayKWh = Math.Round(summaryHourlyData.Sum(x => x.energy), 2);
                var summaryMonthKWh = Math.Round(summaryDailyData.Sum(x => x.energy), 2);
                var summaryYearKWh = Math.Round(summaryMonthlyData.Sum(x => x.energy), 2);
                var summaryRealtimeKWh = 0m;
                var summaryCurrentHour = "";
                var summarySecondsToNextHour = 0;
                if (isToday)
                {
                    summaryRealtimeKWh = summaryHourlyData[serverNow.Hour].energy;
                    summaryCurrentHour = string.Format("{0:D2}:00", serverNow.Hour);
                    summarySecondsToNextHour = (int)(new DateTime(serverNow.Year, serverNow.Month, serverNow.Day, serverNow.Hour, 0, 0).AddHours(1) - serverNow).TotalSeconds;
                    var previousDaysTotal = summaryDailyTotals.Where(x => x.Key < serverToday.Day).Sum(x => x.Value);
                    summaryMonthKWh = Math.Round(summaryTodayKWh + previousDaysTotal, 2);
                    var previousMonthsTotal = summaryMonthlyTotals.Where(x => x.Key < startDate.Month).Sum(x => x.Value);
                    summaryYearKWh = Math.Round(summaryTodayKWh + previousDaysTotal + previousMonthsTotal, 2);
                    var todayIndex = summaryDailyData.FindIndex(x => x.sortKey == serverToday.Day);
                    if (todayIndex >= 0) summaryDailyData[todayIndex] = new { dateLabel = string.Format("{0}/{1}/{2}", serverToday.Day, startDate.Month, startDate.Year), energy = summaryTodayKWh, sortKey = serverToday.Day };
                    var monthIndex = summaryMonthlyData.FindIndex(x => x.sortKey == serverNow.Month);
                    if (monthIndex >= 0) summaryMonthlyData[monthIndex] = new { monthName = GetMonthName(serverNow.Month), energy = summaryMonthKWh, sortKey = serverNow.Month };
                }
                var summaryPeakHour = summaryHourlyData.Max(x => x.energy);
                var summaryPeakDay = summaryDailyData.Max(x => x.energy);
                var summaryPeakMonth = summaryMonthlyData.Max(x => x.energy);
                var summary = new
                {
                    totalToday = summaryTodayKWh,
                    totalThisMonth = summaryMonthKWh,
                    totalThisYear = summaryYearKWh,
                    peakHour = summaryPeakHour,
                    peakHourTime = string.Format("{0:D2}:00", summaryHourlyData.First(x => x.energy == summaryPeakHour).sortKey),
                    peakDay = summaryPeakDay,
                    peakDayDate = string.Format("{0}/{1}", summaryDailyData.First(x => x.energy == summaryPeakDay).sortKey, startDate.Month),
                    peakMonth = summaryPeakMonth,
                    peakMonthName = summaryMonthlyData.First(x => x.energy == summaryPeakMonth).monthName,
                    hourlyData = summaryHourlyData,
                    dailyData = summaryDailyData,
                    monthlyData = summaryMonthlyData,
                    realtimeKWh = Math.Round(summaryRealtimeKWh, 4),
                    currentHourLabel = summaryCurrentHour,
                    secondsToNextHour = summarySecondsToNextHour,
                    isToday,
                    serverDate = serverToday.ToString("yyyy-MM-dd"),
                    serverHour = serverNow.Hour,
                    serverDay = serverNow.Day,
                    serverMonth = serverNow.Month
                };

                var latestAggregateAt = updatedAtByDevice.Count == 0 ? (DateTime?)null : updatedAtByDevice.Values.Max();
                var response = new
                {
                    success = true,
                    devices = result,
                    noAggregateDeviceKeys,
                    summary,
                    serverTime = serverNow,
                    serverDate = serverToday.ToString("yyyy-MM-dd"),
                    latestAggregateAt,
                    dataVersion = batchCacheKey != null
                        ? energyVersionToken + "|" + anomalyVersionToken + "|" + settingsVersion
                        : (latestAggregateAt.HasValue ? latestAggregateAt.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "0"),
                    deviceCount = result.Count
                };
                if (batchCacheKey != null) _cache.Set(batchCacheKey, response, TimeSpan.FromSeconds(1));
                _logger.LogInformation(
                    "Usage statistics batch completed for {DeviceCount} devices in {ElapsedMilliseconds} ms (settings {SettingsQueryMilliseconds} ms; hourly {HourlyQueryMilliseconds} ms/{HourlyGroupCount} groups; daily {DailyQueryMilliseconds} ms/{DailyGroupCount} groups; monthly {MonthlyQueryMilliseconds} ms/{MonthlyGroupCount} groups; anomalies {AnomalyQueryMilliseconds} ms/{AnomalyRowCount} rows; cache eligible: {CacheEligible}).",
                    result.Count, batchTimer.ElapsedMilliseconds, settingsQueryMilliseconds,
                    hourlyQueryMilliseconds, hourlyGroupCount, dailyQueryMilliseconds, dailyGroupCount,
                    monthlyQueryMilliseconds, monthlyGroupCount, anomalyQueryMilliseconds, anomalyRowCount,
                    batchCacheKey != null);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Usage statistics batch failed after {ElapsedMilliseconds} ms ({ExceptionType}); partial query timings: settings {SettingsQueryMilliseconds} ms, hourly {HourlyQueryMilliseconds} ms, daily {DailyQueryMilliseconds} ms, monthly {MonthlyQueryMilliseconds} ms, anomalies {AnomalyQueryMilliseconds} ms.",
                    batchTimer.ElapsedMilliseconds, ex.GetType().Name, settingsQueryMilliseconds,
                    hourlyQueryMilliseconds, dailyQueryMilliseconds, monthlyQueryMilliseconds, anomalyQueryMilliseconds);
                return SafeError(ex, "UsageStatisticsBatch");
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
                var serverNow = DateTime.Now;
                var serverToday = DateTime.Today;

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
                var hourlyRows = await _context.HourlyEnergy
                    .AsNoTracking()
                    .Where(x => x.DeviceKey == deviceKey && x.Hour >= dayStart && x.Hour < dayEnd)
                    .Select(x => new { x.Hour, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();
                var hourlyRecords = hourlyRows.ToDictionary(x => x.Hour.Hour, x => x.EnergyKWh);

                var hourlyData = Enumerable.Range(0, 24).Select(h => new
                {
                    timeLabel = string.Format("{0:D2}:00", h),
                    energy = Math.Round(hourlyRecords.GetValueOrDefault(h), 2),
                    sortKey = h
                }).ToList();

                // Daily
                var dailyRows = await _context.DailyEnergy
                    .AsNoTracking()
                    .Where(x => x.DeviceKey == deviceKey && x.Date >= monthStart && x.Date < monthEnd)
                    .Select(x => new { x.Date, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();
                var dailyRecords = dailyRows.ToDictionary(x => x.Date.Day, x => x.EnergyKWh);

                var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                var dailyData = Enumerable.Range(1, daysInMonth).Select(d => new
                {
                    dateLabel = string.Format("{0}/{1}/{2}", d, startDate.Month, startDate.Year),
                    energy = Math.Round(dailyRecords.GetValueOrDefault(d), 2),
                    sortKey = d
                }).ToList();

                // Monthly
                var monthlyRows = await _context.MonthlyEnergy
                    .AsNoTracking()
                    .Where(x => x.DeviceKey == deviceKey && x.Year == startDate.Year)
                    .Select(x => new { x.Month, x.EnergyKWh, x.CalculatedAt })
                    .ToListAsync();
                var monthlyRecords = monthlyRows.ToDictionary(x => x.Month, x => x.EnergyKWh);

                var monthlyData = Enumerable.Range(1, 12).Select(m => new
                {
                    monthName = GetMonthName(m),
                    energy = Math.Round(monthlyRecords.GetValueOrDefault(m), 2),
                    sortKey = m
                }).ToList();

                // Totals
                var todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);
                var monthKWh = Math.Round(dailyData.Sum(x => x.energy), 2);
                var yearKWh = Math.Round(monthlyData.Sum(x => x.energy), 2);

                // All-time total
                var allTimeKWh = Math.Round(await _context.YearlyEnergy
                    .AsNoTracking()
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

                var tariffPerKWh = await GetTariffPerKWh(deviceKey);
                var estimatedCost = Math.Round(monthKWh * tariffPerKWh, 2);
                var updateTimes = hourlyRows.Select(x => x.CalculatedAt)
                    .Concat(dailyRows.Select(x => x.CalculatedAt))
                    .Concat(monthlyRows.Select(x => x.CalculatedAt))
                    .ToList();
                DateTime? dataUpdatedAt = updateTimes.Count > 0 ? (DateTime?)updateTimes.Max() : null;

                var isTodayDevice = startDate.Date == serverToday.Date;
                decimal realtimeKWh = 0;
                int secondsToNextHour = 0;
                string currentHourLabel = "";

                if (isTodayDevice)
                {
                    var currentHourIndex = serverNow.Hour;
                    currentHourLabel = string.Format("{0:D2}:00", currentHourIndex);
                    var currentHourStart = new DateTime(serverNow.Year, serverNow.Month, serverNow.Day, serverNow.Hour, 0, 0);
                    secondsToNextHour = (int)(currentHourStart.AddHours(1) - serverNow).TotalSeconds;

                    // Gunakan HourlyEnergy untuk jam berjalan (listener flush tiap 30 detik)
                    if (currentHourIndex >= 0 && currentHourIndex < hourlyData.Count)
                    {
                        realtimeKWh = hourlyData[currentHourIndex].energy;
                    }

                    todayKWh = Math.Round(hourlyData.Sum(x => x.energy), 2);

                    // Bulan ini = hari ini dari HourlyEnergy + hari sebelumnya dari DailyEnergy
                    var previousDaysTotal = await _context.DailyEnergy
                        .AsNoTracking()
                        .Where(x => x.DeviceKey == deviceKey && x.Date >= monthStart && x.Date < serverToday)
                        .SumAsync(x => x.EnergyKWh);
                    monthKWh = Math.Round(todayKWh + previousDaysTotal, 2);

                    // Tahun ini = hari ini + hari sebelumnya di bulan ini + bulan-bulan sebelumnya dari MonthlyEnergy
                    // Gunakan hasil query bulan yang sama agar tidak mengirim query identik dua kali.
                    var currentMonthUpToYesterday = previousDaysTotal;
                    var previousMonthsTotal = await _context.MonthlyEnergy
                        .Where(x => x.DeviceKey == deviceKey && x.Year == startDate.Year && x.Month < startDate.Month)
                        .SumAsync(x => x.EnergyKWh);
                    yearKWh = Math.Round(todayKWh + currentMonthUpToYesterday + previousMonthsTotal, 2);

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
                    estimatedCost = Math.Round(monthKWh * tariffPerKWh, 2); // tariffPerKWh already per-device from above
                }

                // --- Financial calculations (per-device) ---
                var devSettings = await _deviceSettingsService.GetEffectiveAsync(deviceKey);

                var hourlyKwhByHour = new decimal[24];
                for (int i = 0; i < hourlyData.Count && i < 24; i++)
                    hourlyKwhByHour[i] = hourlyData[i].energy;

                var wbpResult = CalculateWbpLwbp(hourlyKwhByHour, devSettings);
                var wasteResult = CalculateWaste(hourlyKwhByHour, devSettings);

                // Budget vs Actual
                decimal budgetKWh = devSettings.BudgetKWh;
                decimal budgetVariance = budgetKWh > 0 ? Math.Round(monthKWh - budgetKWh, 2) : 0;
                decimal budgetVariancePct = budgetKWh > 0 ? Math.Round((monthKWh - budgetKWh) / budgetKWh * 100, 1) : 0;
                decimal budgetCost = budgetKWh > 0 ? Math.Round(budgetKWh * tariffPerKWh, 0) : 0;
                decimal actualCost = Math.Round(monthKWh * tariffPerKWh, 0);

                // Period comparison (MoM, YoY)
                var lastMonth = startDate.AddMonths(-1);
                var lastMonthKWh = await _context.MonthlyEnergy
                    .Where(x => x.DeviceKey == deviceKey && x.Year == lastMonth.Year && x.Month == lastMonth.Month)
                    .SumAsync(x => x.EnergyKWh);
                lastMonthKWh = Math.Round(lastMonthKWh, 2);
                var momChange = Math.Round(monthKWh - lastMonthKWh, 2);
                var momChangePercent = lastMonthKWh > 0 ? Math.Round((monthKWh - lastMonthKWh) / lastMonthKWh * 100, 1) : (monthKWh > 0 ? 100m : 0m);

                var lastYearSameMonthKWh = await _context.MonthlyEnergy
                    .Where(x => x.DeviceKey == deviceKey && x.Year == (startDate.Year - 1) && x.Month == startDate.Month)
                    .SumAsync(x => x.EnergyKWh);
                lastYearSameMonthKWh = Math.Round(lastYearSameMonthKWh, 2);
                var yoyChange = Math.Round(monthKWh - lastYearSameMonthKWh, 2);
                var yoyChangePercent = lastYearSameMonthKWh > 0 ? Math.Round((monthKWh - lastYearSameMonthKWh) / lastYearSameMonthKWh * 100, 1) : (monthKWh > 0 ? 100m : 0m);

                // Bill projection
                var dayOfMonth = serverNow.Day;
                var daysInMonthProj = DateTime.DaysInMonth(serverNow.Year, serverNow.Month);
                var projectedMonthKWh = dayOfMonth > 0 ? Math.Round(monthKWh / dayOfMonth * daysInMonthProj, 2) : monthKWh;
                var projectedCost = Math.Round(projectedMonthKWh * tariffPerKWh, 0);
                var daysRemaining = daysInMonthProj - dayOfMonth;

                // Anomaly cost impact
                var monthStartForAnomaly = new DateTime(startDate.Year, startDate.Month, 1);
                var overloadAnomalies = await _context.AnomalyLogs
                    .Where(x => x.DeviceKey == deviceKey && x.DetectedTime >= monthStartForAnomaly && x.DetectedTime < startDate.AddMonths(1))
                    .Where(x => x.AnomalyType == "OVERLOAD")
                    .OrderByDescending(x => x.Deviation)
                    .Take(20).ToListAsync();
                decimal anomalyExcessKWh = 0;
                foreach (var a in overloadAnomalies) { anomalyExcessKWh += Math.Max(a.PowerValue - a.ThresholdValue, 0) * 0.25m / 1000m; }
                anomalyExcessKWh = Math.Round(anomalyExcessKWh, 2);
                var anomalyCostImpact = Math.Round(anomalyExcessKWh * tariffPerKWh, 0);
                var top5Anomalies = overloadAnomalies.Take(5).Select(a => new { a.DeviceKey, a.AnomalyType, a.PowerValue, a.ThresholdValue, a.Deviation, a.Severity, a.DetectedTime, estimatedCost = Math.Round(Math.Max(a.PowerValue - a.ThresholdValue, 0) * 0.25m / 1000m * tariffPerKWh, 0) }).ToList();

                // Load factor
                decimal loadFactor = 0; string loadFactorStatus = "N/A";
                var maxCap = devSettings.MaxCapacity;
                if (maxCap > 0 && dayOfMonth > 0) {
                    var opHours = dayOfMonth * 24;
                    loadFactor = Math.Min(Math.Round(monthKWh / (maxCap / 1000m * opHours) * 100, 1), 100m);
                    loadFactorStatus = loadFactor < 30 ? "Under-utilized" : loadFactor <= 80 ? "Optimal" : "High Risk";
                }

                // Unit economics
                var surfaceArea = devSettings.SurfaceArea;
                var costPerM2 = surfaceArea > 0 ? Math.Round(actualCost / surfaceArea, 0) : 0;
                var costPerHour = dayOfMonth > 0 ? Math.Round(actualCost / (dayOfMonth * 24), 0) : 0;

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
                    dataUpdatedAt,
                    dataVersion = dataUpdatedAt.HasValue ? dataUpdatedAt.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "0",
                    serverDate = serverToday.ToString("yyyy-MM-dd"),
                    serverHour = serverNow.Hour,

                    // Financial: WBP/LWBP
                    wbpLwbp = new
                    {
                        configured = wbpResult.configured,
                        wbpKWh = wbpResult.wbpKWh, lwbpKWh = wbpResult.lwbpKWh,
                        wbpCost = wbpResult.wbpCost, lwbpCost = wbpResult.lwbpCost,
                        totalCostWBP = wbpResult.totalCostWBP, wbpRatio = wbpResult.wbpRatio,
                        tariffWBP = wbpResult.tariffWBP, tariffLWBP = wbpResult.tariffLWBP,
                        wbpStart = devSettings.WbpStartHour, wbpEnd = devSettings.WbpEndHour
                    },
                    // Financial: Waste detection
                    waste = new
                    {
                        configured = wasteResult.configured,
                        wasteKWh = wasteResult.wasteKWh, wasteCost = wasteResult.wasteCost,
                        wastePercent = wasteResult.wastePercent,
                        downtimeStart = wasteResult.dtStart, downtimeEnd = wasteResult.dtEnd
                    },
                    // Financial: Budget vs Actual
                    budget = new
                    {
                        configured = budgetKWh > 0,
                        budgetKWh, actualKWh = monthKWh, variance = budgetVariance, variancePercent = budgetVariancePct,
                        budgetCost, actualCost
                    },
                    // Financial: Period comparison
                    periodComparison = new
                    {
                        lastMonthKWh, momChange, momChangePercent,
                        lastYearSameMonthKWh, yoyChange, yoyChangePercent
                    },
                    // Financial: Bill projection
                    billProjection = new
                    {
                        projectedMonthKWh, projectedCost, daysElapsed = dayOfMonth, daysRemaining
                    },
                    // Financial: Anomaly cost impact
                    anomalyCostImpact = new
                    {
                        totalAnomalies = overloadAnomalies.Count, estimatedExcessKWh = anomalyExcessKWh,
                        estimatedCostImpact = anomalyCostImpact, topAnomalies = top5Anomalies
                    },
                    // Financial: Load factor
                    loadFactorInfo = new
                    {
                        configured = maxCap > 0, loadFactor, maxCapacity = maxCap, status = loadFactorStatus
                    },
                    // Financial: Unit economics
                    unitEconomics = new
                    {
                        configured = surfaceArea > 0, costPerM2, costPerHour, surfaceArea
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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
                        var avgPower = ((prev.Daya_Watt ?? 0m) + (curr.Daya_Watt ?? 0m)) / 2m;
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
                return SafeError(ex);
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
                return SafeError(ex);
            }
        }

        private string GetMonthName(int month)
        {
            if (month < 1 || month > 12) return "";
            var months = new[] { "", "Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Ags", "Sep", "Okt", "Nov", "Des" };
            return months[month];
        }

        // ============================================
        // FINANCIAL CALCULATION HELPERS
        // ============================================
        private (decimal wbpKWh, decimal lwbpKWh, decimal wbpCost, decimal lwbpCost, decimal totalCostWBP, decimal wbpRatio, bool configured, decimal tariffWBP, decimal tariffLWBP) CalculateWbpLwbp(decimal[] hourlyKwhByHour, DeviceSettings ds)
        {
            var tariffWBP = ds.TariffWBP > 0 ? ds.TariffWBP : 0m;
            var tariffLWBP = ds.TariffLWBP > 0 ? ds.TariffLWBP : 0m;
            var flatTariff = ds.TariffPerKWh > 0 ? ds.TariffPerKWh : 1500m;
            var useWbpSplit = tariffWBP > 0 && tariffLWBP > 0;
            var wbpStart = ds.WbpStartHour;
            var wbpEnd = ds.WbpEndHour;

            decimal wbpKWh = 0, lwbpKWh = 0;
            for (int h = 0; h < hourlyKwhByHour.Length && h < 24; h++)
            {
                bool isWbp;
                if (wbpStart < wbpEnd) isWbp = h >= wbpStart && h < wbpEnd;
                else isWbp = h >= wbpStart || h < wbpEnd;

                if (isWbp) wbpKWh += hourlyKwhByHour[h];
                else lwbpKWh += hourlyKwhByHour[h];
            }

            wbpKWh = Math.Round(wbpKWh, 2);
            lwbpKWh = Math.Round(lwbpKWh, 2);
            var total = Math.Round(wbpKWh + lwbpKWh, 2);
            var ratio = total > 0 ? Math.Round(wbpKWh / total * 100, 1) : 0;

            if (!useWbpSplit) return (wbpKWh, lwbpKWh, Math.Round(wbpKWh * flatTariff, 0), Math.Round(lwbpKWh * flatTariff, 0), Math.Round(total * flatTariff, 0), ratio, false, flatTariff, flatTariff);
            return (wbpKWh, lwbpKWh, Math.Round(wbpKWh * tariffWBP, 0), Math.Round(lwbpKWh * tariffLWBP, 0), Math.Round(wbpKWh * tariffWBP + lwbpKWh * tariffLWBP, 0), ratio, true, tariffWBP, tariffLWBP);
        }

        private (decimal wasteKWh, decimal wasteCost, decimal wastePercent, bool configured, string dtStart, string dtEnd) CalculateWaste(decimal[] hourlyKwhByHour, DeviceSettings ds)
        {
            if (!ds.DowntimeEnabled) return (0, 0, 0, false, "", "");
            var startH = ds.DowntimeStart.Hours;
            var endH = ds.DowntimeEnd.Hours;
            var tariff = ds.TariffPerKWh > 0 ? ds.TariffPerKWh : 1500m;

            decimal wasteKWh = 0, totalKWh = 0;
            for (int h = 0; h < hourlyKwhByHour.Length && h < 24; h++)
            {
                bool isDt;
                if (startH < endH) isDt = h >= startH && h < endH;
                else isDt = h >= startH || h < endH;

                totalKWh += hourlyKwhByHour[h];
                if (isDt && hourlyKwhByHour[h] > 0) wasteKWh += hourlyKwhByHour[h];
            }

            wasteKWh = Math.Round(wasteKWh, 2);
            var pct = totalKWh > 0 ? Math.Round(wasteKWh / totalKWh * 100, 1) : 0;
            return (wasteKWh, Math.Round(wasteKWh * tariff, 0), pct, true, string.Format("{0:D2}:00", startH), string.Format("{0:D2}:00", endH));
        }

        // ============================================
        // GET TARIFF PER KWH
        // ============================================
        private async Task<decimal> GetTariffPerKWh(string deviceKey = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(deviceKey))
                {
                    var deviceSettings = await _context.DeviceSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

                    if (deviceSettings != null && deviceSettings.TariffPerKWh > 0)
                        return deviceSettings.TariffPerKWh;
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
        public async Task<IActionResult> GetTariff([FromQuery] string deviceKey)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(deviceKey))
                {
                    var deviceSettings = await _context.DeviceSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

                    if (deviceSettings != null && deviceSettings.TariffPerKWh > 0)
                    {
                        return Ok(new { tariffPerKWh = deviceSettings.TariffPerKWh });
                    }
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
        // ============================================
        [HttpPost("save-tariff")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> SaveTariff([FromBody] Dictionary<string, string> data)
        {
            try
            {
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
                return SafeError(ex);
            }
        }

        // ============================================
        // HISTORY DATA API
        // ============================================
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(string deviceKey, string fromDate, string toDate, int page = 1, int pageSize = 10)
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
                return SafeError(ex);
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
                        waktuDevice = item.Waktu_Device?.ToString("dd/MM/yyyy HH:mm:ss") ?? "-",
                        waktuServer = item.Waktu_Server.ToString("dd/MM/yyyy HH:mm:ss"),
                        voltR = item.Volt_R ?? 0m,
                        voltS = item.Volt_S ?? 0m,
                        voltT = item.Volt_T ?? 0m,
                        ampR = item.Amp_R ?? 0m,
                        ampS = item.Amp_S ?? 0m,
                        ampT = item.Amp_T ?? 0m,
                        cosPhi = item.Cos_Phi ?? 0m,
                        dayaWatt = item.Daya_Watt ?? 0m,
                        totalW1M = item.TotalW1M_Wh ?? 0m,
                        energiAktif = item.Energi_Aktif_Wh ?? 0m,
                        totalEnergy = item.Total_Energy_Wh ?? 0m,
                        frekuensi = item.Frekuensi_Hz ?? 0m,
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
                return SafeError(ex);
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

                // Limit untuk export - maksimal 1000 data untuk mencegah overload
                var data = await query
                    .OrderByDescending(x => x.Waktu_Server)
                    .Take(1000)
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
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.Waktu_Device ?? DateTime.MinValue);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.Waktu_Server);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Volt_R ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Volt_S ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Volt_T ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Amp_R ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Amp_S ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Amp_T ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Cos_Phi ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Daya_Watt ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.TotalW1M_Wh ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Energi_Aktif_Wh ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Total_Energy_Wh ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.Frekuensi_Hz ?? 0m);
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
                        item.Waktu_Device ?? DateTime.MinValue, item.Waktu_Server,
                        item.Volt_R ?? 0m, item.Volt_S ?? 0m, item.Volt_T ?? 0m, item.Amp_R ?? 0m, item.Amp_S ?? 0m, item.Amp_T ?? 0m,
                        item.Cos_Phi ?? 0m, item.Daya_Watt ?? 0m, item.TotalW1M_Wh ?? 0m, item.Energi_Aktif_Wh ?? 0m, item.Total_Energy_Wh ?? 0m, item.Frekuensi_Hz ?? 0m));
                }

                var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(csvBytes, "text/csv", string.Format("KWH_History_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now));
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // HISTORY DATA FROM ARCHIVE TABLE (KWHData_History)
        // Keyset/Seek Pagination — TANPA COUNT(*) untuk efisiensi
        // Menggunakan cursor timestamp agar cepat di tabel ratusan ribu
        // ============================================
        [HttpGet("history-archive")]
        public async Task<IActionResult> GetHistoryArchive(
            [FromQuery] string deviceKey,
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int take = 50,
            [FromQuery] string lastReceivedTime = null,
            [FromQuery] string sort = null,
            [FromQuery] string filter = null,
            [FromQuery] string group = null)
        {
            try
            {
                if (take < 1) take = 10;
                if (take > 500) take = 500;

                var query = _context.KWHData_History.AsQueryable();

                // Apply device key filter
                if (!string.IsNullOrEmpty(deviceKey))
                    query = query.Where(x => x.DeviceKey == deviceKey);

                // Apply date range filter
                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
                    query = query.Where(x => x.ReceivedTime >= parsedFrom);

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedTo))
                    query = query.Where(x => x.ReceivedTime < parsedTo.AddDays(1));

                // KEYSET PAGINATION: gunakan cursor timestamp
                // Lebih cepat dari OFFSET karena tidak perlu scan dan discard ribuan row
                if (!string.IsNullOrEmpty(lastReceivedTime) && DateTime.TryParse(lastReceivedTime, out var lastTime))
                {
                    query = query.Where(x => x.ReceivedTime < lastTime);
                }

                // Apply sorting (default: ReceivedTime DESC untuk seek descending)
                if (!string.IsNullOrEmpty(sort))
                {
                    query = ApplyHistorySort(query, sort);
                }
                else
                {
                    query = query.OrderByDescending(x => x.ReceivedTime);
                }

                // Ambil data LANGSUNG tanpa COUNT(*) — jauh lebih cepat
                var data = await query
                    .Take(take)
                    .ToListAsync();

                var result = new
                {
                    data = data.Select(item => new
                    {
                        historyId = item.HistoryId,
                        originalId = item.OriginalId,
                        deviceKey = item.DeviceKey,
                        deviceId = item.DeviceId,
                        groupName = item.GroupName,
                        terminalTime = item.TerminalTime,
                        receivedTime = item.ReceivedTime,
                        phaseR = item.PhaseR ?? 0m,
                        phaseS = item.PhaseS ?? 0m,
                        phaseT = item.PhaseT ?? 0m,
                        ampereR = item.AmpereR ?? 0m,
                        ampereS = item.AmpereS ?? 0m,
                        ampereT = item.AmpereT ?? 0m,
                        w = item.W ?? 0m,
                        cosPhi = item.CosPhi ?? 0m,
                        f = item.F ?? 0m,
                        aktifPower = item.AktifPower ?? 0m,
                        totalW = item.TotalW ?? 0m,
                        totalW1M = item.TotalW1M ?? 0m,
                        archivedAt = item.ArchivedAt
                    }).ToList(),
                    hasMore = data.Count >= take,
                    pageSize = take
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history archive data");
                return SafeError(ex, "HistoryArchive");
            }
        }

        private IQueryable<KWHDataHistory> ApplyHistorySort(IQueryable<KWHDataHistory> query, string sortJson)
        {
            try
            {
                var sortItems = JsonConvert.DeserializeObject<List<DevExtremeSortItem>>(sortJson);
                if (sortItems == null || sortItems.Count == 0)
                    return query.OrderByDescending(x => x.ReceivedTime);

                bool first = true;
                IOrderedQueryable<KWHDataHistory> orderedQuery = null;

                foreach (var item in sortItems)
                {
                    bool desc = item.Desc;
                    switch (item.Selector?.ToLower())
                    {
                        case "historyid": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.HistoryId) : query.OrderBy(x => x.HistoryId)) : (desc ? orderedQuery.ThenByDescending(x => x.HistoryId) : orderedQuery.ThenBy(x => x.HistoryId)); break;
                        case "originalid": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.OriginalId) : query.OrderBy(x => x.OriginalId)) : (desc ? orderedQuery.ThenByDescending(x => x.OriginalId) : orderedQuery.ThenBy(x => x.OriginalId)); break;
                        case "devicekey": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.DeviceKey) : query.OrderBy(x => x.DeviceKey)) : (desc ? orderedQuery.ThenByDescending(x => x.DeviceKey) : orderedQuery.ThenBy(x => x.DeviceKey)); break;
                        case "deviceid": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.DeviceId) : query.OrderBy(x => x.DeviceId)) : (desc ? orderedQuery.ThenByDescending(x => x.DeviceId) : orderedQuery.ThenBy(x => x.DeviceId)); break;
                        case "groupname": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.GroupName) : query.OrderBy(x => x.GroupName)) : (desc ? orderedQuery.ThenByDescending(x => x.GroupName) : orderedQuery.ThenBy(x => x.GroupName)); break;
                        case "terminaltime": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.TerminalTime) : query.OrderBy(x => x.TerminalTime)) : (desc ? orderedQuery.ThenByDescending(x => x.TerminalTime) : orderedQuery.ThenBy(x => x.TerminalTime)); break;
                        case "receivedtime": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.ReceivedTime) : query.OrderBy(x => x.ReceivedTime)) : (desc ? orderedQuery.ThenByDescending(x => x.ReceivedTime) : orderedQuery.ThenBy(x => x.ReceivedTime)); break;
                        case "phaser": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.PhaseR) : query.OrderBy(x => x.PhaseR)) : (desc ? orderedQuery.ThenByDescending(x => x.PhaseR) : orderedQuery.ThenBy(x => x.PhaseR)); break;
                        case "phases": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.PhaseS) : query.OrderBy(x => x.PhaseS)) : (desc ? orderedQuery.ThenByDescending(x => x.PhaseS) : orderedQuery.ThenBy(x => x.PhaseS)); break;
                        case "phaset": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.PhaseT) : query.OrderBy(x => x.PhaseT)) : (desc ? orderedQuery.ThenByDescending(x => x.PhaseT) : orderedQuery.ThenBy(x => x.PhaseT)); break;
                        case "amperer": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.AmpereR) : query.OrderBy(x => x.AmpereR)) : (desc ? orderedQuery.ThenByDescending(x => x.AmpereR) : orderedQuery.ThenBy(x => x.AmpereR)); break;
                        case "amperes": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.AmpereS) : query.OrderBy(x => x.AmpereS)) : (desc ? orderedQuery.ThenByDescending(x => x.AmpereS) : orderedQuery.ThenBy(x => x.AmpereS)); break;
                        case "amperet": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.AmpereT) : query.OrderBy(x => x.AmpereT)) : (desc ? orderedQuery.ThenByDescending(x => x.AmpereT) : orderedQuery.ThenBy(x => x.AmpereT)); break;
                        case "w": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.W) : query.OrderBy(x => x.W)) : (desc ? orderedQuery.ThenByDescending(x => x.W) : orderedQuery.ThenBy(x => x.W)); break;
                        case "cosphi": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.CosPhi) : query.OrderBy(x => x.CosPhi)) : (desc ? orderedQuery.ThenByDescending(x => x.CosPhi) : orderedQuery.ThenBy(x => x.CosPhi)); break;
                        case "f": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.F) : query.OrderBy(x => x.F)) : (desc ? orderedQuery.ThenByDescending(x => x.F) : orderedQuery.ThenBy(x => x.F)); break;
                        case "aktifpower": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.AktifPower) : query.OrderBy(x => x.AktifPower)) : (desc ? orderedQuery.ThenByDescending(x => x.AktifPower) : orderedQuery.ThenBy(x => x.AktifPower)); break;
                        case "totalw": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.TotalW) : query.OrderBy(x => x.TotalW)) : (desc ? orderedQuery.ThenByDescending(x => x.TotalW) : orderedQuery.ThenBy(x => x.TotalW)); break;
                        case "totalw1m": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.TotalW1M) : query.OrderBy(x => x.TotalW1M)) : (desc ? orderedQuery.ThenByDescending(x => x.TotalW1M) : orderedQuery.ThenBy(x => x.TotalW1M)); break;
                        case "archivedat": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.ArchivedAt) : query.OrderBy(x => x.ArchivedAt)) : (desc ? orderedQuery.ThenByDescending(x => x.ArchivedAt) : orderedQuery.ThenBy(x => x.ArchivedAt)); break;
                        default: break;
                    }
                    first = false;
                }

                return orderedQuery ?? query.OrderByDescending(x => x.ReceivedTime);
            }
            catch
            {
                return query.OrderByDescending(x => x.ReceivedTime);
            }
        }

        private IQueryable<AnomalyLog> ApplyAnomalySort(IQueryable<AnomalyLog> query, string sortJson)
        {
            try
            {
                var sortItems = JsonConvert.DeserializeObject<List<DevExtremeSortItem>>(sortJson);
                if (sortItems == null || sortItems.Count == 0)
                    return query.OrderByDescending(x => x.DetectedTime);

                bool first = true;
                IOrderedQueryable<AnomalyLog> orderedQuery = null;

                foreach (var item in sortItems)
                {
                    bool desc = item.Desc;
                    switch (item.Selector?.ToLower())
                    {
                        case "id": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)) : (desc ? orderedQuery.ThenByDescending(x => x.Id) : orderedQuery.ThenBy(x => x.Id)); break;
                        case "devicekey": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.DeviceKey) : query.OrderBy(x => x.DeviceKey)) : (desc ? orderedQuery.ThenByDescending(x => x.DeviceKey) : orderedQuery.ThenBy(x => x.DeviceKey)); break;
                        case "anomalytype": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.AnomalyType) : query.OrderBy(x => x.AnomalyType)) : (desc ? orderedQuery.ThenByDescending(x => x.AnomalyType) : orderedQuery.ThenBy(x => x.AnomalyType)); break;
                        case "powervalue": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.PowerValue) : query.OrderBy(x => x.PowerValue)) : (desc ? orderedQuery.ThenByDescending(x => x.PowerValue) : orderedQuery.ThenBy(x => x.PowerValue)); break;
                        case "thresholdvalue": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.ThresholdValue) : query.OrderBy(x => x.ThresholdValue)) : (desc ? orderedQuery.ThenByDescending(x => x.ThresholdValue) : orderedQuery.ThenBy(x => x.ThresholdValue)); break;
                        case "deviation": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.Deviation) : query.OrderBy(x => x.Deviation)) : (desc ? orderedQuery.ThenByDescending(x => x.Deviation) : orderedQuery.ThenBy(x => x.Deviation)); break;
                        case "detectedtime": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.DetectedTime) : query.OrderBy(x => x.DetectedTime)) : (desc ? orderedQuery.ThenByDescending(x => x.DetectedTime) : orderedQuery.ThenBy(x => x.DetectedTime)); break;
                        case "acknowledged": orderedQuery = first ? (desc ? query.OrderByDescending(x => x.Acknowledged) : query.OrderBy(x => x.Acknowledged)) : (desc ? orderedQuery.ThenByDescending(x => x.Acknowledged) : orderedQuery.ThenBy(x => x.Acknowledged)); break;
                        default: break;
                    }
                    first = false;
                }

                return orderedQuery ?? query.OrderByDescending(x => x.DetectedTime);
            }
            catch
            {
                return query.OrderByDescending(x => x.DetectedTime);
            }
        }

        // ============================================
        // HISTORY ARCHIVE EXPORT
        // ============================================
        [HttpGet("history-archive-export")]
        public async Task<IActionResult> ExportHistoryArchive(
            [FromQuery] string deviceKey,
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] string format = "csv")
        {
            try
            {
                var query = _context.KWHData_History.AsQueryable();

                if (!string.IsNullOrEmpty(deviceKey))
                    query = query.Where(x => x.DeviceKey == deviceKey);

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
                    query = query.Where(x => x.ReceivedTime >= parsedFrom);

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedTo))
                    query = query.Where(x => x.ReceivedTime < parsedTo.AddDays(1));

                // Limit untuk export archive - maksimal на 1000 data untuk mencegah overload
                var data = await query
                    .OrderByDescending(x => x.ReceivedTime)
                    .Take(1000)
                    .ToListAsync();

                if (format == "excel")
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("<?xml version=\"1.0\"?>");
                    sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
                    sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                    sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
                    sb.AppendLine("<Worksheet ss:Name=\"KWH History Archive\"><Table>");
                    sb.AppendLine("<Row>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">HistoryId</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">OriginalId</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">DeviceKey</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">DeviceId</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">GroupName</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">TerminalTime</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">ReceivedTime</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">PHASE_R</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">PHASE_S</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">PHASE_T</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">AMPERE_R</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">AMPERE_S</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">AMPERE_T</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">W</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">CosPhi</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">F</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">Aktif_Power</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">TotalW</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">TotalW1M</Data></Cell>");
                    sb.AppendLine("<Cell><Data ss:Type=\"String\">ArchivedAt</Data></Cell>");
                    sb.AppendLine("</Row>");

                    foreach (var item in data)
                    {
                        sb.AppendLine("<Row>");
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.HistoryId);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.OriginalId);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0}</Data></Cell>", item.DeviceKey);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0}</Data></Cell>", item.DeviceId);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0}</Data></Cell>", item.GroupName);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.TerminalTime);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.ReceivedTime);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.PhaseR ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.PhaseS ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.PhaseT ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.AmpereR ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.AmpereS ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.AmpereT ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.W ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.CosPhi ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.F ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.AktifPower ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.TotalW ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"Number\">{0}</Data></Cell>", item.TotalW1M ?? 0m);
                        sb.AppendFormat("<Cell><Data ss:Type=\"String\">{0:dd/MM/yyyy HH:mm:ss}</Data></Cell>", item.ArchivedAt);
                        sb.AppendLine("</Row>");
                    }

                    sb.AppendLine("</Table></Worksheet></Workbook>");

                    var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    return File(bytes, "application/vnd.ms-excel", string.Format("KWH_History_Archive_{0:yyyyMMdd_HHmmss}.xls", DateTime.Now));
                }

                // CSV format
                var csv = new StringBuilder();
                csv.AppendLine("HistoryId,OriginalId,DeviceKey,DeviceId,GroupName,TerminalTime,ReceivedTime,PHASE_R,PHASE_S,PHASE_T,AMPERE_R,AMPERE_S,AMPERE_T,W,CosPhi,F,Aktif_Power,TotalW,TotalW1M,ArchivedAt");

                foreach (var item in data)
                {
                    csv.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5:yyyy-MM-dd HH:mm:ss},{6:yyyy-MM-dd HH:mm:ss},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}",
                        item.HistoryId, item.OriginalId, item.DeviceKey, item.DeviceId, item.GroupName,
                        item.TerminalTime, item.ReceivedTime,
                        item.PhaseR ?? 0m, item.PhaseS ?? 0m, item.PhaseT ?? 0m,
                        item.AmpereR ?? 0m, item.AmpereS ?? 0m, item.AmpereT ?? 0m,
                        item.W ?? 0m, item.CosPhi ?? 0m, item.F ?? 0m, item.AktifPower ?? 0m, item.TotalW ?? 0m, item.TotalW1M ?? 0m, item.ArchivedAt));
                }

                var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(csvBytes, "text/csv", string.Format("KWH_History_Archive_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting history archive data");
                return SafeError(ex);
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
                case "receivedTime":
                case "Waktu_Server":
                    if (DateTime.TryParse(strValue, out var dateVal))
                    {
                        if (op == "=") query = query.Where(x => x.Waktu_Server >= dateVal && x.Waktu_Server < dateVal.AddSeconds(1));
                        else if (op == ">") query = query.Where(x => x.Waktu_Server > dateVal);
                        else if (op == "<") query = query.Where(x => x.Waktu_Server < dateVal);
                        else if (op == ">=") query = query.Where(x => x.Waktu_Server >= dateVal);
                        else if (op == "<=") query = query.Where(x => x.Waktu_Server <= dateVal);
                    }
                    break;
                case "dayaWatt":
                case "Daya_Watt":
                case "w":
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
                case "totalW":
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
                case "phaseR":
                    if (decimal.TryParse(strValue, out var voltRVal))
                    {
                        if (op == "=") query = query.Where(x => x.Volt_R == voltRVal);
                        else if (op == ">") query = query.Where(x => x.Volt_R > voltRVal);
                        else if (op == "<") query = query.Where(x => x.Volt_R < voltRVal);
                    }
                    break;
                case "voltS":
                case "Volt_S":
                case "phaseS":
                    if (decimal.TryParse(strValue, out var voltSVal))
                    {
                        if (op == "=") query = query.Where(x => x.Volt_S == voltSVal);
                        else if (op == ">") query = query.Where(x => x.Volt_S > voltSVal);
                        else if (op == "<") query = query.Where(x => x.Volt_S < voltSVal);
                    }
                    break;
                case "voltT":
                case "Volt_T":
                case "phaseT":
                    if (decimal.TryParse(strValue, out var voltTVal))
                    {
                        if (op == "=") query = query.Where(x => x.Volt_T == voltTVal);
                        else if (op == ">") query = query.Where(x => x.Volt_T > voltTVal);
                        else if (op == "<") query = query.Where(x => x.Volt_T < voltTVal);
                    }
                    break;
                case "ampR":
                case "Amp_R":
                case "ampereR":
                    if (decimal.TryParse(strValue, out var ampVal))
                    {
                        if (op == "=") query = query.Where(x => x.Amp_R == ampVal);
                        else if (op == ">") query = query.Where(x => x.Amp_R > ampVal);
                        else if (op == "<") query = query.Where(x => x.Amp_R < ampVal);
                    }
                    break;
                case "ampS":
                case "Amp_S":
                case "ampereS":
                    if (decimal.TryParse(strValue, out var ampSVal))
                    {
                        if (op == "=") query = query.Where(x => x.Amp_S == ampSVal);
                        else if (op == ">") query = query.Where(x => x.Amp_S > ampSVal);
                        else if (op == "<") query = query.Where(x => x.Amp_S < ampSVal);
                    }
                    break;
                case "ampT":
                case "Amp_T":
                case "ampereT":
                    if (decimal.TryParse(strValue, out var ampTVal))
                    {
                        if (op == "=") query = query.Where(x => x.Amp_T == ampTVal);
                        else if (op == ">") query = query.Where(x => x.Amp_T > ampTVal);
                        else if (op == "<") query = query.Where(x => x.Amp_T < ampTVal);
                    }
                    break;
                case "cosPhi":
                case "Cos_Phi":
                    if (decimal.TryParse(strValue, out var cosVal))
                    {
                        if (op == "=") query = query.Where(x => x.Cos_Phi == cosVal);
                        else if (op == ">") query = query.Where(x => x.Cos_Phi > cosVal);
                        else if (op == "<") query = query.Where(x => x.Cos_Phi < cosVal);
                    }
                    break;
                case "f":
                case "Frekuensi_Hz":
                    if (decimal.TryParse(strValue, out var freqVal))
                    {
                        if (op == "=") query = query.Where(x => x.Frekuensi_Hz == freqVal);
                        else if (op == ">") query = query.Where(x => x.Frekuensi_Hz > freqVal);
                        else if (op == "<") query = query.Where(x => x.Frekuensi_Hz < freqVal);
                    }
                    break;
            }

            return query;
        }

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
                            ? ApplySortDescending(query, propName)
                            : ApplySortAscending(query, propName);
                    }
                    else
                    {
                        orderedQuery = sort.Desc
                            ? ApplyThenByDescending(orderedQuery, propName)
                            : ApplyThenByAscending(orderedQuery, propName);
                    }
                }

                return orderedQuery ?? query;
            }
            catch
            {
                return query.OrderByDescending(x => x.Waktu_Server);
            }
        }

        private static IOrderedQueryable<KWHData> ApplySortAscending(IQueryable<KWHData> query, string propName)
        {
            switch (propName)
            {
                case "Id": return query.OrderBy(x => x.Id);
                case "DeviceKey": return query.OrderBy(x => x.DeviceKey);
                case "DeviceId": return query.OrderBy(x => x.DeviceId);
                case "GroupName": return query.OrderBy(x => x.GroupName);
                case "Waktu_Device": return query.OrderBy(x => x.Waktu_Device);
                case "Waktu_Server": return query.OrderBy(x => x.Waktu_Server);
                case "Volt_R": return query.OrderBy(x => x.Volt_R);
                case "Volt_S": return query.OrderBy(x => x.Volt_S);
                case "Volt_T": return query.OrderBy(x => x.Volt_T);
                case "Amp_R": return query.OrderBy(x => x.Amp_R);
                case "Amp_S": return query.OrderBy(x => x.Amp_S);
                case "Amp_T": return query.OrderBy(x => x.Amp_T);
                case "Cos_Phi": return query.OrderBy(x => x.Cos_Phi);
                case "Daya_Watt": return query.OrderBy(x => x.Daya_Watt);
                case "TotalW1M_Wh": return query.OrderBy(x => x.TotalW1M_Wh);
                case "Energi_Aktif_Wh": return query.OrderBy(x => x.Energi_Aktif_Wh);
                case "Total_Energy_Wh": return query.OrderBy(x => x.Total_Energy_Wh);
                case "Frekuensi_Hz": return query.OrderBy(x => x.Frekuensi_Hz);
                default: return query.OrderBy(x => x.Waktu_Server);
            }
        }

        private static IOrderedQueryable<KWHData> ApplySortDescending(IQueryable<KWHData> query, string propName)
        {
            switch (propName)
            {
                case "Id": return query.OrderByDescending(x => x.Id);
                case "DeviceKey": return query.OrderByDescending(x => x.DeviceKey);
                case "DeviceId": return query.OrderByDescending(x => x.DeviceId);
                case "GroupName": return query.OrderByDescending(x => x.GroupName);
                case "Waktu_Device": return query.OrderByDescending(x => x.Waktu_Device);
                case "Waktu_Server": return query.OrderByDescending(x => x.Waktu_Server);
                case "Volt_R": return query.OrderByDescending(x => x.Volt_R);
                case "Volt_S": return query.OrderByDescending(x => x.Volt_S);
                case "Volt_T": return query.OrderByDescending(x => x.Volt_T);
                case "Amp_R": return query.OrderByDescending(x => x.Amp_R);
                case "Amp_S": return query.OrderByDescending(x => x.Amp_S);
                case "Amp_T": return query.OrderByDescending(x => x.Amp_T);
                case "Cos_Phi": return query.OrderByDescending(x => x.Cos_Phi);
                case "Daya_Watt": return query.OrderByDescending(x => x.Daya_Watt);
                case "TotalW1M_Wh": return query.OrderByDescending(x => x.TotalW1M_Wh);
                case "Energi_Aktif_Wh": return query.OrderByDescending(x => x.Energi_Aktif_Wh);
                case "Total_Energy_Wh": return query.OrderByDescending(x => x.Total_Energy_Wh);
                case "Frekuensi_Hz": return query.OrderByDescending(x => x.Frekuensi_Hz);
                default: return query.OrderByDescending(x => x.Waktu_Server);
            }
        }

        private static IOrderedQueryable<KWHData> ApplyThenByAscending(IOrderedQueryable<KWHData> query, string propName)
        {
            switch (propName)
            {
                case "Id": return query.ThenBy(x => x.Id);
                case "DeviceKey": return query.ThenBy(x => x.DeviceKey);
                case "DeviceId": return query.ThenBy(x => x.DeviceId);
                case "GroupName": return query.ThenBy(x => x.GroupName);
                case "Waktu_Device": return query.ThenBy(x => x.Waktu_Device);
                case "Waktu_Server": return query.ThenBy(x => x.Waktu_Server);
                case "Volt_R": return query.ThenBy(x => x.Volt_R);
                case "Volt_S": return query.ThenBy(x => x.Volt_S);
                case "Volt_T": return query.ThenBy(x => x.Volt_T);
                case "Amp_R": return query.ThenBy(x => x.Amp_R);
                case "Amp_S": return query.ThenBy(x => x.Amp_S);
                case "Amp_T": return query.ThenBy(x => x.Amp_T);
                case "Cos_Phi": return query.ThenBy(x => x.Cos_Phi);
                case "Daya_Watt": return query.ThenBy(x => x.Daya_Watt);
                case "TotalW1M_Wh": return query.ThenBy(x => x.TotalW1M_Wh);
                case "Energi_Aktif_Wh": return query.ThenBy(x => x.Energi_Aktif_Wh);
                case "Total_Energy_Wh": return query.ThenBy(x => x.Total_Energy_Wh);
                case "Frekuensi_Hz": return query.ThenBy(x => x.Frekuensi_Hz);
                default: return query.ThenBy(x => x.Waktu_Server);
            }
        }

        private static IOrderedQueryable<KWHData> ApplyThenByDescending(IOrderedQueryable<KWHData> query, string propName)
        {
            switch (propName)
            {
                case "Id": return query.ThenByDescending(x => x.Id);
                case "DeviceKey": return query.ThenByDescending(x => x.DeviceKey);
                case "DeviceId": return query.ThenByDescending(x => x.DeviceId);
                case "GroupName": return query.ThenByDescending(x => x.GroupName);
                case "Waktu_Device": return query.ThenByDescending(x => x.Waktu_Device);
                case "Waktu_Server": return query.ThenByDescending(x => x.Waktu_Server);
                case "Volt_R": return query.ThenByDescending(x => x.Volt_R);
                case "Volt_S": return query.ThenByDescending(x => x.Volt_S);
                case "Volt_T": return query.ThenByDescending(x => x.Volt_T);
                case "Amp_R": return query.ThenByDescending(x => x.Amp_R);
                case "Amp_S": return query.ThenByDescending(x => x.Amp_S);
                case "Amp_T": return query.ThenByDescending(x => x.Amp_T);
                case "Cos_Phi": return query.ThenByDescending(x => x.Cos_Phi);
                case "Daya_Watt": return query.ThenByDescending(x => x.Daya_Watt);
                case "TotalW1M_Wh": return query.ThenByDescending(x => x.TotalW1M_Wh);
                case "Energi_Aktif_Wh": return query.ThenByDescending(x => x.Energi_Aktif_Wh);
                case "Total_Energy_Wh": return query.ThenByDescending(x => x.Total_Energy_Wh);
                case "Frekuensi_Hz": return query.ThenByDescending(x => x.Frekuensi_Hz);
                default: return query.ThenByDescending(x => x.Waktu_Server);
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
                        if (s.Selector == "dayaWatt") value = await query.SumAsync(x => x.Daya_Watt) ?? 0m;
                        else if (s.Selector == "totalEnergy") value = await query.SumAsync(x => x.Total_Energy_Wh) ?? 0m;
                        else if (s.Selector == "energiAktif") value = await query.SumAsync(x => x.Energi_Aktif_Wh) ?? 0m;
                        else if (s.Selector == "totalW1M") value = await query.SumAsync(x => x.TotalW1M_Wh) ?? 0m;
                    }
                    else if (s.Type == "avg")
                    {
                        if (s.Selector == "voltR") value = Math.Round((double)(await query.AverageAsync(x => x.Volt_R) ?? 0m), 1);
                        else if (s.Selector == "ampR") value = Math.Round((double)(await query.AverageAsync(x => x.Amp_R) ?? 0m), 3);
                        else if (s.Selector == "cosPhi") value = Math.Round((double)(await query.AverageAsync(x => x.Cos_Phi) ?? 0m), 3);
                        else if (s.Selector == "dayaWatt") value = Math.Round((double)(await query.AverageAsync(x => x.Daya_Watt) ?? 0m), 0);
                        else if (s.Selector == "frekuensi") value = Math.Round((double)(await query.AverageAsync(x => x.Frekuensi_Hz) ?? 0m), 2);
                    }
                    else if (s.Type == "min")
                    {
                        if (s.Selector == "voltR") value = await query.MinAsync(x => x.Volt_R) ?? 0m;
                        else if (s.Selector == "dayaWatt") value = await query.MinAsync(x => x.Daya_Watt) ?? 0m;
                    }
                    else if (s.Type == "max")
                    {
                        if (s.Selector == "voltR") value = await query.MaxAsync(x => x.Volt_R) ?? 0m;
                        else if (s.Selector == "dayaWatt") value = await query.MaxAsync(x => x.Daya_Watt) ?? 0m;
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
        [Authorize(Roles = "Admin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST DATABASE CONNECTION
        // ============================================
        [HttpPost("test-database-connection")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // MQTT CONNECTION TEST
        // ============================================
        [HttpPost("test-mqtt-connection")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> TestMqttConnection([FromBody] MqttConnectionData data)
        {
            try
            {
                // Sertifikat yang dipakai adalah file yang sudah ter-upload (tersimpan di DB)
                var settings = new MqttSettings
                {
                    Broker = data.broker,
                    Port = data.port,
                    Username = data.username ?? string.Empty,
                    Password = data.password ?? string.Empty,
                    UseTls = data.useTls,
                    ClientId = string.IsNullOrWhiteSpace(data.clientId) ? "KWHMonitoringWeb" : data.clientId,
                    CaCertificateFile = await GetSettingValueAsync("MQTT.TlsCaCertFile"),
                    ClientCertificateFile = await GetSettingValueAsync("MQTT.TlsClientCertFile"),
                    ClientCertificatePassword = data.clientCertPassword ?? string.Empty,
                    SkipCertificateValidation = data.skipCertValidation
                };

                var result = await _mqttService.TestConnectionAsync(settings);

                if (result.Connected)
                {
                    return Ok(new { success = true, message = "MQTT connection successful" });
                }

                var error = string.IsNullOrWhiteSpace(result.Error) ? "Failed to connect to MQTT broker" : result.Error;
                return BadRequest(new { success = false, error = error });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // MQTT CERTIFICATE UPLOAD
        // ============================================
        [HttpPost("upload-mqtt-certificate")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> UploadMqttCertificate(IFormFile file, [FromForm] string certificateType, [FromForm] string certificatePassword)
        {
            try
            {
                // Safety: beberapa request/versi ASP.NET Core tidak mengikat form field ke parameter string saat ada IFormFile
                if (string.IsNullOrEmpty(certificateType) && Request.HasFormContentType)
                {
                    certificateType = Request.Form["certificateType"].FirstOrDefault();
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, error = "Certificate file is required" });
                }

                var isCa = string.Equals(certificateType, "ca", StringComparison.OrdinalIgnoreCase);
                if (!isCa && !string.Equals(certificateType, "client", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, error = "Invalid certificate type" });
                }

                var allowedExtensions = isCa
                    ? new[] { ".pem", ".crt", ".cer", ".der", "" }
                    : new[] { ".pfx", ".p12", "" };

                var extension = (Path.GetExtension(file.FileName) ?? string.Empty).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(new { success = false, error = isCa
                        ? "CA certificate harus .pem / .crt / .cer / .der"
                        : "Client certificate harus .pfx / .p12" });
                }

                if (file.Length > 1024 * 1024)
                {
                    return BadRequest(new { success = false, error = "Certificate file must be under 1 MB" });
                }

                // Validasi isi sertifikat sebelum disimpan
                var tempPath = Path.GetTempFileName();
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                if (isCa)
                {
                    // CA certificate: validasi isi (PEM atau DER)
                    var raw = System.IO.File.ReadAllBytes(tempPath);
                    var text = System.Text.Encoding.ASCII.GetString(raw);
                    try
                    {
                        if (text.Contains("-----BEGIN CERTIFICATE-----"))
                        {
                            var pem = text.Replace("-----BEGIN CERTIFICATE-----", string.Empty)
                                          .Replace("-----END CERTIFICATE-----", string.Empty)
                                          .Trim();
                            new System.Security.Cryptography.X509Certificates.X509Certificate2(Convert.FromBase64String(pem));
                        }
                        else
                        {
                            new System.Security.Cryptography.X509Certificates.X509Certificate2(raw);
                        }
                    }
                    catch (Exception)
                    {
                        System.IO.File.Delete(tempPath);
                        return BadRequest(new { success = false, error = "File is not a valid certificate" });
                    }
                }
                else
                {
                    // Client PFX: jika password diberikan, validasi langsung.
                    // Tanpa password, cek header ASN.1 SEQUENCE (byte 0x30) milik file PFX
                    // karena PFX ber-password tidak bisa di-load tanpa password-nya.
                    var raw = System.IO.File.ReadAllBytes(tempPath);
                    var password = certificatePassword ?? string.Empty;
                    var isValid = false;
                    if (password.Length > 0)
                    {
                        try
                        {
                            new System.Security.Cryptography.X509Certificates.X509Certificate2(tempPath, password);
                            isValid = true;
                        }
                        catch (Exception)
                        {
                            // password salah — kembalikan pesan yang jelas
                        }
                    }
                    else
                    {
                        isValid = raw.Length > 0 && raw[0] == 0x30;
                    }

                    if (!isValid)
                    {
                        System.IO.File.Delete(tempPath);
                        return BadRequest(new { success = false, error = password.Length > 0
                            ? "Client certificate is invalid or the password is wrong"
                            : "File is not a valid .pfx or .p12 certificate" });
                    }
                }

                var directory = Path.Combine(_environment.ContentRootPath, "AppData", "MqttCerts");
                Directory.CreateDirectory(directory);

                var fileName = (isCa ? "ca_certificate" : "client_certificate") + extension.ToLower();
                var fullPath = Path.Combine(directory, fileName);

                System.IO.File.Copy(tempPath, fullPath, true);
                System.IO.File.Delete(tempPath);

                // Bersihkan file lama dengan ekstensi berbeda maupun file tanpa ekstensi sebelumnya
                var baseName = (isCa ? "ca_certificate" : "client_certificate");
                foreach (var existingFile in Directory.GetFiles(directory, baseName + "*"))
                {
                    if (!string.Equals(existingFile, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try { System.IO.File.Delete(existingFile); } catch { /* ignore */ }
                    }
                }

                var settingKey = isCa ? "MQTT.TlsCaCertFile" : "MQTT.TlsClientCertFile";
                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == settingKey);

                if (existing != null)
                {
                    existing.SettingValue = fileName;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = settingKey,
                        SettingValue = fileName,
                        UpdatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("MQTT certificate uploaded: {Type} {FileName}", certificateType, fileName);
                return Ok(new { success = true, fileName = fileName, message = "Certificate uploaded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload MQTT certificate");
                return SafeError(ex);
            }
        }

        // ============================================
        // MQTT CERTIFICATE REMOVE
        // ============================================
        [HttpPost("remove-mqtt-certificate")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> RemoveMqttCertificate([FromBody] MqttCertificateRequest request)
        {
            try
            {
                var isCa = string.Equals(request?.certificateType, "ca", StringComparison.OrdinalIgnoreCase);
                if (!isCa && !string.Equals(request?.certificateType, "client", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, error = "Invalid certificate type" });
                }

                var settingKey = isCa ? "MQTT.TlsCaCertFile" : "MQTT.TlsClientCertFile";
                var record = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == settingKey);

                if (record != null)
                {
                    var directory = Path.Combine(_environment.ContentRootPath, "AppData", "MqttCerts");
                    if (!string.IsNullOrWhiteSpace(record.SettingValue))
                    {
                        var path = Path.Combine(directory, record.SettingValue);
                        if (System.IO.File.Exists(path))
                        {
                            System.IO.File.Delete(path);
                        }
                    }

                    _context.AppSettingsRecords.Remove(record);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("MQTT certificate removed: {Type}", request?.certificateType);
                return Ok(new { success = true, message = "Certificate removed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove MQTT certificate");
                return SafeError(ex);
            }
        }

        // ============================================
        // PUBLISH RELAY CONTROL COMMAND
        // ============================================
        [HttpPost("publish-relay")]
        [Authorize(Roles = "Operator,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishRelayControl([FromBody] RelayControlRequest request)
        {
            var userIdentifier = User.Identity.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

            try
            {
                if (string.IsNullOrWhiteSpace(request.DeviceId))
                {
                    return BadRequest(new { success = false, error = "DeviceId is required" });
                }

                if (request.RCValue != "0" && request.RCValue != "1" && request.RCValue != "2" && request.RCValue != "3")
                {
                    return BadRequest(new { success = false, error = "RC value must be 0, 1, 2, or 3" });
                }

                // OTP validation for OFF (RC=0) and Pulse OFF (Pulse=true, RC=2)
                var requiresOtp = request.RCValue == "0" || (request.Pulse && request.RCValue == "2");
                if (requiresOtp)
                {
                    var otpKey = $"RelayOtp_{userIdentifier}_{request.DeviceId}_{request.RCValue}";
                    if (!_cache.TryGetValue(otpKey, out string storedCode) || string.IsNullOrWhiteSpace(storedCode))
                    {
                        await LogSecurityActionAsync(SecurityAction.RelayOtpFailed, request.DeviceId,
                            "OTP required but not found or expired", false);
                        return BadRequest(new { success = false, error = "Kode OTP belum diminta atau sudah kadaluarsa. Silakan minta kode baru." });
                    }

                    var failKey = $"RelayOtpFail_{userIdentifier}_{request.DeviceId}";
                    var failCount = _cache.TryGetValue(failKey, out int fc) ? fc : 0;
                    if (failCount >= 3)
                    {
                        await LogSecurityActionAsync(SecurityAction.RelayOtpFailed, request.DeviceId,
                            "OTP max attempts exceeded", false);
                        _cache.Remove(otpKey);
                        _cache.Remove(failKey);
                        return StatusCode(429, new { success = false, error = "Terlalu banyak percobaan kode salah. Silakan minta kode baru." });
                    }

                    if (string.IsNullOrWhiteSpace(request.OtpCode) || !PasswordHasher.ConstantTimeEquals(request.OtpCode.Trim(), storedCode.Trim()))
                    {
                        _cache.Set(failKey, failCount + 1, TimeSpan.FromMinutes(5));
                        await LogSecurityActionAsync(SecurityAction.RelayOtpFailed, request.DeviceId,
                            $"Invalid OTP code (attempt {failCount + 1}/3)", false);
                        return BadRequest(new { success = false, error = $"Kode OTP salah. Sisa percobaan: {2 - failCount}" });
                    }

                    _cache.Remove(otpKey);
                    _cache.Remove(failKey);
                    await LogSecurityActionAsync(SecurityAction.RelayOtpVerified, request.DeviceId,
                        "OTP verified successfully", true);
                }

                // Rate limiting: max 10 relay commands per 60 seconds per user
                if (IsRelayControlRateLimited(userIdentifier))
                {
                    await LogSecurityActionAsync(SecurityAction.UnauthorizedAttempt, request.DeviceId,
                        "Relay control rate limit exceeded", false);
                    return StatusCode(429, new { success = false, error = "Terlalu banyak perintah relay. Silakan tunggu 60 detik." });
                }

                var action = request.Pulse
                    ? SecurityAction.RelayPulse
                    : request.RCValue == "0"
                        ? SecurityAction.RelayOff
                        : SecurityAction.RelayOn;

                bool result;
                if (request.Pulse)
                {
                    result = await _mqttService.PublishRelayPulseAsync(request.DeviceId, request.RCValue);
                }
                else
                {
                    result = await _mqttService.PublishRelayControlAsync(request.DeviceId, request.RCValue);
                }

                if (result)
                {
                    var details = $"Device {request.DeviceId}, RC={request.RCValue}, Pulse={request.Pulse}";
                    await LogSecurityActionAsync(action, request.DeviceId, details, true);
                    await _emailService.SendCriticalActionNotificationAsync(
                        User.Identity.Name,
                        request.Pulse ? "Relay Pulse" : (request.RCValue == "0" ? "Relay OFF" : "Relay ON"),
                        details);

                    return Ok(new { success = true, message = $"Command RC={request.RCValue} published to {request.DeviceId}" });
                }

                await LogSecurityActionAsync(action, request.DeviceId, "Failed to publish MQTT command", false);
                return BadRequest(new { success = false, error = "Failed to publish MQTT command" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish relay control command");
                await LogSecurityActionAsync(SecurityAction.RelayOn, request.DeviceId, "Exception: " + ex.Message, false);
                return SafeError(ex);
            }
        }

        // ============================================
        // REQUEST RELAY OTP (for OFF / Pulse OFF)
        // ============================================
        [HttpPost("request-relay-otp")]
        [Authorize(Roles = "Operator,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRelayOtp([FromBody] RelayControlRequest request)
        {
            var userIdentifier = User.Identity.Name ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

            try
            {
                if (string.IsNullOrWhiteSpace(request.DeviceId))
                    return BadRequest(new { success = false, error = "DeviceId is required" });

                // Only allow OTP for OFF (RC=0) or Pulse OFF (Pulse=true, RC=2)
                var requiresOtp = request.RCValue == "0" || (request.Pulse && request.RCValue == "2");
                if (!requiresOtp)
                    return BadRequest(new { success = false, error = "OTP hanya diperlukan untuk perintah OFF." });

                // Rate limit OTP requests: max 3 per 5 minutes
                var rateKey = $"RelayOtpRate_{userIdentifier}";
                var otpRequestCount = _cache.TryGetValue(rateKey, out int orc) ? orc : 0;
                if (otpRequestCount >= 3)
                {
                    await LogSecurityActionAsync(SecurityAction.RelayOtpFailed, request.DeviceId,
                        "OTP request rate limit exceeded", false);
                    return StatusCode(429, new { success = false, error = "Terlalu banyak permintaan OTP. Tunggu 5 menit." });
                }

                // Get user email
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out int uid))
                    {
                        var user = await _context.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == uid);
                        userEmail = user?.Email;
                    }
                }
                if (string.IsNullOrWhiteSpace(userEmail))
                    return BadRequest(new { success = false, error = "Email user tidak ditemukan. Tidak dapat mengirim OTP." });

                // Get device group name: prefer from request (frontend), fallback to DeviceId
                var groupName = !string.IsNullOrWhiteSpace(request.GroupName) ? request.GroupName : request.DeviceId;

                // Generate 6-digit OTP
                var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                var code = (Math.Abs(BitConverter.ToInt32(bytes, 0)) % 900000 + 100000).ToString("D6");

                // Store OTP in cache (5 min TTL)
                var otpKey = $"RelayOtp_{userIdentifier}_{request.DeviceId}_{request.RCValue}";
                _cache.Set(otpKey, code, TimeSpan.FromMinutes(5));

                // Reset fail counter
                var failKey = $"RelayOtpFail_{userIdentifier}_{request.DeviceId}";
                _cache.Remove(failKey);

                // Update rate limit counter
                _cache.Set(rateKey, otpRequestCount + 1, TimeSpan.FromMinutes(5));

                // Send OTP via email
                var actionText = request.Pulse ? "Pulse OFF" : "OFF";
                await _emailService.SendRelayOtpAsync(userEmail, code, groupName, actionText);

                await LogSecurityActionAsync(SecurityAction.RelayOtpRequested, request.DeviceId,
                    $"OTP requested for {actionText}, sent to {userEmail}", true);

                return Ok(new { success = true, message = "Kode OTP telah dikirim ke email Anda." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request relay OTP");
                return SafeError(ex);
            }
        }

        // ============================================
        // GET RELAY STATES (RCI) FROM DEVICE
        // ============================================
        [HttpGet("relay-states")]
        [AllowAnonymous] // Status relay untuk ditampilkan di dashboard (read-only)
        public async Task<IActionResult> GetRelayStates()
        {
            try
            {
                var states = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        // Deteksi apakah kolom RC ada di tabel (untuk backward compatibility).
                        command.CommandText = @"SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'RelayControl' AND COLUMN_NAME = 'RC'";
                        var rcColumnExists = await command.ExecuteScalarAsync() != null;

                        // Ambil record terbaru per DeviceKey. Prioritaskan RCI; jika RCI kosong/null, fallback ke RC.
                        command.CommandText = rcColumnExists
                            ? @"
                                SELECT DeviceKey, DeviceId, GroupName, RCI, RC, ReceivedTime
                                FROM (
                                    SELECT *,
                                        ROW_NUMBER() OVER (PARTITION BY DeviceKey ORDER BY ReceivedTime DESC) AS rn
                                    FROM RelayControl
                                ) t
                                WHERE rn = 1"
                            : @"
                                SELECT DeviceKey, DeviceId, GroupName, RCI, NULL AS RC, ReceivedTime
                                FROM (
                                    SELECT *,
                                        ROW_NUMBER() OVER (PARTITION BY DeviceKey ORDER BY ReceivedTime DESC) AS rn
                                    FROM RelayControl
                                ) t
                                WHERE rn = 1";

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var deviceKey = (reader["DeviceKey"] as string)?.Trim();
                                var deviceId = (reader["DeviceId"] as string)?.Trim();
                                var groupName = (reader["GroupName"] as string)?.Trim();
                                var rciObj = reader["RCI"];
                                var rciRaw = (rciObj != null && rciObj != DBNull.Value) ? rciObj.ToString().Trim() : null;
                                var rcObj = reader["RC"];
                                var rcRaw = (rcObj != null && rcObj != DBNull.Value) ? rcObj.ToString().Trim() : null;
                                var receivedTime = reader.GetDateTime(reader.GetOrdinal("ReceivedTime"));

                                // Indikator border hanya membaca kolom RCI; tidak fallback ke RC.
                                // Jika RCI kosong/null, indikator menampilkan 'unknown'.
                                string effectiveValue = !string.IsNullOrWhiteSpace(rciRaw) ? rciRaw : null;

                                // Lewati device yang tidak punya nilai RCI sama sekali
                                if (string.IsNullOrWhiteSpace(effectiveValue))
                                    continue;

                                var stateObj = new
                                {
                                    rci = effectiveValue,
                                    rciRaw = rciRaw,
                                    rcRaw = rcRaw,
                                    receivedTime = receivedTime
                                };

                                // Expose state under every possible identifier so the UI can find it
                                // regardless of whether the external process writes DeviceKey, DeviceId, or GroupName.
                                var keys = new[] { deviceKey, deviceId, groupName };
                                foreach (var key in keys)
                                {
                                    if (!string.IsNullOrWhiteSpace(key))
                                    {
                                        states[key] = stateObj;
                                    }
                                }
                            }
                        }
                    }
                }

                return Ok(new { success = true, states = states });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get relay states");
                return Ok(new { success = true, error = "Terjadi kesalahan internal.", states = new Dictionary<string, object>() });
            }
        }

        // ============================================
        // DEVICE CONTROL MODE (GLOBAL PRESET)
        // ============================================
        [HttpGet("device-control-mode")]
        public async Task<IActionResult> GetDeviceControlMode()
        {
            try
            {
                var mode = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey == "DeviceControlMode")
                    .Select(x => x.SettingValue)
                    .FirstOrDefaultAsync() ?? "OnOff";

                return Ok(new { success = true, mode = mode });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        [HttpPost("device-control-mode")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDeviceControlMode([FromBody] DeviceControlModeRequest request)
        {
            try
            {
                var validModes = new[] { "Toggle", "Pulse", "OnOff", "Toggle2Relay", "ToggleAll" };
                if (!validModes.Contains(request.Mode))
                {
                    return BadRequest(new { success = false, error = "Invalid control mode" });
                }

                var existing = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "DeviceControlMode");

                if (existing != null)
                {
                    existing.SettingValue = request.Mode;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.AppSettingsRecords.Add(new AppSettingsRecord
                    {
                        SettingKey = "DeviceControlMode",
                        SettingValue = request.Mode,
                        UpdatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Control mode saved" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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
                return SafeError(ex);
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
                    emaUpperThreshold = GetInt(settings, "emaUpperThreshold", 0),
                    emaLowerThreshold = GetInt(settings, "emaLowerThreshold", 0),
                    emaFibUpper = GetDouble(settings, "emaFibUpper", 0),
                    emaFibLower = GetDouble(settings, "emaFibLower", 0),
                    emaShowLine = GetBool(settings, "emaShowLine", true),
                    emaShowThresholds = GetBool(settings, "emaShowThresholds", true),
                    useInitial100ForEma = GetBool(settings, "useInitial100ForEma", false),
                    refreshInterval = GetInt(settings, "refreshInterval", 10),
                    chartDataPoints = GetInt(settings, "chartDataPoints", 20)
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // EMA SETTINGS - SAVE
        // ============================================
        [HttpPost("save-ema-settings")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // ANOMALY LOGS - GET (Server-Side Pagination)
        // ============================================
        [AllowAnonymous]
        [HttpGet("anomaly-logs/summary")]
        public async Task<IActionResult> GetAnomalyLogsSummary()
        {
            try
            {
                var query = _context.AnomalyLogs.AsQueryable();

                var totalCount = await query.CountAsync();
                var overloadCount = await query.CountAsync(x => x.AnomalyType == "OVERLOAD");
                var dropCount = await query.CountAsync(x => x.AnomalyType == "DROP");
                var activeDeviceCount = await query.Select(x => x.DeviceKey).Distinct().CountAsync();
                var unresolvedCount = await query.CountAsync(x => !x.IsResolved);
                var criticalCount = await query.CountAsync(x => x.Severity == "critical");

                return Ok(new
                {
                    success = true,
                    totalLogs = totalCount,
                    totalOverload = overloadCount,
                    totalDrop = dropCount,
                    activeDevices = activeDeviceCount,
                    unresolved = unresolvedCount,
                    critical = criticalCount
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        [AllowAnonymous]
        [HttpGet("anomaly-logs/{deviceKey}")]
        public async Task<IActionResult> GetAnomalyLogs(
            string deviceKey,
            int skip = 0,
            int take = 10,
            string sort = null,
            string filter = null)
        {
            try
            {
                var canViewOperationalMetadata = User.IsInRole("Viewer") || User.IsInRole("Operator") || User.IsInRole("Admin");
                if (skip < 0) skip = 0;
                if (take < 1) take = 10;
                if (take > 1000) take = 1000;

                IQueryable<AnomalyLog> query;

                if (deviceKey.ToUpper() == "ALL")
                {
                    query = _context.AnomalyLogs.AsQueryable();
                }
                else
                {
                    query = _context.AnomalyLogs.Where(x => x.DeviceKey == deviceKey);
                }

                // Apply sorting
                if (!string.IsNullOrEmpty(sort))
                {
                    query = ApplyAnomalySort(query, sort);
                }
                else
                {
                    query = query.OrderByDescending(x => x.DetectedTime);
                }

                // Load device group names, locations and financial settings
                var deviceGroupNames = await _context.DeviceRegistry
                    .Where(x => x.GroupName != null && x.GroupName != "")
                    .ToDictionaryAsync(x => x.DeviceKey, x => x.GroupName);

                var deviceLocations = await _context.DeviceRegistry
                    .Where(x => x.Location != null && x.Location != "")
                    .ToDictionaryAsync(x => x.DeviceKey, x => x.Location);

                var deviceSettings = await _context.DeviceSettings
                    .ToDictionaryAsync(x => x.DeviceKey, x => new
                    {
                        x.DeviceCategory,
                        x.TariffPerKWh,
                        x.RevenuePerHour
                    });

                var totalCount = await query.CountAsync();
                var logs = await query
                    .Skip(skip)
                    .Take(take)
                    .Select(x => new
                    {
                        id = x.Id,
                        deviceKey = x.DeviceKey,
                        deviceId = x.DeviceId,
                        groupName = (string)null,
                        anomalyType = x.AnomalyType,
                        powerValue = x.PowerValue,
                        thresholdValue = x.ThresholdValue,
                        deviation = x.Deviation,
                        detectedTime = x.DetectedTime,
                        emaValue = x.EMAValue,
                        thresholdMode = x.ThresholdMode,
                        severity = x.Severity,
                        rootCause = x.RootCause,
                        recommendedAction = x.RecommendedAction,
                        acknowledged = x.Acknowledged ?? false,
                        acknowledgedBy = x.AcknowledgedBy,
                        acknowledgedTime = x.AcknowledgedTime,
                        isResolved = x.IsResolved,
                        resolvedBy = x.ResolvedBy,
                        resolvedTime = x.ResolvedTime,
                        operatorAction = x.OperatorAction,
                        operatorNotes = x.OperatorNotes,
                        notes = x.Notes
                    })
                    .ToListAsync();

                // Resolve groupName and compute financial impact per anomaly
                var calculationTime = DateTime.Now;
                var result = logs.Select(x =>
                {
                    var settings = deviceSettings.ContainsKey(x.deviceKey) ? deviceSettings[x.deviceKey] : null;
                    var category = settings?.DeviceCategory ?? "Unknown";
                    var tariffPerKWh = settings?.TariffPerKWh ?? 1500m;
                    var revenuePerHour = settings?.RevenuePerHour ?? 0m;
                    var isOverload = x.anomalyType == "OVERLOAD";

                    // Duration in minutes
                    double? durationMinutes = null;
                    if (x.isResolved && x.resolvedTime.HasValue)
                        durationMinutes = (x.resolvedTime.Value - x.detectedTime).TotalMinutes;

                    // Response time in minutes (acknowledge first, else resolve)
                    double? responseTimeMinutes = null;
                    if (x.acknowledged && x.acknowledgedTime.HasValue)
                        responseTimeMinutes = (x.acknowledgedTime.Value - x.detectedTime).TotalMinutes;
                    else if (x.isResolved && x.resolvedTime.HasValue)
                        responseTimeMinutes = (x.resolvedTime.Value - x.detectedTime).TotalMinutes;

                    // Revenue loss for DROP anomalies
                    var revenueLoss = EstimateRevenueLoss(x.anomalyType, x.detectedTime, x.isResolved, x.resolvedTime, revenuePerHour, calculationTime);

                    // Energy cost impact for OVERLOAD anomalies
                    decimal anomalyCostImpact = 0m;
                    if (isOverload)
                    {
                        var excessKWh = Math.Max(x.powerValue - x.thresholdValue, 0m) * 0.25m / 1000m;
                        anomalyCostImpact = Math.Round(excessKWh * tariffPerKWh, 0);
                    }

                    var location = deviceLocations.ContainsKey(x.deviceKey) ? deviceLocations[x.deviceKey] : "-";

                    var dict = new Dictionary<string, object>
                    {
                        { "id", x.id },
                        { "deviceKey", x.deviceKey },
                        { "deviceId", x.deviceId },
                        { "groupName", deviceGroupNames.ContainsKey(x.deviceKey) ? deviceGroupNames[x.deviceKey] : x.deviceKey },
                        { "location", location },
                        { "deviceCategory", category },
                        { "anomalyType", x.anomalyType },
                        { "powerValue", x.powerValue },
                        { "thresholdValue", x.thresholdValue },
                        { "deviation", x.deviation },
                        { "detectedTime", x.detectedTime },
                        { "emaValue", x.emaValue },
                        { "thresholdMode", x.thresholdMode },
                        { "severity", x.severity },
                        { "rootCause", x.rootCause },
                        { "recommendedAction", x.recommendedAction },
                        { "acknowledged", x.acknowledged },
                        { "acknowledgedBy", canViewOperationalMetadata ? x.acknowledgedBy : null },
                        { "acknowledgedTime", x.acknowledgedTime },
                        { "isResolved", x.isResolved },
                        { "resolvedBy", canViewOperationalMetadata ? x.resolvedBy : null },
                        { "resolvedTime", x.resolvedTime },
                        { "durationMinutes", durationMinutes },
                        { "responseTimeMinutes", responseTimeMinutes },
                        { "revenueLoss", revenueLoss },
                        { "anomalyCostImpact", anomalyCostImpact },
                        { "operatorAction", canViewOperationalMetadata ? x.operatorAction : null },
                        { "operatorNotes", canViewOperationalMetadata ? x.operatorNotes : null },
                        { "notes", x.notes }
                    };
                    return dict;
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = result,
                    totalCount = totalCount,
                    skip = skip,
                    take = take
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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
                double smaBaseline = first100Data.Average(x => (double)(x.Daya_Watt ?? 0m));

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
                return SafeError(ex);
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
                return SafeError(ex);
            }
        }

        [HttpGet("anomaly-device-health/{deviceKey}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAnomalyDeviceHealth(string deviceKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deviceKey) || deviceKey.Length > 20)
                    return BadRequest(new { success = false, error = "Invalid device key" });

                var assessment = await AssessDeviceSilenceAsync(deviceKey);
                if (assessment == null)
                    return Ok(new { success = true, hasTelemetry = false, isSilent = false });

                // Clear a DEVICE_OFFLINE episode only after telemetry has resumed after its alert time.
                if (!assessment.IsSilent)
                {
                    var activeKey = "AnomalyAlert.Active." + deviceKey;
                    var active = await _context.AppSettingsRecords.FirstOrDefaultAsync(x => x.SettingKey == activeKey);
                    if (active != null && (active.SettingValue.StartsWith("DEVICE_OFFLINE|", StringComparison.Ordinal)
                        || active.SettingValue.StartsWith("DEVICE_DROP|", StringComparison.Ordinal)))
                    {
                        var parts = active.SettingValue.Split('|');
                        DateTime alertTime;
                        if (parts.Length > 1 && DateTime.TryParseExact(parts[1], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out alertTime) && assessment.LastSampleTime > alertTime)
                        {
                            _context.AppSettingsRecords.Remove(active);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                return Ok(new
                {
                    success = true,
                    hasTelemetry = true,
                    isSilent = assessment.IsSilent,
                    deviceKey = deviceKey,
                    sampleTime = assessment.LastSampleTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    secondsSinceLastSample = assessment.SecondsSinceLastSample,
                    expectedIntervalSeconds = assessment.ExpectedIntervalSeconds,
                    silenceThresholdSeconds = assessment.SilenceThresholdSeconds,
                    lastPowerValue = assessment.LastPowerValue
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        private async Task<DeviceSilenceAssessment> AssessDeviceSilenceAsync(string deviceKey)
        {
            var recentTimes = await _context.KWH_Monitoring.AsNoTracking()
                .Where(x => x.DeviceKey == deviceKey)
                .OrderByDescending(x => x.Waktu_Server)
                .Select(x => x.Waktu_Server)
                .Take(31)
                .ToListAsync();
            if (recentTimes.Count == 0) return null;

            var latest = await _context.KWH_Monitoring.AsNoTracking()
                .Where(x => x.DeviceKey == deviceKey && x.Waktu_Server == recentTimes[0])
                .Select(x => new { x.Daya_Watt })
                .FirstOrDefaultAsync();
            var intervals = new List<double>();
            var configuredCheckInterval = await _context.AppSettingsRecords.AsNoTracking()
                .Where(x => x.SettingKey == "Anomaly.CheckInterval")
                .Select(x => x.SettingValue).FirstOrDefaultAsync();
            var checkSeconds = int.TryParse(configuredCheckInterval, out var parsedCheck) && parsedCheck > 0
                ? parsedCheck : 30;
            for (var i = 0; i + 1 < recentTimes.Count; i++)
            {
                var seconds = (recentTimes[i] - recentTimes[i + 1]).TotalSeconds;
                if (seconds > 0) intervals.Add(seconds);
            }

            double expectedInterval;
            if (intervals.Count > 0)
            {
                intervals.Sort();
                var middle = intervals.Count / 2;
                expectedInterval = intervals.Count % 2 == 0
                    ? (intervals[middle - 1] + intervals[middle]) / 2d
                    : intervals[middle];
            }
            else
            {
                expectedInterval = checkSeconds;
            }

            var silenceThreshold = Math.Max(120d, Math.Max(expectedInterval * 3d, checkSeconds * 2d));
            var latestTime = recentTimes[0];
            var age = Math.Max(0d, (DateTime.Now - latestTime).TotalSeconds);

            return new DeviceSilenceAssessment
            {
                LastSampleTime = latestTime,
                LastPowerValue = latest?.Daya_Watt ?? 0m,
                ExpectedIntervalSeconds = expectedInterval,
                SilenceThresholdSeconds = silenceThreshold,
                SecondsSinceLastSample = age,
                IsSilent = age >= silenceThreshold
            };
        }

        // ============================================
        // DOWNTIME PERIOD CHECK
        // Cek apakah sekarang berada dalam periode jam mati (listrik sengaja dimatikan)
        // ============================================
        private async Task<DowntimeCheckResult> CheckDowntimePeriodAsync(string category = null, string deviceKey = null, DateTime? referenceTime = null)
        {
            var result = new DowntimeCheckResult { IsDowntime = false, StartHour = 0, EndHour = 0 };

            try
            {
                // Per-device downtime takes highest priority
                if (!string.IsNullOrWhiteSpace(deviceKey))
                {
                    var deviceSettings = await _context.DeviceSettings
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

                    if (deviceSettings != null && deviceSettings.DowntimeEnabled)
                    {
                        return EvaluateDowntimeTimeSpan(deviceSettings.DowntimeStart, deviceSettings.DowntimeEnd, referenceTime);
                    }
                }

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

                        var now = referenceTime ?? DateTime.Now;
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

                var now2 = referenceTime ?? DateTime.Now;
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

        private DowntimeCheckResult EvaluateDowntimeTimeSpan(TimeSpan start, TimeSpan end, DateTime? referenceTime = null)
        {
            var now = referenceTime ?? DateTime.Now;
            var currentTime = now.TimeOfDay;
            var result = new DowntimeCheckResult
            {
                StartHour = start.Hours,
                EndHour = end.Hours
            };

            if (start < end)
                result.IsDowntime = currentTime >= start && currentTime < end;
            else
                result.IsDowntime = currentTime >= start || currentTime < end;

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
                if (data == null || string.IsNullOrWhiteSpace(data.DeviceKey) || data.DeviceKey.Length > 20)
                    return BadRequest(new { success = false, error = "DeviceKey is required and must match a configured device" });

                var verified = await VerifyAnomalySampleAsync(data);
                if (!verified.IsValid)
                    return BadRequest(new { success = false, error = verified.Error });

                // The server is the source of truth for measurement, threshold and anomaly type.
                data.AnomalyType = verified.AnomalyType;
                data.PowerValue = verified.PowerValue;
                data.ThresholdValue = verified.ThresholdValue;
                data.Deviation = verified.Deviation;
                data.EMAValue = verified.EmaValue;
                data.ThresholdMode = verified.ThresholdMode;

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
                        // Auto-expire: if alert is older than 30 minutes, clear it and continue
                        if (DateTime.TryParseExact(parts[1], "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var alertTime))
                        {
                            if (!IsDeviceOfflineType(data.AnomalyType) && DateTime.Now - alertTime > TimeSpan.FromMinutes(30))
                            {
                                _logger.LogInformation("Anomaly alert for {DeviceKey} ({AnomalyType}) expired (older than 30 min), auto-clearing", data.DeviceKey, data.AnomalyType);
                                _context.AppSettingsRecords.Remove(activeAlertSetting);
                                // fall through to process the new anomaly
                            }
                            else
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
                        else
                        {
                            // Invalid timestamp format — clear stale entry and continue
                            _context.AppSettingsRecords.Remove(activeAlertSetting);
                        }
                    }
                }

                // Resolve device category for category-aware downtime check
                var categorySetting = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "DeviceCategory." + data.DeviceKey);
                var deviceCategory = categorySetting?.SettingValue ?? "Billboard";

                var downtime = await CheckDowntimePeriodAsync(deviceCategory, data.DeviceKey, verified.SampleTime);

                // ============================================
                // DOWNTIME LOGIC:
                // - Jika periode jam mati & anomali DROP ? suppress (jangan log, jangan notifikasi)
                //   Karena listrik sengaja dimatikan, DROP adalah normal
                // - Jika periode jam mati & anomali OVERLOAD ? LOG dengan tipe khusus
                //   Trigger: Power > EMA (bukan Upper Line) - menandakan listrik masih menyala normal
                //   Karena listrik seharusnya mati tapi masih menyala = anomali serius
                // - Jika bukan periode jam mati ? proses normal (trigger: Power > Upper Line)
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
                    DetectedTime = verified.DetectedTime,
                    EMAValue = data.EMAValue,
                    ThresholdMode = data.ThresholdMode ?? "manual",
                    Acknowledged = false,
                    Notes = verified.Notes ?? string.Empty
                };

                // Jika downtime & OVERLOAD ? tandai sebagai anomali pada jam mati
                if (downtime.IsDowntime && data.AnomalyType == "OVERLOAD")
                {
                    log.Notes = string.Format("ALERT: Power detected during downtime period ({0}:00-{1}:00). Expected OFF but power is {2:N0}W",
                        downtime.StartHour, downtime.EndHour, data.PowerValue);
                }

                _context.AnomalyLogs.Add(log);
                await _context.SaveChangesAsync();

                // ============================================================
                // ANALISIS OTOMATIS & SNAPSHOT CHART
                // ============================================================
                try
                {
                    // Ambil riwayat 24 jam terakhir untuk device yang sama
                    var recentHistory = await _context.AnomalyLogs
                        .Where(x => x.DeviceKey == data.DeviceKey && x.DetectedTime >= DateTime.Now.AddHours(-24))
                        .ToListAsync();

                    var analysis = _analysisService.Analyze(log, recentHistory);

                    log.Severity = analysis.Severity;
                    log.RootCause = analysis.RootCause;
                    log.RecommendedAction = analysis.RecommendedAction;

                    // Simpan chart snapshot jika ada
                    if (data.ChartSnapshot != null)
                    {
                        var beforeJson = data.ChartSnapshot.Before != null
                            ? JsonConvert.SerializeObject(data.ChartSnapshot.Before)
                            : null;
                        var afterJson = data.ChartSnapshot.After != null && data.ChartSnapshot.After.Count > 0
                            ? JsonConvert.SerializeObject(data.ChartSnapshot.After)
                            : null;

                        _context.AnomalyChartSnapshots.Add(new AnomalyChartSnapshot
                        {
                            AnomalyLogId = log.Id,
                            DetectedTime = log.DetectedTime,
                            BeforeDataJson = beforeJson,
                            AfterDataJson = afterJson,
                            UpperThreshold = data.ChartSnapshot.UpperThreshold,
                            LowerThreshold = data.ChartSnapshot.LowerThreshold,
                            EMAValue = data.ChartSnapshot.EMAValue,
                            SnapshotStatus = string.IsNullOrEmpty(afterJson) ? "before" : "complete"
                        });
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception analysisEx)
                {
                    _logger.LogWarning(analysisEx, "Failed to analyze anomaly #{LogId}", log.Id);
                    // Jangan gagalkan logging anomali karena analisis gagal
                }

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
                            isTest: false,
                            anomalyContext: verified.Notes
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
                return SafeError(ex);
            }
        }

        private static bool IsDeviceOfflineType(string anomalyType)
        {
            return string.Equals(anomalyType, "DEVICE_OFFLINE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(anomalyType, "DEVICE_DROP", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<VerifiedAnomalySample> VerifyAnomalySampleAsync(AnomalyLogRequest request)
        {
            if (!request.SampleTime.HasValue)
                return VerifiedAnomalySample.Invalid("SampleTime is required");

            if (IsDeviceOfflineType(request.AnomalyType))
            {
                var silence = await AssessDeviceSilenceAsync(request.DeviceKey);
                if (silence == null || Math.Abs((silence.LastSampleTime - request.SampleTime.Value).TotalSeconds) > 1.1)
                    return VerifiedAnomalySample.Invalid("The submitted last-seen time does not match telemetry");
                if (!silence.IsSilent)
                    return VerifiedAnomalySample.Invalid("Telemetry is still arriving within the expected interval");

                return new VerifiedAnomalySample
                {
                    IsValid = true,
                    SampleTime = silence.LastSampleTime,
                    DetectedTime = DateTime.Now,
                    PowerValue = silence.LastPowerValue,
                    ThresholdValue = 0m,
                    Deviation = 0m,
                    EmaValue = null,
                    AnomalyType = "DEVICE_OFFLINE",
                    ThresholdMode = "telemetry-silence",
                    Notes = string.Format(CultureInfo.InvariantCulture,
                        "DEVICE_OFFLINE: no telemetry for {0:N0}s; expected interval {1:N0}s; silence threshold {2:N0}s.",
                        silence.SecondsSinceLastSample, silence.ExpectedIntervalSeconds, silence.SilenceThresholdSeconds)
                };
            }

            if (!string.Equals(request.AnomalyType, "OVERLOAD", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(request.AnomalyType, "DROP", StringComparison.OrdinalIgnoreCase))
                return VerifiedAnomalySample.Invalid("Unsupported anomaly type");

            var latest = await _context.KWH_Monitoring.AsNoTracking()
                .Where(x => x.DeviceKey == request.DeviceKey)
                .OrderByDescending(x => x.Waktu_Server)
                .FirstOrDefaultAsync();
            if (latest == null || !latest.Daya_Watt.HasValue)
                return VerifiedAnomalySample.Invalid("No valid telemetry sample is available");

            // Browser Date objects have millisecond precision; SQL datetime2 can retain finer precision.
            if (Math.Abs((latest.Waktu_Server - request.SampleTime.Value).TotalSeconds) > 1.1)
                return VerifiedAnomalySample.Invalid("The submitted sample is not the latest telemetry sample");

            var settingsRows = await _context.AppSettingsRecords.AsNoTracking()
                .Where(x => x.SettingKey.StartsWith("Anomaly.") || x.SettingKey.StartsWith("ema")
                    || x.SettingKey == "chartDataPoints" || x.SettingKey == "useInitial100ForEma")
                .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
            var intervalSeconds = GetInt(settingsRows, "Anomaly.CheckInterval", 30);
            if (intervalSeconds <= 0) intervalSeconds = 30;
            var maxAgeSeconds = Math.Max(120, intervalSeconds * 3);
            if ((DateTime.Now - latest.Waktu_Server).TotalSeconds > maxAgeSeconds || latest.Waktu_Server > DateTime.Now.AddSeconds(10))
                return VerifiedAnomalySample.Invalid("The latest telemetry sample is stale");

            var pointCount = Math.Max(50, GetInt(settingsRows, "chartDataPoints", 20));
            pointCount = Math.Min(pointCount, 1000);
            var samples = await _context.KWH_Monitoring.AsNoTracking()
                .Where(x => x.DeviceKey == request.DeviceKey)
                .OrderByDescending(x => x.Waktu_Server)
                .Take(pointCount)
                .OrderBy(x => x.Waktu_Server)
                .ToListAsync();
            if (samples.Count == 0 || samples[samples.Count - 1].Waktu_Server != latest.Waktu_Server)
                return VerifiedAnomalySample.Invalid("Telemetry changed while the anomaly was being verified");

            var deviceSettings = await _deviceSettingsService.GetEffectiveAsync(request.DeviceKey);
            var upperValue = deviceSettings == null ? 0d : deviceSettings.EmaUpperThreshold;
            var lowerValue = deviceSettings == null ? 0d : deviceSettings.EmaLowerThreshold;
            if (upperValue <= 0 && lowerValue <= 0)
                return VerifiedAnomalySample.Invalid("Anomaly thresholds are not configured for this device");

            var period = Math.Max(1, GetInt(settingsRows, "emaPeriod", 20));
            var ema = new double[samples.Count];
            var useInitialBaseline = GetBool(settingsRows, "useInitial100ForEma", false);
            if (useInitialBaseline)
            {
                var firstSamples = await _context.KWH_Monitoring.AsNoTracking()
                    .Where(x => x.DeviceKey == request.DeviceKey)
                    .OrderBy(x => x.Waktu_Server)
                    .Take(100)
                    .ToListAsync();
                var baseline = firstSamples.Count == 0 ? 0d : firstSamples.Average(x => (double)(x.Daya_Watt ?? 0m));
                for (var i = 0; i < ema.Length; i++) ema[i] = baseline;
            }
            else
            {
                var k = 2d / (period + 1d);
                ema[0] = (double)(samples[0].Daya_Watt ?? 0m);
                for (var i = 1; i < samples.Count; i++)
                {
                    var value = (double)(samples[i].Daya_Watt ?? 0m);
                    ema[i] = value * k + ema[i - 1] * (1d - k);
                }
            }

            var mode = GetString(settingsRows, "emaMode", "manual");
            var currentPower = (double)latest.Daya_Watt.Value;
            var currentEma = ema[ema.Length - 1];
            var upper = string.Equals(mode, "fibonacci", StringComparison.OrdinalIgnoreCase)
                ? currentEma * upperValue
                : currentEma * (1d + upperValue / 100d);
            var lower = string.Equals(mode, "fibonacci", StringComparison.OrdinalIgnoreCase)
                ? currentEma * lowerValue
                : currentEma * (1d - lowerValue / 100d);
            var category = await _context.AppSettingsRecords.AsNoTracking()
                .Where(x => x.SettingKey == "DeviceCategory." + request.DeviceKey)
                .Select(x => x.SettingValue).FirstOrDefaultAsync() ?? deviceSettings?.DeviceCategory;
            var downtime = await CheckDowntimePeriodAsync(category, request.DeviceKey, latest.Waktu_Server);

            string anomalyType = null;
            double threshold = 0d;
            double deviation = 0d;
            if (downtime.IsDowntime)
            {
                if (currentPower > currentEma && currentEma > 0d)
                {
                    anomalyType = "OVERLOAD";
                    threshold = currentEma;
                    deviation = (currentPower - currentEma) / currentEma * 100d;
                }
            }
            else if (currentPower > upper && upper > 0d)
            {
                anomalyType = "OVERLOAD";
                threshold = upper;
                deviation = (currentPower - upper) / upper * 100d;
            }
            else if (currentPower < lower && lower > 0d)
            {
                anomalyType = "DROP";
                threshold = lower;
                deviation = (lower - currentPower) / lower * 100d;
            }

            if (anomalyType == null || !string.Equals(anomalyType, request.AnomalyType, StringComparison.OrdinalIgnoreCase))
                return VerifiedAnomalySample.Invalid("The submitted anomaly does not match the current server-side thresholds");

            return new VerifiedAnomalySample
            {
                IsValid = true,
                SampleTime = latest.Waktu_Server,
                DetectedTime = latest.Waktu_Server,
                PowerValue = (decimal)currentPower,
                EmaValue = (decimal)currentEma,
                ThresholdValue = (decimal)threshold,
                Deviation = (decimal)Math.Round(deviation, 1),
                AnomalyType = anomalyType,
                ThresholdMode = mode
            };
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
                if (data == null || string.IsNullOrEmpty(data.DeviceKey) || data.DeviceKey.Length > 20)
                    return BadRequest(new { error = "DeviceKey is required" });

                var activeAlertKey = "AnomalyAlert.Active." + data.DeviceKey;
                var activeAlert = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == activeAlertKey);

                if (activeAlert != null)
                {
                    var activeType = activeAlert.SettingValue?.Split('|').FirstOrDefault();
                    if (activeType == "DEVICE_OFFLINE" || activeType == "DEVICE_DROP")
                        return Ok(new { success = true, deviceKey = data.DeviceKey, unchanged = true });

                    var latest = await _context.KWH_Monitoring.AsNoTracking()
                        .Where(x => x.DeviceKey == data.DeviceKey)
                        .OrderByDescending(x => x.Waktu_Server)
                        .FirstOrDefaultAsync();
                    if (latest == null || !latest.Daya_Watt.HasValue
                        || (DateTime.Now - latest.Waktu_Server).TotalSeconds > 120
                        || latest.Waktu_Server > DateTime.Now.AddSeconds(10))
                        return Ok(new { success = true, deviceKey = data.DeviceKey, unchanged = true });

                    var currentAssessment = await VerifyAnomalySampleAsync(new AnomalyLogRequest
                    {
                        DeviceKey = data.DeviceKey,
                        SampleTime = latest.Waktu_Server,
                        AnomalyType = activeType
                    });
                    if (currentAssessment.IsValid && currentAssessment.AnomalyType == activeType)
                        return Ok(new { success = true, deviceKey = data.DeviceKey, unchanged = true });

                    _context.AppSettingsRecords.Remove(activeAlert);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Anomaly alert reset for {DeviceKey}", data.DeviceKey);
                }

                return Ok(new { success = true, deviceKey = data.DeviceKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting anomaly alert for {DeviceKey}", data.DeviceKey);
                return SafeError(ex);
            }
        }

        // ============================================
        // GET SINGLE ANOMALY WITH ANALYSIS & SNAPSHOT
        // ============================================
        [AllowAnonymous]
        [HttpGet("anomaly-logs/detail/{id}")]
        public async Task<IActionResult> GetAnomalyLog(long id)
        {
            try
            {
                var canViewOperationalMetadata = User.IsInRole("Viewer") || User.IsInRole("Operator") || User.IsInRole("Admin");
                var log = await _context.AnomalyLogs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (log == null)
                    return NotFound(new { success = false, error = "Anomaly log not found" });

                var deviceGroupNames = await _context.DeviceRegistry
                    .Where(x => x.GroupName != null && x.GroupName != "")
                    .ToDictionaryAsync(x => x.DeviceKey, x => x.GroupName);

                var deviceLocations = await _context.DeviceRegistry
                    .Where(x => x.Location != null && x.Location != "")
                    .ToDictionaryAsync(x => x.DeviceKey, x => x.Location);

                var deviceSettings = await _context.DeviceSettings
                    .FirstOrDefaultAsync(x => x.DeviceKey == log.DeviceKey);

                var groupName = deviceGroupNames.ContainsKey(log.DeviceKey) ? deviceGroupNames[log.DeviceKey] : log.DeviceKey;
                var location = deviceLocations.ContainsKey(log.DeviceKey) ? deviceLocations[log.DeviceKey] : "-";
                var category = deviceSettings?.DeviceCategory ?? "Unknown";
                var tariffPerKWh = deviceSettings?.TariffPerKWh ?? 1500m;
                var revenuePerHour = deviceSettings?.RevenuePerHour ?? 0m;
                var isOverload = log.AnomalyType == "OVERLOAD";

                double? durationMinutes = null;
                if (log.IsResolved && log.ResolvedTime.HasValue)
                    durationMinutes = (log.ResolvedTime.Value - log.DetectedTime).TotalMinutes;

                double? responseTimeMinutes = null;
                if ((log.Acknowledged ?? false) && log.AcknowledgedTime.HasValue)
                    responseTimeMinutes = (log.AcknowledgedTime.Value - log.DetectedTime).TotalMinutes;
                else if (log.IsResolved && log.ResolvedTime.HasValue)
                    responseTimeMinutes = (log.ResolvedTime.Value - log.DetectedTime).TotalMinutes;

                var revenueLoss = EstimateRevenueLoss(log.AnomalyType, log.DetectedTime, log.IsResolved, log.ResolvedTime, revenuePerHour, DateTime.Now);

                decimal anomalyCostImpact = 0m;
                if (isOverload)
                {
                    var excessKWh = Math.Max(log.PowerValue - log.ThresholdValue, 0m) * 0.25m / 1000m;
                    anomalyCostImpact = Math.Round(excessKWh * tariffPerKWh, 0);
                }

                var snapshot = await _context.AnomalyChartSnapshots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.AnomalyLogId == id);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = log.Id,
                        deviceKey = log.DeviceKey,
                        deviceId = log.DeviceId,
                        groupName = groupName,
                        location = location,
                        deviceCategory = category,
                        anomalyType = log.AnomalyType,
                        powerValue = log.PowerValue,
                        thresholdValue = log.ThresholdValue,
                        deviation = log.Deviation,
                        detectedTime = log.DetectedTime,
                        emaValue = log.EMAValue,
                        thresholdMode = log.ThresholdMode,
                        severity = log.Severity,
                        rootCause = log.RootCause,
                        recommendedAction = log.RecommendedAction,
                        notes = log.Notes,
                        acknowledged = log.Acknowledged ?? false,
                        acknowledgedBy = canViewOperationalMetadata ? log.AcknowledgedBy : null,
                        acknowledgedTime = log.AcknowledgedTime,
                        isResolved = log.IsResolved,
                        resolvedBy = canViewOperationalMetadata ? log.ResolvedBy : null,
                        resolvedTime = log.ResolvedTime,
                        durationMinutes = durationMinutes,
                        responseTimeMinutes = responseTimeMinutes,
                        revenueLoss = revenueLoss,
                        anomalyCostImpact = anomalyCostImpact,
                        operatorAction = canViewOperationalMetadata ? log.OperatorAction : null,
                        operatorNotes = canViewOperationalMetadata ? log.OperatorNotes : null,
                        chartSnapshot = snapshot == null ? null : new
                        {
                            detectedTime = snapshot.DetectedTime,
                            beforeDataJson = snapshot.BeforeDataJson,
                            afterDataJson = snapshot.AfterDataJson,
                            upperThreshold = snapshot.UpperThreshold,
                            lowerThreshold = snapshot.LowerThreshold,
                            emaValue = snapshot.EMAValue,
                            snapshotStatus = snapshot.SnapshotStatus
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // ACKNOWLEDGE ANOMALY (Operator / Admin)
        // ============================================
        [Authorize(Policy = "RequireOperator")]
        [HttpPost("anomaly-logs/{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeAnomaly(long id, [FromBody] AcknowledgeAnomalyRequest data)
        {
            try
            {
                var log = await _context.AnomalyLogs.FindAsync(id);
                if (log == null)
                    return NotFound(new { success = false, error = "Anomaly log not found" });

                var username = User.Identity.Name ?? "system";

                log.Acknowledged = true;
                log.AcknowledgedTime = DateTime.Now;
                log.AcknowledgedBy = username;

                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyAcknowledged,
                    log.DeviceKey,
                    $"Anomaly #{id} acknowledged by {username}",
                    true);

                return Ok(new { success = true, message = "Anomaly acknowledged" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // RESOLVE ANOMALY (Operator / Admin)
        // ============================================
        [Authorize(Policy = "RequireOperator")]
        [HttpPost("anomaly-logs/{id}/resolve")]
        public async Task<IActionResult> ResolveAnomaly(long id, [FromBody] ResolveAnomalyRequest data)
        {
            try
            {
                var log = await _context.AnomalyLogs.FindAsync(id);
                if (log == null)
                    return NotFound(new { success = false, error = "Anomaly log not found" });

                var username = User.Identity.Name ?? "system";

                log.IsResolved = true;
                log.ResolvedTime = DateTime.Now;
                log.ResolvedBy = username;
                log.OperatorAction = data?.Action;
                log.OperatorNotes = data?.Notes;

                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyResolved,
                    log.DeviceKey,
                    $"Anomaly #{id} resolved by {username}. Action: {data?.Action ?? "-"}. Notes: {data?.Notes ?? "-"}",
                    true);

                return Ok(new { success = true, message = "Anomaly resolved" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // UPDATE ANOMALY NOTES (Operator / Admin)
        // ============================================
        [Authorize(Policy = "RequireOperator")]
        [HttpPut("anomaly-logs/{id}/notes")]
        public async Task<IActionResult> UpdateAnomalyNotes(long id, [FromBody] UpdateAnomalyNotesRequest data)
        {
            try
            {
                var log = await _context.AnomalyLogs.FindAsync(id);
                if (log == null)
                    return NotFound(new { success = false, error = "Anomaly log not found" });

                var username = User.Identity.Name ?? "system";

                log.OperatorNotes = data?.Notes;
                log.OperatorAction = data?.Action ?? "notes_updated";

                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyActionTaken,
                    log.DeviceKey,
                    $"Anomaly #{id} notes updated by {username}",
                    true);

                return Ok(new { success = true, message = "Notes updated" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // DELETE SINGLE ANOMALY LOG (Admin only)
        // ============================================
        [Authorize(Policy = "RequireAdmin")]
        [HttpDelete("anomaly-logs/{id}")]
        public async Task<IActionResult> DeleteAnomalyLog(long id)
        {
            try
            {
                var log = await _context.AnomalyLogs.FindAsync(id);
                if (log == null)
                    return NotFound(new { success = false, error = "Anomaly log not found" });

                var deviceKey = log.DeviceKey;
                _context.AnomalyLogs.Remove(log);
                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyLogDeleted,
                    deviceKey,
                    $"Anomaly log #{id} deleted by {User.Identity.Name ?? "system"}",
                    true);

                return Ok(new { success = true, message = "Anomaly log deleted" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // CLEAR ALL ANOMALY LOGS (Admin only)
        // ============================================
        [Authorize(Policy = "RequireAdmin")]
        [HttpDelete("anomaly-logs/clear-all")]
        public async Task<IActionResult> ClearAllAnomalyLogs()
        {
            try
            {
                var allLogs = await _context.AnomalyLogs.ToListAsync();
                _context.AnomalyLogs.RemoveRange(allLogs);
                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyLogsCleared,
                    null,
                    $"All anomaly logs cleared by {User.Identity.Name ?? "system"}",
                    true);

                return Ok(new { success = true, message = "All anomaly logs cleared" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // ANOMALY DASHBOARD SUMMARY
        // ============================================
        [AllowAnonymous]
        [HttpGet("anomaly-dashboard")]
        public async Task<IActionResult> GetAnomalyDashboard()
        {
            try
            {
                var today = DateTime.Now.Date;
                var startOfDay = today;
                var endOfDay = today.AddDays(1);

                var totalQuery = _context.AnomalyLogs.AsNoTracking();
                var todayQuery = totalQuery.Where(x => x.DetectedTime >= startOfDay && x.DetectedTime < endOfDay);

                var totalCount = await totalQuery.CountAsync();
                var todayCount = await todayQuery.CountAsync();
                var unresolvedCount = await totalQuery.CountAsync(x => !x.IsResolved);
                var criticalCount = await totalQuery.CountAsync(x => x.Severity == "critical");
                var highCount = await totalQuery.CountAsync(x => x.Severity == "high");

                var topDevice = await totalQuery
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => new { DeviceKey = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

                var recentAnomalies = await totalQuery
                    .OrderByDescending(x => x.DetectedTime)
                    .Take(5)
                    .Select(x => new
                    {
                        x.Id,
                        x.DeviceKey,
                        x.AnomalyType,
                        x.Severity,
                        x.DetectedTime,
                        x.IsResolved
                    })
                    .ToListAsync();

                // Aggregate financial impact from existing anomaly logs
                var deviceSettingsDict = await _context.DeviceSettings
                    .ToDictionaryAsync(x => x.DeviceKey, x => new
                    {
                        x.DeviceCategory,
                        x.TariffPerKWh,
                        x.RevenuePerHour
                    });

                decimal totalAnomalyCostImpact = 0m;
                decimal totalRevenueLoss = 0m;
                decimal ongoingRevenueLoss = 0m;
                var financialDeviceKeys = new HashSet<string>();

                var allLogs = await totalQuery
                    .Select(x => new
                    {
                        x.DeviceKey,
                        x.AnomalyType,
                        x.PowerValue,
                        x.ThresholdValue,
                        x.DetectedTime,
                        x.IsResolved,
                        x.ResolvedTime
                    })
                    .ToListAsync();

                var calculationTime = DateTime.Now;
                foreach (var log in allLogs)
                {
                    var settings = deviceSettingsDict.ContainsKey(log.DeviceKey) ? deviceSettingsDict[log.DeviceKey] : null;
                    var tariff = settings?.TariffPerKWh ?? 1500m;
                    var revenuePerHour = settings?.RevenuePerHour ?? 0m;
                    var isDrop = log.AnomalyType == "DROP";
                    var isOverload = log.AnomalyType == "OVERLOAD";

                    if (isOverload)
                    {
                        var excessKWh = Math.Max(log.PowerValue - log.ThresholdValue, 0m) * 0.25m / 1000m;
                        var cost = Math.Round(excessKWh * tariff, 0);
                        totalAnomalyCostImpact += cost;
                        if (cost > 0) financialDeviceKeys.Add(log.DeviceKey);
                    }

                    if (isDrop && revenuePerHour > 0)
                    {
                        var loss = EstimateRevenueLoss(log.AnomalyType, log.DetectedTime, log.IsResolved, log.ResolvedTime, revenuePerHour, calculationTime);
                        totalRevenueLoss += loss;
                        if (!log.IsResolved)
                            ongoingRevenueLoss += loss;
                    }
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        total = totalCount,
                        today = todayCount,
                        unresolved = unresolvedCount,
                        critical = criticalCount,
                        high = highCount,
                        topDevice = topDevice?.DeviceKey,
                        topDeviceCount = topDevice?.Count ?? 0,
                        totalAnomalyCostImpact,
                        totalRevenueLoss,
                        ongoingRevenueLoss,
                        financialDeviceCount = financialDeviceKeys.Count,
                        recentAnomalies
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // ANOMALY TRENDS (7 atau 30 hari terakhir)
        // ============================================
        [AllowAnonymous]
        [HttpGet("anomaly-trends")]
        public async Task<IActionResult> GetAnomalyTrends([FromQuery] int days = 7)
        {
            try
            {
                if (days < 1) days = 7;
                if (days > 90) days = 90;

                var startDate = DateTime.Now.Date.AddDays(-days + 1);

                var logs = await _context.AnomalyLogs
                    .AsNoTracking()
                    .Where(x => x.DetectedTime >= startDate)
                    .Select(x => new { x.DetectedTime, x.AnomalyType, x.DeviceKey, x.Severity })
                    .ToListAsync();

                var trend = Enumerable.Range(0, days)
                    .Select(i => startDate.AddDays(i))
                    .Select(date => new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        total = logs.Count(x => x.DetectedTime.Date == date),
                        overload = logs.Count(x => x.DetectedTime.Date == date && x.AnomalyType == "OVERLOAD"),
                        drop = logs.Count(x => x.DetectedTime.Date == date && x.AnomalyType == "DROP"),
                        critical = logs.Count(x => x.DetectedTime.Date == date && x.Severity == "critical")
                    })
                    .ToList();

                return Ok(new { success = true, data = trend });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // ANOMALY DEVICE DISTRIBUTION (pie chart data)
        // ============================================
        [AllowAnonymous]
        [HttpGet("anomaly-device-distribution")]
        public async Task<IActionResult> GetAnomalyDeviceDistribution()
        {
            try
            {
                var deviceGroupNames = await _context.DeviceRegistry
                    .Where(x => x.GroupName != null && x.GroupName != "")
                    .ToDictionaryAsync(x => x.DeviceKey, x => x.GroupName);

                var distribution = await _context.AnomalyLogs
                    .AsNoTracking()
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => new { DeviceKey = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                var result = distribution.Select(d => new
                {
                    group = deviceGroupNames.ContainsKey(d.DeviceKey) ? deviceGroupNames[d.DeviceKey] : d.DeviceKey,
                    count = d.Count
                }).ToList();

                // Merge entries with the same group name
                var merged = result
                    .GroupBy(x => x.group)
                    .Select(g => new { group = g.Key, count = g.Sum(x => x.count) })
                    .OrderByDescending(x => x.count)
                    .ToList();

                return Ok(new { success = true, data = merged });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // GENERATE MONTHLY ANOMALY REPORT
        // ============================================
        [Authorize(Policy = "RequireOperator")]
        [HttpPost("anomaly-monthly-report/generate")]
        public async Task<IActionResult> GenerateMonthlyReport([FromBody] GenerateMonthlyReportRequest request)
        {
            try
            {
                var year = request?.Year ?? DateTime.Now.Year;
                var month = request?.Month ?? DateTime.Now.Month;

                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1);

                var logs = await _context.AnomalyLogs
                    .AsNoTracking()
                    .Where(x => x.DetectedTime >= startDate && x.DetectedTime < endDate)
                    .ToListAsync();

                var total = logs.Count;
                var overload = logs.Count(x => x.AnomalyType == "OVERLOAD");
                var drop = logs.Count(x => x.AnomalyType == "DROP");
                var affectedDevices = logs.Select(x => x.DeviceKey).Distinct().Count();
                var avgDeviation = total > 0 ? logs.Average(x => (double)x.Deviation) : 0;
                var criticalCount = logs.Count(x => string.Equals(x.Severity, "critical", StringComparison.OrdinalIgnoreCase));
                var unresolvedCount = logs.Count(x => !x.IsResolved);

                var topDevice = logs
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => new { DeviceKey = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                // Financial impact for the month
                var deviceSettingsDict = await _context.DeviceSettings
                    .ToDictionaryAsync(x => x.DeviceKey, x => new
                    {
                        x.DeviceCategory,
                        x.TariffPerKWh,
                        x.RevenuePerHour
                    });

                decimal totalAnomalyCostImpact = 0m;
                decimal totalRevenueLoss = 0m;
                decimal ongoingRevenueLoss = 0m;
                var financialDeviceKeys = new HashSet<string>();
                var rootCauseGroups = logs.Where(x => !string.IsNullOrWhiteSpace(x.RootCause))
                    .GroupBy(x => x.RootCause)
                    .Select(g => new { RootCause = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(3)
                    .ToList();

                var calculationTime = DateTime.Now;
                foreach (var log in logs)
                {
                    var settings = deviceSettingsDict.ContainsKey(log.DeviceKey) ? deviceSettingsDict[log.DeviceKey] : null;
                    var tariff = settings?.TariffPerKWh ?? 1500m;
                    var revenuePerHour = settings?.RevenuePerHour ?? 0m;
                    var isDrop = log.AnomalyType == "DROP";
                    var isOverload = log.AnomalyType == "OVERLOAD";

                    if (isOverload)
                    {
                        var excessKWh = Math.Max(log.PowerValue - log.ThresholdValue, 0m) * 0.25m / 1000m;
                        var cost = Math.Round(excessKWh * tariff, 0);
                        totalAnomalyCostImpact += cost;
                        if (cost > 0) financialDeviceKeys.Add(log.DeviceKey);
                    }

                    if (isDrop && revenuePerHour > 0)
                    {
                        var loss = EstimateRevenueLoss(log.AnomalyType, log.DetectedTime, log.IsResolved, log.ResolvedTime, revenuePerHour, calculationTime);
                        totalRevenueLoss += loss;
                        if (!log.IsResolved)
                            ongoingRevenueLoss += loss;
                        if (loss > 0) financialDeviceKeys.Add(log.DeviceKey);
                    }
                }

                var recommendations = new List<string>();
                if (overload > drop)
                    recommendations.Add("Anomali beban berlebih paling banyak terjadi. Tinjau kapasitas panel dan kurangi beban pada jam puncak.");
                if (drop > overload)
                    recommendations.Add("Gangguan perangkat paling banyak terjadi. Periksa kualitas koneksi dan catu daya.");
                if (affectedDevices > 1)
                    recommendations.Add($"Sebanyak {affectedDevices} perangkat terdampak. Lakukan pemeriksaan menyeluruh terhadap perangkat tersebut.");
                if (criticalCount > 0)
                    recommendations.Add("Terdapat anomali berkategori kritis. Segera lakukan penanganan.");
                if (totalAnomalyCostImpact > 0)
                    recommendations.Add($"Dampak biaya akibat beban berlebih pada bulan ini sebesar Rp {totalAnomalyCostImpact:N0}. Tinjau penggunaan beban untuk meningkatkan efisiensi.");
                if (totalRevenueLoss > 0)
                    recommendations.Add($"Estimasi kehilangan pendapatan pada bulan ini sebesar Rp {totalRevenueLoss:N0}. Prioritaskan pemulihan ketersediaan perangkat.");

                var report = await _context.AnomalyMonthlyReports
                    .OrderByDescending(x => x.GeneratedAt)
                    .FirstOrDefaultAsync(x => x.Year == year && x.Month == month);
                if (report == null)
                {
                    report = new AnomalyMonthlyReport { Year = year, Month = month };
                    _context.AnomalyMonthlyReports.Add(report);
                }
                report.TotalAnomalies = total;
                report.OverloadCount = overload;
                report.DropCount = drop;
                report.AffectedDevices = affectedDevices;
                report.AverageDeviation = (decimal)avgDeviation;
                report.TopAffectedDevice = topDevice?.DeviceKey;
                report.SummaryText = $"Laporan anomali untuk {startDate:MMMM yyyy} mencatat {total} kejadian. Sebanyak {criticalCount} kejadian berkategori kritis dan {unresolvedCount} kejadian belum diselesaikan.";
                report.Recommendations = string.Join("\n", recommendations);
                report.GeneratedBy = User.Identity.Name ?? "system";
                report.GeneratedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyMonthlyReportGenerated,
                    null,
                    $"Monthly anomaly report generated for {year}-{month} by {report.GeneratedBy}",
                    true);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        report.Id,
                        report.Year,
                        report.Month,
                        report.TotalAnomalies,
                        report.OverloadCount,
                        report.DropCount,
                        report.AffectedDevices,
                        report.AverageDeviation,
                        report.TopAffectedDevice,
                        topAffectedDeviceCount = topDevice?.Count ?? 0,
                        criticalCount,
                        unresolvedCount,
                        report.SummaryText,
                        report.Recommendations,
                        totalAnomalyCostImpact,
                        totalRevenueLoss,
                        ongoingRevenueLoss,
                        topRootCauses = rootCauseGroups,
                        report.GeneratedBy,
                        report.GeneratedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // GET MONTHLY ANOMALY REPORT
        // ============================================
        [AllowAnonymous]
        [HttpGet("anomaly-monthly-report")]
        public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                var report = await _context.AnomalyMonthlyReports
                    .AsNoTracking()
                    .OrderByDescending(x => x.GeneratedAt)
                    .FirstOrDefaultAsync(x => x.Year == year && x.Month == month);

                if (report == null)
                    return Ok(new { success = true, data = (object)null, message = "No report found" });

                // Recompute financials on-the-fly from raw logs
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1);
                var logs = await _context.AnomalyLogs
                    .AsNoTracking()
                    .Where(x => x.DetectedTime >= startDate && x.DetectedTime < endDate)
                    .ToListAsync();
                var criticalCount = logs.Count(x => string.Equals(x.Severity, "critical", StringComparison.OrdinalIgnoreCase));
                var unresolvedCount = logs.Count(x => !x.IsResolved);
                var topDevice = logs.GroupBy(x => x.DeviceKey)
                    .Select(g => new { DeviceKey = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                var deviceSettingsDict = await _context.DeviceSettings
                    .ToDictionaryAsync(x => x.DeviceKey, x => new
                    {
                        x.TariffPerKWh,
                        x.RevenuePerHour
                    });

                decimal totalAnomalyCostImpact = 0m;
                decimal totalRevenueLoss = 0m;
                decimal ongoingRevenueLoss = 0m;
                var calculationTime = DateTime.Now;
                foreach (var log in logs)
                {
                    var settings = deviceSettingsDict.ContainsKey(log.DeviceKey) ? deviceSettingsDict[log.DeviceKey] : null;
                    var tariff = settings?.TariffPerKWh ?? 1500m;
                    var revenuePerHour = settings?.RevenuePerHour ?? 0m;
                    var isDrop = log.AnomalyType == "DROP";
                    var isOverload = log.AnomalyType == "OVERLOAD";

                    if (isOverload)
                    {
                        var excessKWh = Math.Max(log.PowerValue - log.ThresholdValue, 0m) * 0.25m / 1000m;
                        totalAnomalyCostImpact += Math.Round(excessKWh * tariff, 0);
                    }

                    if (isDrop && revenuePerHour > 0)
                    {
                        var loss = EstimateRevenueLoss(log.AnomalyType, log.DetectedTime, log.IsResolved, log.ResolvedTime, revenuePerHour, calculationTime);
                        totalRevenueLoss += loss;
                        if (!log.IsResolved)
                            ongoingRevenueLoss += loss;
                    }
                }

                var rootCauseGroups = logs.Where(x => !string.IsNullOrWhiteSpace(x.RootCause))
                    .GroupBy(x => x.RootCause)
                    .Select(g => new { RootCause = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(3)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        report.Id,
                        report.Year,
                        report.Month,
                        report.TotalAnomalies,
                        report.OverloadCount,
                        report.DropCount,
                        report.AffectedDevices,
                        report.AverageDeviation,
                        report.TopAffectedDevice,
                        topAffectedDeviceCount = topDevice?.Count ?? 0,
                        criticalCount,
                        unresolvedCount,
                        report.SummaryText,
                        report.Recommendations,
                        totalAnomalyCostImpact,
                        totalRevenueLoss,
                        ongoingRevenueLoss,
                        topRootCauses = rootCauseGroups,
                        report.GeneratedBy,
                        report.GeneratedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // OPERATOR ACTIONS AUDIT
        // ============================================
        [Authorize(Policy = "RequireAdmin")]
        [HttpGet("anomaly-operator-actions")]
        public async Task<IActionResult> GetOperatorActions([FromQuery] string fromDate, [FromQuery] string toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var query = _context.SecurityAuditLogs
                    .AsNoTracking()
                    .Where(x => x.Action == SecurityAction.AnomalyAcknowledged
                        || x.Action == SecurityAction.AnomalyResolved
                        || x.Action == SecurityAction.AnomalyActionTaken
                        || x.Action == SecurityAction.AnomalyLogDeleted
                        || x.Action == SecurityAction.AnomalyLogsCleared);

                if (DateTime.TryParse(fromDate, out var from) && DateTime.TryParse(toDate, out var to))
                {
                    query = query.Where(x => x.Timestamp >= from && x.Timestamp < to.AddDays(1));
                }

                var total = await query.CountAsync();
                var auditItems = await query
                    .OrderByDescending(x => x.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new
                    {
                        x.Id,
                        x.Email,
                        Action = x.Action.ToString(),
                        x.TargetDevice,
                        x.Details,
                        x.Success,
                        x.Timestamp
                    })
                    .ToListAsync();

                var anomalyIds = auditItems
                    .Select(x => System.Text.RegularExpressions.Regex.Match(x.Details ?? string.Empty, @"Anomaly #(\d+)"))
                    .Where(match => match.Success)
                    .Select(match => long.Parse(match.Groups[1].Value))
                    .Distinct()
                    .ToList();
                var relatedAnomalies = anomalyIds.Count == 0
                    ? new List<AnomalyLog>()
                    : await _context.AnomalyLogs.AsNoTracking()
                        .Where(x => anomalyIds.Contains(x.Id))
                        .ToListAsync();
                var anomalyById = relatedAnomalies.ToDictionary(x => x.Id);
                var deviceKeys = relatedAnomalies.Select(x => x.DeviceKey)
                    .Concat(auditItems.Select(x => x.TargetDevice))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
                var groupNameRows = deviceKeys.Count == 0
                    ? new List<DeviceRegistry>()
                    : await _context.DeviceRegistry.AsNoTracking()
                        .Where(x => deviceKeys.Contains(x.DeviceKey))
                        .ToListAsync();
                var groupNames = groupNameRows
                    .Where(x => !string.IsNullOrWhiteSpace(x.GroupName))
                    .GroupBy(x => x.DeviceKey)
                    .ToDictionary(g => g.Key, g => g.First().GroupName);
                var items = auditItems.Select(x =>
                {
                    var match = System.Text.RegularExpressions.Regex.Match(x.Details ?? string.Empty, @"Anomaly #(\d+)");
                    AnomalyLog anomaly = null;
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var anomalyId))
                        anomalyById.TryGetValue(anomalyId, out anomaly);

                    var deviceKey = anomaly?.DeviceKey ?? x.TargetDevice;
                    var maintenanceAction = anomaly?.OperatorAction;
                    if (string.IsNullOrWhiteSpace(maintenanceAction))
                    {
                        var actionMatch = System.Text.RegularExpressions.Regex.Match(
                            x.Details ?? string.Empty,
                            @"Action:\s*(.*?)\.\s*Notes:",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (actionMatch.Success && actionMatch.Groups[1].Value != "-")
                            maintenanceAction = actionMatch.Groups[1].Value.Trim();
                    }
                    return new
                    {
                        x.Id,
                        x.Email,
                        x.Action,
                        groupName = groupNames.ContainsKey(deviceKey) ? groupNames[deviceKey] : deviceKey,
                        maintenanceAction = maintenanceAction ?? string.Empty,
                        x.Details,
                        x.Success,
                        x.Timestamp
                    };
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = items,
                    total,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // OPERATOR ACTIONS SUMMARY
        // ============================================
        [Authorize(Policy = "RequireAdmin")]
        [HttpGet("anomaly-operator-actions/summary")]
        public async Task<IActionResult> GetOperatorActionsSummary([FromQuery] string fromDate, [FromQuery] string toDate)
        {
            try
            {
                var query = _context.SecurityAuditLogs
                    .AsNoTracking()
                    .Where(x => x.Action == SecurityAction.AnomalyAcknowledged
                        || x.Action == SecurityAction.AnomalyResolved
                        || x.Action == SecurityAction.AnomalyActionTaken
                        || x.Action == SecurityAction.AnomalyLogDeleted
                        || x.Action == SecurityAction.AnomalyLogsCleared);

                if (DateTime.TryParse(fromDate, out var from) && DateTime.TryParse(toDate, out var to))
                {
                    query = query.Where(x => x.Timestamp >= from && x.Timestamp < to.AddDays(1));
                }

                var summary = await query
                    .GroupBy(x => x.Email)
                    .Select(g => new
                    {
                        Email = g.Key,
                        Total = g.Count(),
                        Acknowledged = g.Count(x => x.Action == SecurityAction.AnomalyAcknowledged),
                        Resolved = g.Count(x => x.Action == SecurityAction.AnomalyResolved),
                        ActionTaken = g.Count(x => x.Action == SecurityAction.AnomalyActionTaken),
                        Deleted = g.Count(x => x.Action == SecurityAction.AnomalyLogDeleted),
                        LogsCleared = g.Count(x => x.Action == SecurityAction.AnomalyLogsCleared)
                    })
                    .OrderByDescending(x => x.Total)
                    .ToListAsync();

                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // UPDATE CHART SNAPSHOT (After data - incremental)
        // No [Authorize] — consistent with log-anomaly endpoint;
        // logId in URL provides adequate access control
        // ============================================
        [HttpPost("anomaly-logs/{id}/chart-snapshot")]
        [Authorize(Policy = "RequireOperator")]
        public async Task<IActionResult> UpdateChartSnapshot(long id, [FromBody] ChartSnapshotRequest data)
        {
            try
            {
                var snapshot = await _context.AnomalyChartSnapshots
                    .FirstOrDefaultAsync(x => x.AnomalyLogId == id);

                if (snapshot == null)
                    return NotFound(new { success = false, error = "Chart snapshot not found" });

                if (string.Equals(snapshot.SnapshotStatus, "complete", StringComparison.OrdinalIgnoreCase))
                    return Ok(new { success = true, message = "Chart snapshot is already complete", status = snapshot.SnapshotStatus, afterCount = 50 });

                if (data?.After != null && data.After.Count > 0)
                {
                    // Merge idempotently by sample timestamp so retries and overlapping
                    // incremental uploads cannot replace a longer snapshot with a shorter one.
                    var merged = new List<ChartDataPointRequest>();
                    if (!string.IsNullOrEmpty(snapshot.AfterDataJson))
                    {
                        try
                        {
                            var existingList = JsonConvert.DeserializeObject<List<ChartDataPointRequest>>(snapshot.AfterDataJson);
                            if (existingList != null) merged.AddRange(existingList);
                        }
                        catch { }
                    }
                    merged.AddRange(data.After);
                    merged = merged
                        .Where(point => point != null)
                        .GroupBy(point => point.Timestamp.HasValue
                            ? point.Timestamp.Value.ToString("o", CultureInfo.InvariantCulture)
                            : JsonConvert.SerializeObject(point))
                        .Select(group => group.Last())
                        .OrderBy(point => point.Timestamp.HasValue ? 0 : 1)
                        .ThenBy(point => point.Timestamp)
                        .ToList();
                    if (merged.Count > 50)
                        merged = merged.Skip(merged.Count - 50).ToList();

                    snapshot.AfterDataJson = JsonConvert.SerializeObject(merged);
                    snapshot.UpdatedAt = DateTime.Now;

                    // "Complete" means all 50 post-anomaly samples are stored.
                    if (merged.Count >= 50)
                    {
                        snapshot.SnapshotStatus = "complete";
                    }
                    else
                    {
                        snapshot.SnapshotStatus = "partial";
                    }

                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Chart snapshot updated", status = snapshot.SnapshotStatus, afterCount = data?.After?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        [HttpGet("anomaly-logs/{id}/chart-snapshot")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAnomalyChartSnapshot(long id)
        {
            try
            {
                var snapshot = await _context.AnomalyChartSnapshots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.AnomalyLogId == id);

                if (snapshot == null)
                    return NotFound(new { success = false, error = "Chart snapshot not found" });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        detectedTime = snapshot.DetectedTime,
                        beforeDataJson = snapshot.BeforeDataJson,
                        afterDataJson = snapshot.AfterDataJson,
                        upperThreshold = snapshot.UpperThreshold,
                        lowerThreshold = snapshot.LowerThreshold,
                        emaValue = snapshot.EMAValue,
                        snapshotStatus = snapshot.SnapshotStatus
                    }
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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
                return SafeError(ex);
            }
        }

        // ============================================
        // DOWNTIME SETTINGS - SAVE
        // ============================================
        [HttpPost("save-downtime-settings")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // DOWNTIME STATUS (untuk frontend)
        // Frontend memanggil ini untuk tahu apakah lower line harus dimatikan
        // ============================================
        [HttpGet("downtime-status")]
        public async Task<IActionResult> GetDowntimeStatus([FromQuery] string category, [FromQuery] string deviceKey)
        {
            try
            {
                var downtime = await CheckDowntimePeriodAsync(category ?? null, deviceKey);

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
                return SafeError(ex);
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
                return SafeError(ex);
            }
        }

        // ============================================
        // PER-CATEGORY DOWNTIME SETTINGS - SAVE
        // ============================================
        [HttpPost("downtime-settings/{category}")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
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
                return SafeError(ex);
            }
        }

        // ============================================
        // DEVICE CATEGORY - GET (single device)
        // ============================================
        [HttpGet("device-category/{deviceKey}")]
        [AllowAnonymous] // Kategori device untuk tampilan dashboard (read-only)
        public async Task<IActionResult> GetDeviceCategory(string deviceKey)
        {
            try
            {
                var deviceSettings = await _context.DeviceSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

                var category = deviceSettings?.DeviceCategory;

                if (string.IsNullOrWhiteSpace(category))
                {
                    var setting = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == "DeviceCategory." + deviceKey);

                    category = setting?.SettingValue ?? "Billboard";
                }

                return Ok(new
                {
                    deviceKey = deviceKey,
                    category = category
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // DEVICE CATEGORY - SAVE (single device)
        // ============================================
        [HttpPost("device-category")]
        [Authorize(Policy = "RequireAdmin")]
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

                // Sync to per-device settings so both sources stay consistent
                try
                {
                    var deviceSettings = await _context.DeviceSettings
                        .FirstOrDefaultAsync(x => x.DeviceKey == data.deviceKey);

                    if (deviceSettings != null)
                    {
                        deviceSettings.DeviceCategory = data.category;
                        deviceSettings.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "Failed to sync device category to DeviceSettings for {DeviceKey}", data.deviceKey);
                }

                return Ok(new { success = true, message = "Device category saved", deviceKey = data.deviceKey, category = data.category });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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
                    new AppSettingsRecord { SettingKey = "Category.Billboard", SettingValue = "{\"icon\":\"??\",\"color\":\"#2196f3\",\"description\":\"Perangkat kategori Billboard � Digunakan untuk panel iklan billboard.\"}", UpdatedAt = DateTime.Now },
                    new AppSettingsRecord { SettingKey = "Category.Megatron", SettingValue = "{\"icon\":\"??\",\"color\":\"#ff9800\",\"description\":\"Perangkat kategori Megatron � Digunakan untuk panel megatron / LED display.\"}", UpdatedAt = DateTime.Now },
                    new AppSettingsRecord { SettingKey = "Category.NeonBox", SettingValue = "{\"icon\":\"??\",\"color\":\"#9c27b0\",\"description\":\"Perangkat kategori Neon Box � Digunakan untuk box neon sign.\"}", UpdatedAt = DateTime.Now }
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
        // HELPER: Sync device category to legacy AppSettingsRecord
        // ============================================
        private async Task SyncDeviceCategoryToAppSettingsAsync(string deviceKey, string category)
        {
            var key = "DeviceCategory." + deviceKey;
            var existing = await _context.AppSettingsRecords
                .FirstOrDefaultAsync(x => x.SettingKey == key);

            if (existing != null)
            {
                existing.SettingValue = category;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _context.AppSettingsRecords.Add(new AppSettingsRecord
                {
                    SettingKey = key,
                    SettingValue = category,
                    UpdatedAt = DateTime.Now
                });
            }
        }

        // ============================================
        // HELPER: Sync overlapping DeviceSettings fields to legacy AppSettingsRecord
        // DeviceSettings is the source of truth; pushes relevant fields to global settings
        // ============================================
        private async Task SyncDeviceSettingsToAppSettingsAsync(string deviceKey, DeviceSettings settings)
        {
            try
            {
                // Category sync
                if (!string.IsNullOrWhiteSpace(settings.DeviceCategory))
                {
                    await SyncDeviceCategoryToAppSettingsAsync(deviceKey, settings.DeviceCategory);
                }

                // ControlMode sync
                if (!string.IsNullOrWhiteSpace(settings.ControlMode))
                {
                    await UpsertAppSettingAsync("Control.Mode", settings.ControlMode);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync device settings to AppSettingsRecord for {DeviceKey}", deviceKey);
            }
        }

        private async Task UpsertAppSettingAsync(string key, string value)
        {
            var existing = await _context.AppSettingsRecords
                .FirstOrDefaultAsync(x => x.SettingKey == key);

            if (existing != null)
            {
                existing.SettingValue = value;
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _context.AppSettingsRecords.Add(new AppSettingsRecord
                {
                    SettingKey = key,
                    SettingValue = value,
                    UpdatedAt = DateTime.Now
                });
            }
        }

        // ============================================
        // CATEGORIES - GET ALL (with metadata)
        // ============================================
        [HttpGet("categories")]
        [AllowAnonymous] // Daftar kategori untuk filter dashboard (read-only)
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
                    string icon = "?", color = "#607d8b", description = "";
                    try
                    {
                        if (!string.IsNullOrEmpty(x.SettingValue))
                        {
                            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(x.SettingValue);
                            icon = parsed.icon?.ToString() ?? "?";
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
                return SafeError(ex);
            }
        }

        // ============================================
        // CATEGORY - ADD
        // ============================================
        [HttpPost("categories")]
        [Authorize(Policy = "RequireAdmin")]
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
                    icon = string.IsNullOrWhiteSpace(data.icon) ? "?" : data.icon,
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
                return SafeError(ex);
            }
        }

        // ============================================
        // CATEGORY - UPDATE
        // ============================================
        [HttpPut("categories/{name}")]
        [Authorize(Policy = "RequireAdmin")]
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
                    icon = string.IsNullOrWhiteSpace(data.icon) ? "?" : data.icon,
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
                return SafeError(ex);
            }
        }

        // ============================================
        // CATEGORY - DELETE
        // ============================================
        [HttpDelete("categories/{name}")]
        [Authorize(Policy = "RequireAdmin")]
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

                var reassignedDeviceKeys = new List<string>();
                foreach (var dc in deviceCategoryKeys)
                {
                    dc.SettingValue = "Billboard";
                    dc.UpdatedAt = DateTime.Now;
                    reassignedDeviceKeys.Add(dc.SettingKey.Replace("DeviceCategory.", ""));
                }

                // Sync reassignment to per-device settings
                if (reassignedDeviceKeys.Any())
                {
                    var deviceSettingsToUpdate = await _context.DeviceSettings
                        .Where(x => reassignedDeviceKeys.Contains(x.DeviceKey) && x.DeviceCategory == name)
                        .ToListAsync();

                    foreach (var ds in deviceSettingsToUpdate)
                    {
                        ds.DeviceCategory = "Billboard";
                        ds.UpdatedAt = DateTime.UtcNow;
                    }
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
                return SafeError(ex);
            }
        }

        // ============================================
        // DEVICE CATEGORIES - GET ALL
        // ============================================
        [HttpGet("device-categories")]
        [AllowAnonymous] // Daftar kategori device untuk tampilan (read-only)
        public async Task<IActionResult> GetAllDeviceCategories()
        {
            try
            {
                var validCategories = await GetValidCategoriesAsync();

                // Legacy AppSettingsRecord categories
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("DeviceCategory."))
                    .ToDictionaryAsync(x => x.SettingKey.Replace("DeviceCategory.", ""), x => x.SettingValue);

                // Per-device settings override legacy records
                var deviceSettings = await _context.DeviceSettings
                    .AsNoTracking()
                    .Where(x => !string.IsNullOrEmpty(x.DeviceCategory))
                    .ToListAsync();

                foreach (var ds in deviceSettings)
                {
                    settings[ds.DeviceKey] = ds.DeviceCategory;
                }

                return Ok(new
                {
                    categories = validCategories,
                    devices = settings
                });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // NOTIFICATION SETTINGS - GET (Public — for anomaly service, no credentials)
        // ============================================
        [HttpGet("get-notification-settings")]
        [AllowAnonymous] // Publik by design: hanya toggle/jadwal, tanpa kredensial
        public async Task<IActionResult> GetNotificationSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification") || x.SettingKey.StartsWith("Anomaly."))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Ok(new
                {
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
                return StatusCode(500, new { error = "Terjadi kesalahan internal." });
            }
        }

        // ============================================
        // NOTIFICATION SETTINGS - GET FULL (Admin only — includes credentials)
        // ============================================
        [HttpGet("get-notification-settings-full")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> GetNotificationSettingsFull()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Notification") || x.SettingKey.StartsWith("Anomaly."))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                string senderPassword = GetString(settings, "Notification.SenderPassword", "");
                if (!string.IsNullOrEmpty(senderPassword) && !senderPassword.StartsWith("ENC:"))
                    senderPassword = _encryption.Decrypt(senderPassword) ?? senderPassword;

                return Ok(new
                {
                    smtpServer = GetString(settings, "Notification.SmtpServer", "smtp.gmail.com"),
                    smtpPort = GetInt(settings, "Notification.SmtpPort", 587),
                    senderEmail = GetString(settings, "Notification.SenderEmail", ""),
                    senderPassword = senderPassword,
                    masterAdminEmail = await _context.ApplicationUsers
                        .Where(x => x.IsMasterAdmin && x.IsActive)
                        .Select(x => x.Email)
                        .FirstOrDefaultAsync() ?? "",
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
                    anomalyCheckInterval = GetInt(settings, "Anomaly.CheckInterval", 30),
                    anomalyMaxConfirmations = GetInt(settings, "Anomaly.MaxConfirmations", 3),
                    anomalyCooldownTime = GetInt(settings, "Anomaly.CooldownTime", 60),
                    settingsReloadInterval = GetInt(settings, "Anomaly.SettingsReloadInterval", 60)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GET-NOTIF-FULL] Error: {0}", ex.Message);
                return StatusCode(500, new { error = "Terjadi kesalahan internal." });
            }
        }

        // ============================================
        // ANOMALY SETTINGS - SAVE
        // ============================================
        [HttpPost("save-anomaly-settings")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // ANOMALY STATE - GET (persist confirmation counts & cooldown state)
        // ============================================
        [HttpGet("get-anomaly-state")]
        public async Task<IActionResult> GetAnomalyState([FromQuery] string scopeId)
        {
            try
            {
                if (!Guid.TryParse(scopeId, out var parsedScopeId))
                    return BadRequest(new { success = false, error = "A valid state scope is required" });
                var statePrefix = "AnomalyState." + parsedScopeId.ToString("N") + ".";
                var expiredState = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("AnomalyState.") && x.UpdatedAt.HasValue
                        && x.UpdatedAt.Value < DateTime.Now.AddDays(-30))
                    .ToListAsync();
                if (expiredState.Count > 0)
                {
                    _context.AppSettingsRecords.RemoveRange(expiredState);
                    await _context.SaveChangesAsync();
                }
                var perDeviceState = await _context.AppSettingsRecords.AsNoTracking()
                    .Where(x => x.SettingKey.StartsWith(statePrefix + "Counts.") || x.SettingKey.StartsWith(statePrefix + "Cooldown."))
                    .ToListAsync();
                var counts = new JObject();
                var cooldown = new JObject();
                foreach (var state in perDeviceState)
                {
                    var countPrefix = statePrefix + "Counts.";
                    var isCount = state.SettingKey.StartsWith(countPrefix, StringComparison.Ordinal);
                    var prefix = isCount ? countPrefix : statePrefix + "Cooldown.";
                    var deviceKey = state.SettingKey.Substring(prefix.Length);
                    try
                    {
                        var parsed = JObject.Parse(state.SettingValue ?? "{}");
                        if (isCount)
                        {
                            foreach (var property in parsed.Properties()) counts[property.Name] = property.Value.DeepClone();
                        }
                        else if (parsed.HasValues)
                        {
                            cooldown[deviceKey] = parsed.DeepClone();
                        }
                    }
                    catch (JsonException) { /* Ignore malformed per-device state and continue loading other devices. */ }
                }

                return Ok(new
                {
                    success = true,
                    confirmationCounts = counts.ToString(Formatting.None),
                    cooldownState = cooldown.ToString(Formatting.None)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GET-ANOMALY-STATE] Error: {0}", ex.Message);
                return SafeError(ex);
            }
        }

        // ============================================
        // ANOMALY STATE - SAVE (persist confirmation counts & cooldown state)
        // ============================================
        [HttpPost("save-anomaly-state")]
        [Authorize(Policy = "RequireOperator")]
        public async Task<IActionResult> SaveAnomalyState([FromBody] AnomalyStateData data)
        {
            try
            {
                if (data == null) return BadRequest(new { success = false, error = "State payload is required" });
                if (!Guid.TryParse(data.scopeId, out var parsedScopeId))
                    return BadRequest(new { success = false, error = "A valid state scope is required" });
                var statePrefix = "AnomalyState." + parsedScopeId.ToString("N") + ".";
                var counts = JObject.Parse(string.IsNullOrWhiteSpace(data.confirmationCounts) ? "{}" : data.confirmationCounts);
                var cooldown = JObject.Parse(string.IsNullOrWhiteSpace(data.cooldownState) ? "{}" : data.cooldownState);
                var deviceKeys = counts.Properties().Select(x =>
                    x.Name.EndsWith("_OVERLOAD", StringComparison.Ordinal) ? x.Name.Substring(0, x.Name.Length - 9) :
                    x.Name.EndsWith("_DROP", StringComparison.Ordinal) ? x.Name.Substring(0, x.Name.Length - 5) : string.Empty)
                    .Concat(cooldown.Properties().Select(x => x.Name))
                    .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 20)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                foreach (var deviceKey in deviceKeys)
                {
                    var deviceCounts = new JObject();
                    foreach (var property in counts.Properties().Where(x => x.Name.StartsWith(deviceKey + "_", StringComparison.Ordinal)))
                        deviceCounts[property.Name] = property.Value.DeepClone();
                    var deviceCooldown = cooldown[deviceKey] as JObject ?? new JObject();
                    await UpsertAnomalyStateRecordAsync(statePrefix + "Counts." + deviceKey, deviceCounts.ToString(Formatting.None));
                    await UpsertAnomalyStateRecordAsync(statePrefix + "Cooldown." + deviceKey, deviceCooldown.ToString(Formatting.None));
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError("[SAVE-ANOMALY-STATE] Error: {0}", ex.Message);
                return SafeError(ex);
            }
        }

        private async Task UpsertAnomalyStateRecordAsync(string key, string value)
        {
            if (value.Length > 500) throw new InvalidOperationException("Per-device anomaly state exceeds the AppSettings value limit");
            var record = await _context.AppSettingsRecords.FirstOrDefaultAsync(x => x.SettingKey == key);
            if (record == null)
            {
                _context.AppSettingsRecords.Add(new AppSettingsRecord
                {
                    SettingKey = key,
                    SettingValue = value,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                record.SettingValue = value;
                record.UpdatedAt = DateTime.Now;
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
                return SafeError(ex);
            }
        }

        // ============================================
        // CLEAR ANOMALY LOGS BY DEVICE (Admin only)
        // ============================================
        [Authorize(Policy = "RequireAdmin")]
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

                await LogSecurityActionAsync(
                    SecurityAction.AnomalyLogsCleared,
                    deviceKey,
                    $"Anomaly logs for device {deviceKey} cleared by {User.Identity.Name ?? "system"}",
                    true);

                return Ok(new { success = true, message = string.Format("Cleared {0} logs for {1}", logs.Count, deviceKey) });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // RESET CHART DATA BY DEVICE (Admin only)
        // ============================================
        [Authorize(Policy = "RequireAdmin")]
        [HttpDelete("reset-chart-data/{deviceKey}")]
        public async Task<IActionResult> ResetChartDataByDevice(string deviceKey)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceKey))
                    return BadRequest(new { success = false, message = "DeviceKey is required" });

                var dataRows = await _context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey)
                    .ToListAsync();

                if (dataRows.Count == 0)
                    return Ok(new { success = true, message = "No chart data found for " + deviceKey, deletedCount = 0 });

                _context.KWH_Monitoring.RemoveRange(dataRows);

                // Clear anomaly alert state for this device
                var activeAlertKey = "AnomalyAlert.Active." + deviceKey;
                var activeAlert = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == activeAlertKey);
                if (activeAlert != null)
                    _context.AppSettingsRecords.Remove(activeAlert);

                var perDeviceAnomalyState = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("AnomalyState.")
                        && (x.SettingKey.EndsWith(".Counts." + deviceKey) || x.SettingKey.EndsWith(".Cooldown." + deviceKey)))
                    .ToListAsync();
                if (perDeviceAnomalyState.Count > 0)
                    _context.AppSettingsRecords.RemoveRange(perDeviceAnomalyState);

                // Remove this device from anomaly confirmation counts & cooldown state
                var countsRecord = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "AnomalyState.ConfirmationCounts");
                if (countsRecord != null && !string.IsNullOrEmpty(countsRecord.SettingValue))
                {
                    try
                    {
                        var countsObj = JObject.Parse(countsRecord.SettingValue);
                        if (countsObj.ContainsKey(deviceKey))
                        {
                            countsObj.Remove(deviceKey);
                            countsRecord.SettingValue = countsObj.ToString(Formatting.None);
                            countsRecord.UpdatedAt = DateTime.Now;
                        }
                    }
                    catch { /* ignore parse errors */ }
                }

                var cooldownRecord = await _context.AppSettingsRecords
                    .FirstOrDefaultAsync(x => x.SettingKey == "AnomalyState.CooldownState");
                if (cooldownRecord != null && !string.IsNullOrEmpty(cooldownRecord.SettingValue))
                {
                    try
                    {
                        var cooldownObj = JObject.Parse(cooldownRecord.SettingValue);
                        if (cooldownObj.ContainsKey(deviceKey))
                        {
                            cooldownObj.Remove(deviceKey);
                            cooldownRecord.SettingValue = cooldownObj.ToString(Formatting.None);
                            cooldownRecord.UpdatedAt = DateTime.Now;
                        }
                    }
                    catch { /* ignore parse errors */ }
                }

                await _context.SaveChangesAsync();

                await LogSecurityActionAsync(
                    SecurityAction.ChartDataReset,
                    deviceKey,
                    $"Chart data reset for device {deviceKey}: {dataRows.Count} rows deleted by {User.Identity.Name ?? "system"}",
                    true);

                _logger.LogInformation("Chart data reset for device {DeviceKey}: {Count} rows deleted", deviceKey, dataRows.Count);

                return Ok(new { success = true, message = $"Reset {dataRows.Count} data points for {deviceKey}", deletedCount = dataRows.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting chart data for device {DeviceKey}", deviceKey);
                return SafeError(ex);
            }
        }

        // ============================================
        // NOTIFICATION SETTINGS - SAVE
        // ============================================
        [HttpPost("save-notification-settings")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveNotificationSettings([FromBody] NotificationSettingsData data)
        {
            try
            {
                _logger.LogInformation("[SAVE-NOTIF] Received settings - SendInstantAlert={0}, SendHourlyReport={1}, SendDailyReport={2}, SendMonthlyReport={3}, DailyTime={4}, MonthlyDay={5}, MonthlyTime={6}",
                    data.sendInstantAlert, data.sendHourlyReport, data.sendDailyReport, data.sendMonthlyReport,
                    data.dailyReportTime, data.monthlyReportDay, data.monthlyReportTime);

                // Encrypt SMTP password before storing
                var encryptedPassword = data.senderPassword ?? "";
                if (!string.IsNullOrEmpty(encryptedPassword) && !encryptedPassword.StartsWith("ENC:"))
                {
                    encryptedPassword = "ENC:" + _encryption.Encrypt(encryptedPassword);
                }

                var settingsToSave = new Dictionary<string, string>
                {
                    { "Notification.SmtpServer", data.smtpServer ?? "smtp.gmail.com" },
                    { "Notification.SmtpPort", data.smtpPort.ToString() },
                    { "Notification.SenderEmail", data.senderEmail ?? "" },
                    { "Notification.SenderPassword", encryptedPassword },
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
                        _logger.LogInformation("[SAVE-NOTIF] Updated key: {0}", kvp.Key);
                    }
                    else
                    {
                        _context.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = kvp.Key,
                            SettingValue = kvp.Value,
                            UpdatedAt = DateTime.Now
                        });
                        _logger.LogInformation("[SAVE-NOTIF] Added key: {0}", kvp.Key);
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("[SAVE-NOTIF] DB save successful. Total keys: {0}", settingsToSave.Count);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError("[SAVE-NOTIF] Error saving: {0}", ex.Message);
                return SafeError(ex);
            }
        }

        // ============================================
        // TRANSFER MASTER ADMIN (Only current master admin)
        // ============================================
        [HttpPost("transfer-master-admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TransferMasterAdmin([FromBody] TransferMasterAdminData data)
        {
            try
            {
                var currentMasterAdmin = await _context.ApplicationUsers
                    .FirstOrDefaultAsync(x => x.IsMasterAdmin && x.IsActive);
                var masterEmail = currentMasterAdmin?.Email ?? string.Empty;

                if (currentMasterAdmin == null ||
                    !string.Equals(User.Identity.Name, masterEmail, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[TRANSFER-MASTER] Blocked: {0} is not the current master admin", User.Identity.Name);

                    try
                    {
                        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        int? userId = int.TryParse(userIdClaim, out var parsedId) ? (int?)parsedId : null;
                        _context.SecurityAuditLogs.Add(new SecurityAuditLog
                        {
                            UserId = userId,
                            Email = User.Identity.Name ?? "unknown",
                            Action = SecurityAction.MasterAdminTransferBlocked,
                            Success = false,
                            Details = $"Unauthorized master admin transfer attempt to {data?.newMasterAdminEmail}",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                            UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                            Timestamp = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                    catch { /* audit log failure should not break the main flow */ }

                    return StatusCode(403, new { error = "Hanya master admin yang dapat memindahkan hak master admin." });
                }

                if (string.IsNullOrEmpty(data?.newMasterAdminEmail))
                {
                    return BadRequest(new { error = "Email master admin baru tidak boleh kosong." });
                }

                var targetUser = await _context.ApplicationUsers
                    .FirstOrDefaultAsync(x => x.Email == data.newMasterAdminEmail && x.IsActive);
                if (targetUser == null)
                {
                    return BadRequest(new { error = "User dengan email tersebut tidak ditemukan atau tidak aktif." });
                }

                if (targetUser.Role != UserRoles.Admin)
                {
                    return BadRequest(new { error = "Master admin hanya dapat dipindahkan ke user dengan role Admin." });
                }

                // Toggle IsMasterAdmin flags
                currentMasterAdmin.IsMasterAdmin = false;
                targetUser.IsMasterAdmin = true;

                await _context.SaveChangesAsync();

                // Security audit log
                try
                {
                    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    int? userId = int.TryParse(userIdClaim, out var parsedId) ? (int?)parsedId : null;
                    _context.SecurityAuditLogs.Add(new SecurityAuditLog
                    {
                        UserId = userId,
                        Email = User.Identity.Name ?? "unknown",
                        Action = SecurityAction.MasterAdminTransferred,
                        Success = true,
                        Details = $"Master admin transferred from {masterEmail} to {data.newMasterAdminEmail}",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
                catch { /* audit log failure should not break the main flow */ }

                _logger.LogInformation("[TRANSFER-MASTER] Master admin transferred from {0} to {1}",
                    masterEmail, data.newMasterAdminEmail);

                return Ok(new { success = true, message = "Master admin berhasil dipindahkan." });
            }
            catch (Exception ex)
            {
                _logger.LogError("[TRANSFER-MASTER] Error: {0}", ex.Message);
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST EMAIL
        // ============================================
        [HttpPost("test-email-notification")]
        [Authorize(Policy = "RequireAdmin")]
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

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    return BadRequest(new { error = "Email SMTP settings not configured (sender email/password)" });
                }

                var notificationService = HttpContext.RequestServices.GetService(typeof(NotificationService)) as NotificationService;
                var recipients = await notificationService.GetReportRecipientEmailsAsync();

                if (recipients.Count == 0)
                {
                    return BadRequest(new { error = "No active Operator/Admin users found to send test email to" });
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

                    foreach (var recipient in recipients)
                    {
                        mailMessage.To.Add(recipient.Trim());
                    }

                    await client.SendMailAsync(mailMessage);
                }

                return Ok(new { success = true, message = string.Format("Test email sent successfully to {0} recipient(s)", recipients.Count) });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST WHATSAPP NOTIFICATION
        // ============================================
        [HttpPost("test-whatsapp-notification")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // RESCAN PANELS
        // ============================================
        [HttpPost("rescan-panels")]
        [Authorize(Policy = "RequireOperator")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST INSTANT ALERT (Realtime)
        // Mengirim instant alert contoh berdasarkan anomali terbaru
        // ============================================
        [HttpPost("test-instant-alert")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST HOURLY REPORT (Realtime)
        // Mengirim laporan jam ini (1 jam terakhir dari saat ini)
        // ============================================
        [HttpPost("test-hourly-report")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST DAILY REPORT (Realtime)
        // Mengirim laporan hari ini (dari jam 00:00 sampai sekarang)
        // ============================================
        [HttpPost("test-daily-report")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // TEST MONTHLY REPORT (Realtime)
        // Mengirim laporan bulan ini (dari tanggal 1 sampai sekarang)
        // ============================================
        [HttpPost("test-monthly-report")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // HELPER METHODS
        // ============================================
        private string GetDeviceStatus(
            KWHData data,
            Dictionary<string, DeviceSettings> deviceSettingsDict,
            Dictionary<string, TitikLokasi> erpCapacityMap)
        {
            DeviceSettings ds;
            var hasSettings = deviceSettingsDict.TryGetValue(data.DeviceKey, out ds);
            TitikLokasi erp = null;
            var hasErpCapacity = erpCapacityMap != null && erpCapacityMap.TryGetValue(data.DeviceKey, out erp);
            var maxCap = hasErpCapacity
                ? PanelViewModel.CalculateMaxCapacityWatt(erp.DayaVA, data.Cos_Phi ?? 0m)
                : 0m;
            var normalThresh = hasSettings && ds.LoadNormalThreshold > 0 ? ds.LoadNormalThreshold : 30;
            var mediumThresh = hasSettings && ds.LoadMediumThreshold > 0 ? ds.LoadMediumThreshold : 70;

            if (maxCap <= 0) return "NORMAL";
            var loadPercent = Math.Min(((data.Daya_Watt ?? 0m) / maxCap) * 100, 100m);
            if (loadPercent > mediumThresh) return "HIGH";
            if (loadPercent > normalThresh) return "MEDIUM";
            return "NORMAL";
        }

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
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // WABLAS - SAVE SETTINGS
        // ============================================
        [HttpPost("wablas/settings")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex);
            }
        }

        // ============================================
        // WABLAS - TEST CONNECTION
        // ============================================
        [HttpPost("wablas/test")]
        [Authorize(Policy = "RequireAdmin")]
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
                return SafeError(ex, "WablasTest");
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
                return SafeError(ex);
            }
        }

        // ============================================
        // AI CHATBOT SETTINGS
        // ============================================
        [HttpGet("chatbot-settings")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> GetChatbotSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("Chatbot."))
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                string apiKey = "";
                if (settings.TryGetValue("Chatbot.ApiKey", out var encryptedKey) && !string.IsNullOrEmpty(encryptedKey))
                {
                    var decrypted = _encryption.Decrypt(encryptedKey);
                    apiKey = decrypted ?? "";
                }

                string model = settings.TryGetValue("Chatbot.Model", out var m) ? m : "qwen-plus-2025-04-28";
                string apiUrl = settings.TryGetValue("Chatbot.ApiUrl", out var u) ? u : "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";
                bool isConfigured = settings.TryGetValue("Chatbot.ApiKey", out var ck) && !string.IsNullOrEmpty(ck);

                return Ok(new
                {
                    apiKey = apiKey,
                    model = model,
                    apiUrl = apiUrl,
                    isConfigured = isConfigured
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chatbot settings");
                return SafeError(ex);
            }
        }

        [HttpPost("chatbot-settings")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> SaveChatbotSettings([FromBody] ChatbotSettingsData data)
        {
            try
            {
                if (data == null)
                    return BadRequest(new { error = "Invalid data" });

                var settingsDict = new Dictionary<string, string>();

                if (!string.IsNullOrWhiteSpace(data.ApiKey))
                {
                    settingsDict["Chatbot.ApiKey"] = _encryption.Encrypt(data.ApiKey.Trim());
                }
                if (!string.IsNullOrWhiteSpace(data.Model))
                {
                    settingsDict["Chatbot.Model"] = data.Model.Trim();
                }
                if (!string.IsNullOrWhiteSpace(data.ApiUrl))
                {
                    settingsDict["Chatbot.ApiUrl"] = data.ApiUrl.Trim();
                }

                foreach (var kvp in settingsDict)
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

                // Invalidate cache so chatbot picks up new settings immediately
                _cache.Remove("ChatbotConfig_Cached");

                _logger.LogInformation("Chatbot settings saved successfully. Key configured: {HasKey}", !string.IsNullOrWhiteSpace(data.ApiKey));
                return Ok(new { success = true, message = "Chatbot settings saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save chatbot settings");
                return SafeError(ex);
            }
        }

        [HttpPost("test-chatbot-connection")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> TestChatbotConnection([FromBody] ChatbotSettingsData data)
        {
            try
            {
                string apiKey = data?.ApiKey?.Trim() ?? "";
                string model = data?.Model?.Trim() ?? "qwen-plus-2025-04-28";
                string apiUrl = data?.ApiUrl?.Trim() ?? "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";

                // If no apiKey provided in test data, try loading from DB
                if (string.IsNullOrEmpty(apiKey))
                {
                    var existingKey = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == "Chatbot.ApiKey");

                    if (existingKey != null && !string.IsNullOrEmpty(existingKey.SettingValue))
                    {
                        var decrypted = _encryption.Decrypt(existingKey.SettingValue);
                        if (!string.IsNullOrEmpty(decrypted))
                            apiKey = decrypted;
                    }

                    if (string.IsNullOrEmpty(apiKey))
                    {
                        // Fallback to appsettings.json
                        var config = _serviceProvider.GetRequiredService<IConfiguration>();
                        apiKey = config["Qwen:ApiKey"] ?? "";
                    }
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    return Ok(new { success = false, message = "API Key belum dikonfigurasi" });
                }

                // If no model provided, try loading from DB
                if (string.IsNullOrEmpty(data?.Model))
                {
                    var existingModel = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == "Chatbot.Model");
                    if (existingModel != null)
                        model = existingModel.SettingValue;
                }

                // If no apiUrl provided, try loading from DB
                if (string.IsNullOrEmpty(data?.ApiUrl))
                {
                    var existingUrl = await _context.AppSettingsRecords
                        .FirstOrDefaultAsync(x => x.SettingKey == "Chatbot.ApiUrl");
                    if (existingUrl != null)
                        apiUrl = existingUrl.SettingValue;
                }

                var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
                var client = httpClientFactory.CreateClient("QwenClient");

                var payload = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "user", content = "Halo, ini tes koneksi. Jawab singkat: 'OK'." }
                    }
                };

                string jsonString = JsonConvert.SerializeObject(payload);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                httpRequest.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                httpRequest.Headers.Add("Authorization", "Bearer " + apiKey);

                var response = await client.SendAsync(httpRequest);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = JObject.Parse(responseString);
                    var choices = responseJson["choices"] as JArray;
                    string replyText = "";
                    if (choices != null && choices.Count > 0 && choices[0]["message"] != null)
                    {
                        replyText = choices[0]["message"]["content"]?.ToString() ?? "";
                    }

                    return Ok(new
                    {
                        success = true,
                        message = "Berhasil terhubung ke AI Model: " + model,
                        reply = replyText.Trim()
                    });
                }
                else
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Gagal terhubung ke AI API (HTTP " + (int)response.StatusCode + ")",
                        detail = responseString
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save device settings");
                return Ok(new { success = false, message = "Terjadi kesalahan internal." });
            }
        }

        // ============================================
        // DEVICE SETTINGS API
        // ============================================
        [HttpGet("device-settings")]
        [AllowAnonymous] // Effective settings untuk tampilan dashboard (read-only)
        public async Task<IActionResult> GetAllDeviceSettings()
        {
            try
            {
                var allSettings = await _deviceSettingsService.GetAllEffectiveAsync();
                var erpCapacityMap = await _titikLokasiService.GetByDeviceKeysAsync(allSettings.Keys);
                foreach (var item in allSettings)
                {
                    if (erpCapacityMap.TryGetValue(item.Key, out var erpCapacity))
                    {
                        item.Value.InstalledCapacityVA = erpCapacity.DayaVA;

                        var latest = await _context.KWH_Monitoring
                            .Where(x => x.DeviceKey == item.Key)
                            .OrderByDescending(x => x.Waktu_Server)
                            .Select(x => new { x.Cos_Phi })
                            .FirstOrDefaultAsync();

                        item.Value.EffectiveMaxCapacityWatt = PanelViewModel.CalculateMaxCapacityWatt(
                            erpCapacity.DayaVA,
                            latest?.Cos_Phi ?? 0m);
                        item.Value.MaxCapacity = item.Value.EffectiveMaxCapacityWatt;
                    }
                    else
                    {
                        item.Value.InstalledCapacityVA = 0m;
                        item.Value.EffectiveMaxCapacityWatt = 0m;
                        item.Value.MaxCapacity = 0m;
                    }
                }
                return Ok(new { success = true, settings = allSettings });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        [HttpGet("device-settings/{deviceKey}")]
        public async Task<IActionResult> GetDeviceSettings(string deviceKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deviceKey))
                    return BadRequest(new { success = false, message = "DeviceKey is required" });

                var settings = await _deviceSettingsService.GetEffectiveAsync(deviceKey);
                var erpCapacityMap = await _titikLokasiService.GetByDeviceKeysAsync(new[] { deviceKey });
                if (erpCapacityMap.TryGetValue(deviceKey, out var erpCapacity))
                {
                    settings.InstalledCapacityVA = erpCapacity.DayaVA;
                    var latest = await _context.KWH_Monitoring
                        .Where(x => x.DeviceKey == deviceKey)
                        .OrderByDescending(x => x.Waktu_Server)
                        .Select(x => new { x.Cos_Phi })
                        .FirstOrDefaultAsync();
                    settings.EffectiveMaxCapacityWatt = PanelViewModel.CalculateMaxCapacityWatt(
                        erpCapacity.DayaVA,
                        latest?.Cos_Phi ?? 0m);
                    settings.MaxCapacity = settings.EffectiveMaxCapacityWatt;
                }
                else
                {
                    settings.InstalledCapacityVA = 0m;
                    settings.EffectiveMaxCapacityWatt = 0m;
                    settings.MaxCapacity = 0m;
                }
                return Ok(new { success = true, deviceKey, settings });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        [HttpPost("device-settings/{deviceKey}")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> SaveDeviceSettings(string deviceKey, [FromBody] DeviceSettingsRequest data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deviceKey))
                    return BadRequest(new { success = false, message = "DeviceKey is required" });

                if (data == null)
                    return BadRequest(new { success = false, message = "Data is required" });

                var settings = new DeviceSettings
                {
                    MaxCapacity = data.MaxCapacity,
                    DeviceCategory = data.DeviceCategory,
                    DowntimeEnabled = data.DowntimeEnabled,
                    DowntimeStart = data.DowntimeStart,
                    DowntimeEnd = data.DowntimeEnd,
                    TariffPerKWh = data.TariffPerKWh,
                    LoadNormalThreshold = data.LoadNormalThreshold,
                    LoadMediumThreshold = data.LoadMediumThreshold,
                    EmaUpperThreshold = data.EmaUpperThreshold,
                    EmaLowerThreshold = data.EmaLowerThreshold,
                    EmaFibUpper = data.EmaFibUpper,
                    EmaFibLower = data.EmaFibLower,
                    ControlMode = data.ControlMode,
                    TariffWBP = data.TariffWBP,
                    TariffLWBP = data.TariffLWBP,
                    WbpStartHour = data.WbpStartHour,
                    WbpEndHour = data.WbpEndHour,
                    BudgetKWh = data.BudgetKWh,
                    SurfaceArea = data.SurfaceArea,
                    RevenuePerHour = data.RevenuePerHour
                };

                await _deviceSettingsService.SaveAsync(deviceKey, settings);

                // Sync overlapping fields to legacy AppSettingsRecord (DeviceSettings = source of truth)
                await SyncDeviceSettingsToAppSettingsAsync(deviceKey, settings);

                return Ok(new { success = true, message = "Device settings saved successfully" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
            }
        }

        [HttpPost("device-settings/bulk")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> BulkSaveDeviceSettings([FromBody] BulkDeviceSettingsRequest data)
        {
            try
            {
                if (data?.Settings == null || !data.Settings.Any())
                    return BadRequest(new { success = false, message = "Settings list is required" });

                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            foreach (var item in data.Settings)
                            {
                                if (string.IsNullOrWhiteSpace(item.DeviceKey)) continue;

                                var settings = new DeviceSettings
                                {
                                    MaxCapacity = item.MaxCapacity,
                                    DeviceCategory = item.DeviceCategory,
                                    DowntimeEnabled = item.DowntimeEnabled,
                                    DowntimeStart = item.DowntimeStart,
                                    DowntimeEnd = item.DowntimeEnd,
                                    TariffPerKWh = item.TariffPerKWh,
                                    LoadNormalThreshold = item.LoadNormalThreshold,
                                    LoadMediumThreshold = item.LoadMediumThreshold,
                                    EmaUpperThreshold = item.EmaUpperThreshold,
                                    EmaLowerThreshold = item.EmaLowerThreshold,
                                    EmaFibUpper = item.EmaFibUpper,
                                    EmaFibLower = item.EmaFibLower,
                                    ControlMode = item.ControlMode,
                                    TariffWBP = item.TariffWBP,
                                    TariffLWBP = item.TariffLWBP,
                                    WbpStartHour = item.WbpStartHour,
                                    WbpEndHour = item.WbpEndHour,
                                    BudgetKWh = item.BudgetKWh,
                                    SurfaceArea = item.SurfaceArea,
                                    RevenuePerHour = item.RevenuePerHour
                                };

                                await _deviceSettingsService.SaveAsync(item.DeviceKey, settings);

                                // Sync overlapping fields to legacy AppSettingsRecord
                                await SyncDeviceSettingsToAppSettingsAsync(item.DeviceKey, settings);
                            }

                            await _context.SaveChangesAsync();
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                });

                return Ok(new { success = true, message = "Device settings saved successfully" });
            }
            catch (Exception ex)
            {
                return SafeError(ex);
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

    public class MqttConnectionData
    {
        public string broker { get; set; } = string.Empty;
        public int port { get; set; } = 1883;
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public bool useTls { get; set; } = false;
        public string clientId { get; set; } = string.Empty;
        public string clientCertPassword { get; set; } = string.Empty;
        public bool skipCertValidation { get; set; } = false;
    }

    public class MqttCertificateRequest
    {
        public string certificateType { get; set; } = string.Empty;
    }

    public class RelayControlRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string RCValue { get; set; } = string.Empty;
        public bool Pulse { get; set; } = false;
        public string OtpCode { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }

    public class DeviceControlModeRequest
    {
        public string DeviceKey { get; set; } = string.Empty;
        public string Mode { get; set; } = "OnOff";
    }

    public class EmaSettingsData
    {
        public int emaPeriod { get; set; } = 20;
        public string emaMode { get; set; } = "manual";
        public int emaUpperThreshold { get; set; } = 0;
        public int emaLowerThreshold { get; set; } = 0;
        public double emaFibUpper { get; set; } = 0;
        public double emaFibLower { get; set; } = 0;
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
        public string masterAdminEmail { get; set; }
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

    public class TransferMasterAdminData
    {
        public string newMasterAdminEmail { get; set; }
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
        public string scopeId { get; set; }
    }

    public class AnomalyLogRequest
    {
        public string DeviceKey { get; set; } = string.Empty;
        public DateTime? SampleTime { get; set; }
        public string DeviceId { get; set; }
        public string AnomalyType { get; set; } = string.Empty;
        public decimal PowerValue { get; set; }
        public decimal ThresholdValue { get; set; }
        public decimal Deviation { get; set; }
        public decimal? EMAValue { get; set; }
        public string ThresholdMode { get; set; }
        public ChartSnapshotRequest ChartSnapshot { get; set; }
    }

    internal class VerifiedAnomalySample
    {
        public bool IsValid { get; set; }
        public string Error { get; set; }
        public DateTime SampleTime { get; set; }
        public DateTime DetectedTime { get; set; }
        public decimal PowerValue { get; set; }
        public decimal ThresholdValue { get; set; }
        public decimal Deviation { get; set; }
        public decimal? EmaValue { get; set; }
        public string AnomalyType { get; set; }
        public string ThresholdMode { get; set; }
        public string Notes { get; set; }

        public static VerifiedAnomalySample Invalid(string error)
        {
            return new VerifiedAnomalySample { IsValid = false, Error = error };
        }
    }

    internal class DeviceSilenceAssessment
    {
        public DateTime LastSampleTime { get; set; }
        public decimal LastPowerValue { get; set; }
        public double ExpectedIntervalSeconds { get; set; }
        public double SilenceThresholdSeconds { get; set; }
        public double SecondsSinceLastSample { get; set; }
        public bool IsSilent { get; set; }
    }

    public class ChartSnapshotRequest
    {
        public List<ChartDataPointRequest> Before { get; set; } = new List<ChartDataPointRequest>();
        public List<ChartDataPointRequest> After { get; set; } = new List<ChartDataPointRequest>();
        public decimal UpperThreshold { get; set; }
        public decimal LowerThreshold { get; set; }
        public decimal? EMAValue { get; set; }
        public bool IsFinal { get; set; }
    }

    public class ChartDataPointRequest
    {
        public DateTime? Timestamp { get; set; }
        public decimal Power { get; set; }
        public decimal? Upper { get; set; }
        public decimal? Lower { get; set; }
        public decimal? EMA { get; set; }
    }

    public class DateFilterRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }

    public class UsageStatisticsBatchRequest : DateFilterRequest
    {
        public List<string> DeviceKeys { get; set; } = new List<string>();
        public string DeviceKey { get; set; }
    }

    internal class UsageStatisticsBatchAnomaly
    {
        public string DeviceKey { get; set; }
        public string AnomalyType { get; set; }
        public decimal PowerValue { get; set; }
        public decimal ThresholdValue { get; set; }
        public decimal Deviation { get; set; }
        public string Severity { get; set; }
        public DateTime DetectedTime { get; set; }
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
        public int Take { get; set; } = 10;
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
        public string icon { get; set; } = "?";
        public string color { get; set; } = "#607d8b";
        public string description { get; set; } = "";
    }

    public class ResetAnomalyAlertRequest
    {
        public string DeviceKey { get; set; } = string.Empty;
    }

    public class ChatbotSettingsData
    {
        public string ApiKey { get; set; }
        public string Model { get; set; } = "qwen-plus-2025-04-28";
        public string ApiUrl { get; set; } = "https://dashscope-intl.aliyuncs.com/compatible-mode/v1/chat/completions";
    }

    public class AcknowledgeAnomalyRequest
    {
        public string Notes { get; set; }
    }

    public class ResolveAnomalyRequest
    {
        public string Action { get; set; }
        public string Notes { get; set; }
    }

    public class UpdateAnomalyNotesRequest
    {
        public string Action { get; set; }
        public string Notes { get; set; }
    }

    public class GenerateMonthlyReportRequest
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
    }

    // ============================================
    // DEVICE SETTINGS
    // ============================================
    public class DeviceSettingsRequest
    {
        public string DeviceKey { get; set; }
        public decimal MaxCapacity { get; set; }
        public string DeviceCategory { get; set; }
        public bool DowntimeEnabled { get; set; }
        public TimeSpan DowntimeStart { get; set; }
        public TimeSpan DowntimeEnd { get; set; }
        public decimal TariffPerKWh { get; set; }
        public int LoadNormalThreshold { get; set; }
        public int LoadMediumThreshold { get; set; }
        public int EmaUpperThreshold { get; set; }
        public int EmaLowerThreshold { get; set; }
        public double EmaFibUpper { get; set; }
        public double EmaFibLower { get; set; }
        public string ControlMode { get; set; }
        public decimal TariffWBP { get; set; }
        public decimal TariffLWBP { get; set; }
        public int WbpStartHour { get; set; } = 18;
        public int WbpEndHour { get; set; } = 22;
        public decimal BudgetKWh { get; set; }
        public decimal SurfaceArea { get; set; }
        public decimal RevenuePerHour { get; set; }
    }

    public class BulkDeviceSettingsRequest
    {
        public List<DeviceSettingsRequest> Settings { get; set; }
    }
}




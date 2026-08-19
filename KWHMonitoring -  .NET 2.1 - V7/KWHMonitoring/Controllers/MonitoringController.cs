using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KWHMonitoring.Models;

namespace KWHMonitoring.Controllers
{
    public class MonitoringController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static AppSettings _appSettings = new AppSettings();

        public MonitoringController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // PANEL MONITORING (Halaman Utama)
        // ============================================
        public async Task<IActionResult> Index()
        {
            try
            {
                var viewModel = new DashboardViewModel
                {
                    Settings = _appSettings
                };

                var latestData = await _context.KWH_Monitoring
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                    .ToListAsync();

                var validData = latestData.Where(x => x != null).ToList();

                foreach (var data in validData)
                {
                    if (data == null) continue;

                    viewModel.Panels.Add(new PanelViewModel
                    {
                        DeviceKey = data.DeviceKey,
                        DeviceId = data.DeviceId,
                        GroupName = data.GroupName,
                        Waktu_Server = data.Waktu_Server,
                        Volt_R = data.Volt_R ?? 0m,
                        Volt_S = data.Volt_S ?? 0m,
                        Volt_T = data.Volt_T ?? 0m,
                        Amp_R = data.Amp_R ?? 0m,
                        Amp_S = data.Amp_S ?? 0m,
                        Amp_T = data.Amp_T ?? 0m,
                        Cos_Phi = data.Cos_Phi ?? 0m,
                        Daya_Watt = data.Daya_Watt ?? 0m,
                        TotalW1M_Wh = data.TotalW1M_Wh ?? 0m,
                        Energi_Aktif_Wh = data.Energi_Aktif_Wh ?? 0m,
                        Total_Energy_Wh = data.Total_Energy_Wh ?? 0m,
                        Frekuensi_Hz = data.Frekuensi_Hz ?? 0m
                    });
                }

                if (validData.Any())
                {
                    viewModel.TotalStats = new TotalStatistics
                    {
                        TotalDaya = validData.Sum(x => x.Daya_Watt) ?? 0m,
                        TotalEnergy = validData.Sum(x => x.Total_Energy_Wh) ?? 0m,
                        TotalW1M = validData.Sum(x => x.TotalW1M_Wh) ?? 0m,
                        TotalEnergiAktif = validData.Sum(x => x.Energi_Aktif_Wh) ?? 0m,
                        ActivePanels = validData.Count,
                        AvgPowerFactor = validData.Average(x => x.Cos_Phi) ?? 0m,
                        AvgVoltage = validData.Average(x => x.AvgVoltage),
                        AvgFrequency = validData.Average(x => x.Frekuensi_Hz) ?? 0m
                    };
                }

                ViewBag.TariffPerKWh = _appSettings.TariffPerKWh ?? 1500m;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                ViewBag.TariffPerKWh = 1500m;
                return View(new DashboardViewModel());
            }
        }

        // ============================================
        // ALL CHARTS
        // ============================================
        public async Task<IActionResult> Charts()
        {
            try
            {
                var viewModel = new DashboardViewModel
                {
                    Settings = _appSettings
                };

                var latestData = await _context.KWH_Monitoring
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                    .ToListAsync();

                var validData = latestData.Where(x => x != null).ToList();

                foreach (var data in validData)
                {
                    if (data == null) continue;

                    viewModel.Panels.Add(new PanelViewModel
                    {
                        DeviceKey = data.DeviceKey,
                        DeviceId = data.DeviceId,
                        GroupName = data.GroupName,
                        Waktu_Server = data.Waktu_Server,
                        Volt_R = data.Volt_R ?? 0m,
                        Volt_S = data.Volt_S ?? 0m,
                        Volt_T = data.Volt_T ?? 0m,
                        Amp_R = data.Amp_R ?? 0m,
                        Amp_S = data.Amp_S ?? 0m,
                        Amp_T = data.Amp_T ?? 0m,
                        Cos_Phi = data.Cos_Phi ?? 0m,
                        Daya_Watt = data.Daya_Watt ?? 0m,
                        TotalW1M_Wh = data.TotalW1M_Wh ?? 0m,
                        Energi_Aktif_Wh = data.Energi_Aktif_Wh ?? 0m,
                        Total_Energy_Wh = data.Total_Energy_Wh ?? 0m,
                        Frekuensi_Hz = data.Frekuensi_Hz ?? 0m
                    });
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return View(new DashboardViewModel());
            }
        }

        // ============================================
        // ANOMALY LOGS PAGE
        // ============================================
        public async Task<IActionResult> AnomalyLogs()
        {
            try
            {
                var viewModel = new DashboardViewModel();

                var latestData = await _context.KWH_Monitoring
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => g.OrderByDescending(x => x.Waktu_Server).FirstOrDefault())
                    .ToListAsync();

                var validData = latestData.Where(x => x != null).ToList();

                foreach (var data in validData)
                {
                    if (data == null) continue;

                    viewModel.Panels.Add(new PanelViewModel
                    {
                        DeviceKey = data.DeviceKey,
                        DeviceId = data.DeviceId,
                        GroupName = data.GroupName,
                        Waktu_Server = data.Waktu_Server,
                        Volt_R = data.Volt_R ?? 0m,
                        Volt_S = data.Volt_S ?? 0m,
                        Volt_T = data.Volt_T ?? 0m,
                        Amp_R = data.Amp_R ?? 0m,
                        Amp_S = data.Amp_S ?? 0m,
                        Amp_T = data.Amp_T ?? 0m,
                        Cos_Phi = data.Cos_Phi ?? 0m,
                        Daya_Watt = data.Daya_Watt ?? 0m,
                        TotalW1M_Wh = data.TotalW1M_Wh ?? 0m,
                        Energi_Aktif_Wh = data.Energi_Aktif_Wh ?? 0m,
                        Total_Energy_Wh = data.Total_Energy_Wh ?? 0m,
                        Frekuensi_Hz = data.Frekuensi_Hz ?? 0m
                    });
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return View(new DashboardViewModel());
            }
        }

        // ============================================
        // USAGE STATISTICS
        // ============================================
        public async Task<IActionResult> UsageStatistics()
        {
            var tariff = await _context.AppSettingsRecords
                .Where(x => x.SettingKey == "Tariff.PerKWh" || x.SettingKey == "TariffPerKWh")
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync();

            ViewBag.TariffPerKWh = decimal.TryParse(tariff, out var result) ? result : 1500m;
            return View();
        }

        // SETTINGS PAGE
        public async Task<IActionResult> Settings()
        {
            var settings = await _context.AppSettingsRecords
                .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

            ViewBag.CurrentSettings = settings;
            return View();
        }

        // ============================================
        // DETAILS
        // ============================================
        public async Task<IActionResult> Details(string deviceKey)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceKey))
                    return RedirectToAction(nameof(Index));

                var latestData = await _context.KWH_Monitoring
                    .Where(x => x.DeviceKey == deviceKey)
                    .OrderByDescending(x => x.Waktu_Server)
                    .FirstOrDefaultAsync();

                if (latestData == null)
                    return NotFound();

                var viewModel = new PanelViewModel
                {
                    DeviceKey = latestData.DeviceKey,
                    DeviceId = latestData.DeviceId,
                    GroupName = latestData.GroupName,
                    Waktu_Server = latestData.Waktu_Server,
                    Volt_R = latestData.Volt_R ?? 0m,
                    Volt_S = latestData.Volt_S ?? 0m,
                    Volt_T = latestData.Volt_T ?? 0m,
                    Amp_R = latestData.Amp_R ?? 0m,
                    Amp_S = latestData.Amp_S ?? 0m,
                    Amp_T = latestData.Amp_T ?? 0m,
                    Cos_Phi = latestData.Cos_Phi ?? 0m,
                    Daya_Watt = latestData.Daya_Watt ?? 0m,
                    TotalW1M_Wh = latestData.TotalW1M_Wh ?? 0m,
                    Energi_Aktif_Wh = latestData.Energi_Aktif_Wh ?? 0m,
                    Total_Energy_Wh = latestData.Total_Energy_Wh ?? 0m,
                    Frekuensi_Hz = latestData.Frekuensi_Hz ?? 0m
                };

                ViewBag.DeviceKey = deviceKey;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // ============================================
        // HISTORY
        // ============================================
        public async Task<IActionResult> History(string deviceKey, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            try
            {
                var query = _context.KWH_Monitoring.AsQueryable();

                if (!string.IsNullOrEmpty(deviceKey))
                    query = query.Where(x => x.DeviceKey == deviceKey);

                if (fromDate.HasValue)
                    query = query.Where(x => x.Waktu_Server >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(x => x.Waktu_Server <= toDate.Value.AddDays(1));

                var totalCount = await query.CountAsync();

                var pageSize = 10;
                var data = await query
                    .OrderByDescending(x => x.Waktu_Server)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.DeviceKeys = await _context.KWH_Monitoring
                    .Select(x => x.DeviceKey)
                    .Distinct()
                    .ToListAsync();

                ViewBag.CurrentDeviceKey = deviceKey ?? "";
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-ddTHH:mm") ?? "";
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-ddTHH:mm") ?? "";
                ViewBag.TotalRecords = totalCount;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                return View(data);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return View(new List<KWHData>());
            }
        }

        // ============================================
        // EXPORT CSV
        // ============================================
        public async Task<IActionResult> ExportCSV(string deviceKey)
        {
            try
            {
                var query = _context.KWH_Monitoring.AsQueryable();

                if (!string.IsNullOrEmpty(deviceKey))
                    query = query.Where(x => x.DeviceKey == deviceKey);

                var data = await query
                    .OrderByDescending(x => x.Waktu_Server)
                    .Take(50000)
                    .ToListAsync();

                var sb = new StringBuilder();
                sb.AppendLine("Id,DeviceKey,DeviceId,GroupName,Waktu_Device,Waktu_Server,Volt_R,Volt_S,Volt_T,Amp_R,Amp_S,Amp_T,Cos_Phi,Daya_Watt,TotalW1M_Wh,Energi_Aktif_Wh,Total_Energy_Wh,Frekuensi_Hz");

                foreach (var item in data)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "\"{0}\",\"{1}\",\"{2}\",\"{3}\"," +
                        "\"{4:yyyy-MM-dd HH:mm:ss}\",\"{5:yyyy-MM-dd HH:mm:ss}\"," +
                        "{6},{7},{8},{9},{10},{11}," +
                        "{12},{13},{14},{15},{16},{17}",
                        item.Id, CsvEscape(item.DeviceKey), CsvEscape(item.DeviceId), CsvEscape(item.GroupName),
                        item.Waktu_Device ?? DateTime.MinValue, item.Waktu_Server,
                        item.Volt_R ?? 0m, item.Volt_S ?? 0m, item.Volt_T ?? 0m, item.Amp_R ?? 0m, item.Amp_S ?? 0m, item.Amp_T ?? 0m,
                        item.Cos_Phi ?? 0m, item.Daya_Watt ?? 0m, item.TotalW1M_Wh ?? 0m, item.Energi_Aktif_Wh ?? 0m, item.Total_Energy_Wh ?? 0m, item.Frekuensi_Hz ?? 0m));
                }

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var fileName = string.Format("KWH_Monitoring_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now);
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return RedirectToAction(nameof(History));
            }
        }

        // ============================================
        // SETTINGS
        // ============================================
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(AppSettings settings)
        {
            try
            {
                var settingsDict = new Dictionary<string, string>
                {
                    { "emaPeriod", settings.EmaPeriod.ToString() },
                    { "emaMode", settings.EmaMode },
                    { "emaUpperThreshold", settings.EmaUpperThreshold.ToString() },
                    { "emaLowerThreshold", settings.EmaLowerThreshold.ToString() },
                    { "emaFibUpper", settings.EmaFibUpper.ToString() },
                    { "emaFibLower", settings.EmaFibLower.ToString() },
                    { "emaShowLine", settings.EmaShowLine.ToString() },
                    { "emaShowThresholds", settings.EmaShowThresholds.ToString() },
                    { "useInitial100ForEma", settings.useInitial100ForEma.ToString() },
                    { "refreshInterval", settings.RefreshInterval.ToString() },
                    { "chartDataPoints", settings.ChartDataPoints.ToString() },
                    { "Tariff.PerKWh", settings.TariffPerKWh?.ToString() ?? "1500" }
                };

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
                _appSettings = settings;
                return Json(new { success = true, message = "Settings updated" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var settings = await _context.AppSettingsRecords
                    .Where(x => x.SettingKey.StartsWith("ema") ||
                               x.SettingKey.StartsWith("refresh") ||
                               x.SettingKey.StartsWith("chart") ||
                               x.SettingKey == "Tariff.PerKWh" ||
                               x.SettingKey == "TariffPerKWh" ||
                               x.SettingKey == "useInitial100ForEma")
                    .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);

                return Json(new
                {
                    emaPeriod = GetIntSetting(settings, "emaPeriod", 20),
                    emaMode = GetStrSetting(settings, "emaMode", "manual"),
                    emaUpperThreshold = GetIntSetting(settings, "emaUpperThreshold", 30),
                    emaLowerThreshold = GetIntSetting(settings, "emaLowerThreshold", 50),
                    emaFibUpper = GetDblSetting(settings, "emaFibUpper", 1.618),
                    emaFibLower = GetDblSetting(settings, "emaFibLower", 0.618),
                    emaShowLine = GetBoolSetting(settings, "emaShowLine", true),
                    emaShowThresholds = GetBoolSetting(settings, "emaShowThresholds", true),
                    useInitial100ForEma = GetBoolSetting(settings, "useInitial100ForEma", false),
                    refreshInterval = GetIntSetting(settings, "refreshInterval", 10),
                    chartDataPoints = GetIntSetting(settings, "chartDataPoints", 20),
                    TariffPerKWh = GetDecSetting(settings, "Tariff.PerKWh", 1500m)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // RESCAN PANELS
        // ============================================
        [HttpPost]
        public async Task<IActionResult> RescanPanels()
        {
            try
            {
                var panels = await _context.KWH_Monitoring
                    .Select(x => x.DeviceKey)
                    .Distinct()
                    .ToListAsync();

                return Json(new { success = true, panels = panels, count = panels.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("\"", "\"\"");
        }

        private static int GetIntSetting(Dictionary<string, string> dict, string key, int defaultValue)
        {
            return dict.TryGetValue(key, out var value) && int.TryParse(value, out var result) ? result : defaultValue;
        }

        private static double GetDblSetting(Dictionary<string, string> dict, string key, double defaultValue)
        {
            return dict.TryGetValue(key, out var value) && double.TryParse(value, out var result) ? result : defaultValue;
        }

        private static bool GetBoolSetting(Dictionary<string, string> dict, string key, bool defaultValue)
        {
            return dict.TryGetValue(key, out var value) && bool.TryParse(value, out var result) ? result : defaultValue;
        }

        private static string GetStrSetting(Dictionary<string, string> dict, string key, string defaultValue)
        {
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private static decimal GetDecSetting(Dictionary<string, string> dict, string key, decimal defaultValue)
        {
            return dict.TryGetValue(key, out var value) && decimal.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}

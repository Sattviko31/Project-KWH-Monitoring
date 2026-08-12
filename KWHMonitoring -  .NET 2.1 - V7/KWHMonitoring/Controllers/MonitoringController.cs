using System;
using System.Collections.Generic;
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
                        Volt_R = data.Volt_R,
                        Volt_S = data.Volt_S,
                        Volt_T = data.Volt_T,
                        Amp_R = data.Amp_R,
                        Amp_S = data.Amp_S,
                        Amp_T = data.Amp_T,
                        Cos_Phi = data.Cos_Phi,
                        Daya_Watt = data.Daya_Watt,
                        TotalW1M_Wh = data.TotalW1M_Wh,
                        Energi_Aktif_Wh = data.Energi_Aktif_Wh,
                        Total_Energy_Wh = data.Total_Energy_Wh,
                        Frekuensi_Hz = data.Frekuensi_Hz
                    });
                }

                if (validData.Any())
                {
                    viewModel.TotalStats = new TotalStatistics
                    {
                        TotalDaya = validData.Sum(x => x.Daya_Watt),
                        TotalEnergy = validData.Sum(x => x.Total_Energy_Wh),
                        TotalW1M = validData.Sum(x => x.TotalW1M_Wh),
                        TotalEnergiAktif = validData.Sum(x => x.Energi_Aktif_Wh),
                        ActivePanels = validData.Count,
                        AvgPowerFactor = validData.Average(x => x.Cos_Phi),
                        AvgVoltage = validData.Average(x => x.AvgVoltage),
                        AvgFrequency = validData.Average(x => x.Frekuensi_Hz)
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
                        Volt_R = data.Volt_R,
                        Volt_S = data.Volt_S,
                        Volt_T = data.Volt_T,
                        Amp_R = data.Amp_R,
                        Amp_S = data.Amp_S,
                        Amp_T = data.Amp_T,
                        Cos_Phi = data.Cos_Phi,
                        Daya_Watt = data.Daya_Watt,
                        TotalW1M_Wh = data.TotalW1M_Wh,
                        Energi_Aktif_Wh = data.Energi_Aktif_Wh,
                        Total_Energy_Wh = data.Total_Energy_Wh,
                        Frekuensi_Hz = data.Frekuensi_Hz
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
                        Volt_R = data.Volt_R,
                        Volt_S = data.Volt_S,
                        Volt_T = data.Volt_T,
                        Amp_R = data.Amp_R,
                        Amp_S = data.Amp_S,
                        Amp_T = data.Amp_T,
                        Cos_Phi = data.Cos_Phi,
                        Daya_Watt = data.Daya_Watt,
                        TotalW1M_Wh = data.TotalW1M_Wh,
                        Energi_Aktif_Wh = data.Energi_Aktif_Wh,
                        Total_Energy_Wh = data.Total_Energy_Wh,
                        Frekuensi_Hz = data.Frekuensi_Hz
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

        // [UNUSED] NotificationSettings - hanya redirect ke Settings, tidak punya view sendiri
        public IActionResult NotificationSettings()
        {
            return RedirectToAction(nameof(Settings));
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
                    Volt_R = latestData.Volt_R,
                    Volt_S = latestData.Volt_S,
                    Volt_T = latestData.Volt_T,
                    Amp_R = latestData.Amp_R,
                    Amp_S = latestData.Amp_S,
                    Amp_T = latestData.Amp_T,
                    Cos_Phi = latestData.Cos_Phi,
                    Daya_Watt = latestData.Daya_Watt,
                    TotalW1M_Wh = latestData.TotalW1M_Wh,
                    Energi_Aktif_Wh = latestData.Energi_Aktif_Wh,
                    Total_Energy_Wh = latestData.Total_Energy_Wh,
                    Frekuensi_Hz = latestData.Frekuensi_Hz
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

                var pageSize = 100;
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
        // [BUG] Tidak ada CSV escaping. Jika field mengandung koma atau quote,
        // CSV akan rusak. Sebaiknya wrap setiap field dengan double-quotes.
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
                    sb.AppendLine(string.Format("{0},{1},{2},{3}," +
                        "{4:yyyy-MM-dd HH:mm:ss},{5:yyyy-MM-dd HH:mm:ss}," +
                        "{6},{7},{8},{9},{10},{11}," +
                        "{12},{13},{14},{15},{16},{17}",
                        item.Id, item.DeviceKey, item.DeviceId, item.GroupName,
                        item.Waktu_Device, item.Waktu_Server,
                        item.Volt_R, item.Volt_S, item.Volt_T, item.Amp_R, item.Amp_S, item.Amp_T,
                        item.Cos_Phi, item.Daya_Watt, item.TotalW1M_Wh, item.Energi_Aktif_Wh, item.Total_Energy_Wh, item.Frekuensi_Hz));
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
        // [BUG] _appSettings adalah static in-memory, tidak sync dengan DB.
        // MonitoringController.UsageStatistics() dan Settings() sudah baca dari DB,
        // tapi UpdateSettings/GetSettings masih pakai static variable.
        [HttpPost]
        public IActionResult UpdateSettings(AppSettings settings)
        {
            try
            {
                _appSettings = settings;
                return Json(new { success = true, message = "Settings updated" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // [UNUSED] GetSettings() mengembalikan static _appSettings yang tidak sync dengan DB.
        // ApiController sudah punya get-system-settings yang baca dari DB.
        public IActionResult GetSettings()
        {
            return Json(_appSettings);
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
    }
}

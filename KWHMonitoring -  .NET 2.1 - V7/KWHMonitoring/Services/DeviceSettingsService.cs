using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public interface IDeviceSettingsService
    {
        Task<DeviceSettings> GetEffectiveAsync(string deviceKey);
        Task<Dictionary<string, DeviceSettings>> GetAllEffectiveAsync();
        Task<DeviceSettings> GetOrCreateAsync(string deviceKey);
        Task SaveAsync(string deviceKey, DeviceSettings settings);
    }

    public class DeviceSettingsService : IDeviceSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITitikLokasiService _titikLokasiService;

        public DeviceSettingsService(ApplicationDbContext context, ITitikLokasiService titikLokasiService)
        {
            _context = context;
            _titikLokasiService = titikLokasiService;
        }

        public async Task<DeviceSettings> GetEffectiveAsync(string deviceKey)
        {
            var deviceSettings = await _context.DeviceSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

            if (deviceSettings == null)
            {
                deviceSettings = new DeviceSettings { DeviceKey = deviceKey };
            }

            // MaxCapacity, EMA thresholds, BudgetKWh, SurfaceArea: 0 means not configured — do NOT override
            if (string.IsNullOrWhiteSpace(deviceSettings.DeviceCategory))
                deviceSettings.DeviceCategory = "Billboard";

            if (string.IsNullOrWhiteSpace(deviceSettings.ControlMode))
                deviceSettings.ControlMode = "OnOff";

            NormalizeBillingConfigurationDefaults(deviceSettings);

            await PopulateAutomaticSurfaceAreaAsync(deviceSettings);

            await PopulateAutomaticBudgetAsync(deviceSettings);

            NormalizeBillingCutoff(deviceSettings);

            // WBP/LWBP: if both 0, not configured — keep 0 to signal "use flat tariff"
            // WbpStartHour/WbpEndHour default to 18/22 from model, only override if clearly invalid
            return deviceSettings;
        }

        public async Task<Dictionary<string, DeviceSettings>> GetAllEffectiveAsync()
        {
            var allDeviceSettings = await _context.DeviceSettings
                .AsNoTracking()
                .ToListAsync();

            var result = new Dictionary<string, DeviceSettings>();

            var activeTrendDevices = await _context.BillingTrendProfiles
                .AsNoTracking()
                .Where(x => x.Status == "ACTIVE"
                    && x.ActiveBaselineAmount.HasValue
                    && x.ActiveBaselineAmount.Value > 0m)
                .Select(x => x.DeviceKey)
                .Distinct()
                .ToListAsync();

            var locationKeys = allDeviceSettings.Select(x => x.DeviceKey)
                .Concat(activeTrendDevices)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var lokasiMap = await _titikLokasiService.GetByDeviceKeysAsync(locationKeys);

            foreach (var setting in allDeviceSettings)
            {
                // MaxCapacity, EMA thresholds, BudgetKWh, SurfaceArea: 0 means not configured — do NOT override
                if (string.IsNullOrWhiteSpace(setting.DeviceCategory))
                    setting.DeviceCategory = "Billboard";

                if (string.IsNullOrWhiteSpace(setting.ControlMode))
                    setting.ControlMode = "OnOff";

                NormalizeBillingConfigurationDefaults(setting);

                PopulateAutomaticSurfaceArea(setting, lokasiMap);

                await PopulateAutomaticBudgetAsync(setting);

                // WBP/LWBP: if both 0, not configured — keep 0
                if (setting.WbpStartHour < 0 || setting.WbpStartHour > 23)
                    setting.WbpStartHour = 18;
                if (setting.WbpEndHour < 0 || setting.WbpEndHour > 23)
                    setting.WbpEndHour = 22;

                NormalizeBillingCutoff(setting);

                result[setting.DeviceKey] = setting;
            }

            // A device can already have an active billing trend before its
            // DeviceSettings row exists. Include that device in the effective
            // settings so the trend budget is visible in Settings per Device.
            foreach (var deviceKey in activeTrendDevices)
            {
                if (string.IsNullOrWhiteSpace(deviceKey) || result.ContainsKey(deviceKey))
                    continue;

                var setting = new DeviceSettings
                {
                    DeviceKey = deviceKey,
                    DeviceCategory = "Billboard",
                    ControlMode = "OnOff"
                };

                NormalizeBillingConfigurationDefaults(setting);
                PopulateAutomaticSurfaceArea(setting, lokasiMap);
                await PopulateAutomaticBudgetAsync(setting);
                NormalizeBillingCutoff(setting);
                result[deviceKey] = setting;
            }

            return result;
        }

        private async Task PopulateAutomaticSurfaceAreaAsync(DeviceSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.DeviceKey) || settings.SurfaceArea > 0m)
                return;

            try
            {
                var lokasiMap = await _titikLokasiService.GetByDeviceKeysAsync(new[] { settings.DeviceKey });
                if (lokasiMap.TryGetValue(settings.DeviceKey.Trim(), out var lokasi)
                    && lokasi != null
                    && lokasi.Luas > 0m)
                {
                    settings.SurfaceArea = lokasi.Luas;
                }
            }
            catch
            {
                // ERP bersifat pelengkap. Settings tetap dapat digunakan jika ERP tidak tersedia.
            }
        }

        private static void PopulateAutomaticSurfaceArea(
            DeviceSettings settings,
            Dictionary<string, TitikLokasi> lokasiMap)
        {
            if (settings == null || settings.SurfaceArea > 0m || lokasiMap == null
                || string.IsNullOrWhiteSpace(settings.DeviceKey))
                return;

            if (lokasiMap.TryGetValue(settings.DeviceKey.Trim(), out var lokasi)
                && lokasi != null
                && lokasi.Luas > 0m)
            {
                settings.SurfaceArea = lokasi.Luas;
            }
        }

        public async Task<DeviceSettings> GetOrCreateAsync(string deviceKey)
        {
            var existing = await _context.DeviceSettings
                .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

            if (existing != null)
            {
                return existing;
            }

            var effective = await GetEffectiveAsync(deviceKey);
            var created = new DeviceSettings
            {
                DeviceKey = deviceKey,
                MaxCapacity = effective.MaxCapacity,
                DeviceCategory = effective.DeviceCategory,
                DowntimeEnabled = effective.DowntimeEnabled,
                DowntimeStart = effective.DowntimeStart,
                DowntimeEnd = effective.DowntimeEnd,
                TariffPerKWh = effective.TariffPerKWh,
                LoadNormalThreshold = effective.LoadNormalThreshold,
                LoadMediumThreshold = effective.LoadMediumThreshold,
                EmaUpperThreshold = effective.EmaUpperThreshold,
                EmaLowerThreshold = effective.EmaLowerThreshold,
                EmaFibUpper = effective.EmaFibUpper,
                EmaFibLower = effective.EmaFibLower,
                ControlMode = effective.ControlMode,
                InvertRelayCommand = effective.InvertRelayCommand,
                TariffWBP = effective.TariffWBP,
                TariffLWBP = effective.TariffLWBP,
                WbpStartHour = effective.WbpStartHour,
                WbpEndHour = effective.WbpEndHour,
                BudgetKWh = effective.BudgetKWh,
                SurfaceArea = effective.SurfaceArea,
                RevenuePerHour = effective.RevenuePerHour,
                BillingAnomalyThreshold = effective.BillingAnomalyThreshold,
                BillingAnomalyThresholdMode = effective.BillingAnomalyThresholdMode,
                BillingAnomalyThresholdPercent = effective.BillingAnomalyThresholdPercent,
                BillingTrendToleranceMode = effective.BillingTrendToleranceMode,
                BillingTrendToleranceAmount = effective.BillingTrendToleranceAmount,
                BillingTrendTolerancePercent = effective.BillingTrendTolerancePercent,
                MonthlyBillingBudgetAmount = effective.MonthlyBillingBudgetAmount,
                MonthlyBillingBudgetSource = effective.MonthlyBillingBudgetSource,
                MonthlyBillingBudgetManualOverride = effective.MonthlyBillingBudgetManualOverride,
                MonthlyBillingBudgetTrendProfileId = effective.MonthlyBillingBudgetTrendProfileId,
                MonthlyBillingBudgetBaselineVersion = effective.MonthlyBillingBudgetBaselineVersion,
                MonthlyBillingBudgetUpdatedAt = effective.MonthlyBillingBudgetUpdatedAt,
                BillingCutoffEnabled = effective.BillingCutoffEnabled,
                BillingCutoffDay = effective.BillingCutoffDay,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.DeviceSettings.Add(created);
            await _context.SaveChangesAsync();

            return created;
        }

        public async Task SaveAsync(string deviceKey, DeviceSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(deviceKey))
                throw new ArgumentException("Device key wajib diisi.", nameof(deviceKey));
            if (settings.BillingAnomalyThreshold.HasValue && settings.BillingAnomalyThreshold.Value < 0m)
                throw new ArgumentOutOfRangeException(nameof(settings.BillingAnomalyThreshold), "Threshold anomaly tagihan tidak boleh negatif.");

            ValidateBillingConfiguration(settings);

            NormalizeBillingCutoff(settings);

            deviceKey = deviceKey.Trim();

            var existing = await _context.DeviceSettings
                .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

            if (existing != null)
            {
                existing.MaxCapacity = settings.MaxCapacity;
                existing.DeviceCategory = settings.DeviceCategory;
                existing.DowntimeEnabled = settings.DowntimeEnabled;
                existing.DowntimeStart = settings.DowntimeStart;
                existing.DowntimeEnd = settings.DowntimeEnd;
                existing.TariffPerKWh = settings.TariffPerKWh;
                existing.LoadNormalThreshold = settings.LoadNormalThreshold;
                existing.LoadMediumThreshold = settings.LoadMediumThreshold;
                existing.EmaUpperThreshold = settings.EmaUpperThreshold;
                existing.EmaLowerThreshold = settings.EmaLowerThreshold;
                existing.EmaFibUpper = settings.EmaFibUpper;
                existing.EmaFibLower = settings.EmaFibLower;
                existing.ControlMode = settings.ControlMode;
                existing.InvertRelayCommand = settings.InvertRelayCommand;
                existing.TariffWBP = settings.TariffWBP;
                existing.TariffLWBP = settings.TariffLWBP;
                existing.WbpStartHour = settings.WbpStartHour;
                existing.WbpEndHour = settings.WbpEndHour;
                existing.BudgetKWh = settings.BudgetKWh;
                existing.SurfaceArea = settings.SurfaceArea;
                existing.RevenuePerHour = settings.RevenuePerHour;
                existing.BillingAnomalyThreshold = settings.BillingAnomalyThreshold;
                existing.BillingAnomalyThresholdMode = settings.BillingAnomalyThresholdMode;
                existing.BillingAnomalyThresholdPercent = settings.BillingAnomalyThresholdPercent;
                existing.BillingTrendToleranceMode = settings.BillingTrendToleranceMode;
                existing.BillingTrendToleranceAmount = settings.BillingTrendToleranceAmount;
                existing.BillingTrendTolerancePercent = settings.BillingTrendTolerancePercent;
                existing.MonthlyBillingBudgetAmount = settings.MonthlyBillingBudgetAmount;
                existing.MonthlyBillingBudgetSource = settings.MonthlyBillingBudgetSource;
                existing.MonthlyBillingBudgetManualOverride = settings.MonthlyBillingBudgetManualOverride;
                existing.MonthlyBillingBudgetTrendProfileId = settings.MonthlyBillingBudgetTrendProfileId;
                existing.MonthlyBillingBudgetBaselineVersion = settings.MonthlyBillingBudgetBaselineVersion;
                existing.MonthlyBillingBudgetUpdatedAt = settings.MonthlyBillingBudgetUpdatedAt;
                existing.BillingCutoffEnabled = settings.BillingCutoffEnabled;
                existing.BillingCutoffDay = settings.BillingCutoffDay;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                settings.DeviceKey = deviceKey;
                settings.CreatedAt = DateTime.UtcNow;
                settings.UpdatedAt = DateTime.UtcNow;
                _context.DeviceSettings.Add(settings);
            }

            await _context.SaveChangesAsync();
        }

        private static void NormalizeBillingCutoff(DeviceSettings settings)
        {
            if (!settings.BillingCutoffEnabled)
            {
                settings.BillingCutoffDay = null;
                return;
            }

            if (!settings.BillingCutoffDay.HasValue || settings.BillingCutoffDay.Value < 25 || settings.BillingCutoffDay.Value > 31)
                throw new ArgumentOutOfRangeException(nameof(settings.BillingCutoffDay), "Tanggal cutoff harus berada pada rentang 25 sampai 31 ketika cutoff diaktifkan.");
        }

        private async Task PopulateAutomaticBudgetAsync(DeviceSettings settings)
        {
            if (settings.MonthlyBillingBudgetManualOverride)
                return;

            var baseline = await _context.BillingTrendProfiles
                .AsNoTracking()
                .Where(x => x.DeviceKey == settings.DeviceKey
                    && x.Status == "ACTIVE"
                    && x.ActiveBaselineAmount.HasValue
                    && x.ActiveBaselineAmount.Value > 0m)
                .OrderByDescending(x => x.BaselineVersion)
                .ThenByDescending(x => x.UpdatedAt)
                .Select(x => new { x.Id, x.BaselineVersion, x.ActiveBaselineAmount })
                .FirstOrDefaultAsync();

            if (baseline == null)
                return;

            if (settings.MonthlyBillingBudgetAmount.HasValue
                && string.Equals(settings.MonthlyBillingBudgetSource, "TREND_AUTO", StringComparison.OrdinalIgnoreCase)
                && settings.MonthlyBillingBudgetBaselineVersion.HasValue
                && settings.MonthlyBillingBudgetBaselineVersion.Value >= baseline.BaselineVersion)
                return;

            settings.MonthlyBillingBudgetAmount = Math.Round(baseline.ActiveBaselineAmount.Value, 2, MidpointRounding.AwayFromZero);
            settings.MonthlyBillingBudgetSource = "TREND_AUTO";
            settings.MonthlyBillingBudgetTrendProfileId = baseline.Id;
            settings.MonthlyBillingBudgetBaselineVersion = baseline.BaselineVersion;
        }

        private static void NormalizeBillingConfigurationDefaults(DeviceSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.BillingAnomalyThresholdMode))
                settings.BillingAnomalyThresholdMode = BillingConfigurationResolver.AmountMode;
            else
                settings.BillingAnomalyThresholdMode = settings.BillingAnomalyThresholdMode.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(settings.BillingTrendToleranceMode))
                settings.BillingTrendToleranceMode = BillingConfigurationResolver.PercentageMode;
            else
                settings.BillingTrendToleranceMode = settings.BillingTrendToleranceMode.Trim().ToUpperInvariant();

            if (!settings.BillingTrendTolerancePercent.HasValue
                && settings.BillingTrendToleranceMode == BillingConfigurationResolver.PercentageMode)
                settings.BillingTrendTolerancePercent = 10m;

            if (string.IsNullOrWhiteSpace(settings.MonthlyBillingBudgetSource))
                settings.MonthlyBillingBudgetSource = "MANUAL";
        }

        private static void ValidateBillingConfiguration(DeviceSettings settings)
        {
            var thresholdMode = (settings.BillingAnomalyThresholdMode ?? BillingConfigurationResolver.AmountMode).Trim().ToUpperInvariant();
            if (thresholdMode != BillingConfigurationResolver.AmountMode && thresholdMode != BillingConfigurationResolver.PercentageMode)
                throw new ArgumentException("Mode threshold billing harus AMOUNT atau PERCENTAGE.", nameof(settings.BillingAnomalyThresholdMode));

            if (settings.BillingAnomalyThresholdPercent.HasValue
                && (settings.BillingAnomalyThresholdPercent.Value < 0m || settings.BillingAnomalyThresholdPercent.Value > 1000m))
                throw new ArgumentOutOfRangeException(nameof(settings.BillingAnomalyThresholdPercent), "Persentase threshold billing tidak valid.");

            var toleranceMode = (settings.BillingTrendToleranceMode ?? BillingConfigurationResolver.PercentageMode).Trim().ToUpperInvariant();
            if (toleranceMode != BillingConfigurationResolver.AmountMode && toleranceMode != BillingConfigurationResolver.PercentageMode)
                throw new ArgumentException("Mode toleransi trend harus AMOUNT atau PERCENTAGE.", nameof(settings.BillingTrendToleranceMode));

            if (settings.BillingTrendToleranceAmount.HasValue && settings.BillingTrendToleranceAmount.Value < 0m)
                throw new ArgumentOutOfRangeException(nameof(settings.BillingTrendToleranceAmount), "Toleransi nominal tidak boleh negatif.");

            if (settings.BillingTrendTolerancePercent.HasValue
                && (settings.BillingTrendTolerancePercent.Value < 0m || settings.BillingTrendTolerancePercent.Value > 1000m))
                throw new ArgumentOutOfRangeException(nameof(settings.BillingTrendTolerancePercent), "Persentase toleransi trend tidak valid.");

            if (settings.MonthlyBillingBudgetAmount.HasValue && settings.MonthlyBillingBudgetAmount.Value < 0m)
                throw new ArgumentOutOfRangeException(nameof(settings.MonthlyBillingBudgetAmount), "Anggaran tagihan nominal tidak boleh negatif.");

            var source = (settings.MonthlyBillingBudgetSource ?? string.Empty).Trim().ToUpperInvariant();
            if (source != "TREND_AUTO" && source != "MANUAL" && source != "FALLBACK")
                source = settings.MonthlyBillingBudgetManualOverride ? "MANUAL" : "TREND_AUTO";

            // Mode is controlled by the explicit override switch. Never allow a
            // stale/inconsistent source from the client to disable trend updates.
            if (settings.MonthlyBillingBudgetManualOverride)
                source = "MANUAL";
            else if (source == "MANUAL")
                source = "TREND_AUTO";

            settings.BillingAnomalyThresholdMode = thresholdMode;
            settings.BillingTrendToleranceMode = toleranceMode;
            settings.MonthlyBillingBudgetSource = source;
            if (settings.BillingTrendToleranceMode == BillingConfigurationResolver.PercentageMode
                && !settings.BillingTrendTolerancePercent.HasValue)
                settings.BillingTrendTolerancePercent = 10m;
        }
    }
}

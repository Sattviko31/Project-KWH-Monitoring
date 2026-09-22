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

        public DeviceSettingsService(ApplicationDbContext context)
        {
            _context = context;
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

            // MaxCapacity, EMA thresholds: 0 means not configured — do NOT override
            if (string.IsNullOrWhiteSpace(deviceSettings.DeviceCategory))
                deviceSettings.DeviceCategory = "Billboard";

            if (deviceSettings.TariffPerKWh <= 0)
                deviceSettings.TariffPerKWh = 1500m;

            if (deviceSettings.LoadNormalThreshold <= 0)
                deviceSettings.LoadNormalThreshold = 30;

            if (deviceSettings.LoadMediumThreshold <= 0)
                deviceSettings.LoadMediumThreshold = 70;

            if (string.IsNullOrWhiteSpace(deviceSettings.ControlMode))
                deviceSettings.ControlMode = "OnOff";

            return deviceSettings;
        }

        public async Task<Dictionary<string, DeviceSettings>> GetAllEffectiveAsync()
        {
            var allDeviceSettings = await _context.DeviceSettings
                .AsNoTracking()
                .ToListAsync();

            var result = new Dictionary<string, DeviceSettings>();

            foreach (var setting in allDeviceSettings)
            {
                // MaxCapacity, EMA thresholds: 0 means not configured — do NOT override
                if (string.IsNullOrWhiteSpace(setting.DeviceCategory))
                    setting.DeviceCategory = "Billboard";

                if (setting.TariffPerKWh <= 0)
                    setting.TariffPerKWh = 1500m;

                if (setting.LoadNormalThreshold <= 0)
                    setting.LoadNormalThreshold = 30;

                if (setting.LoadMediumThreshold <= 0)
                    setting.LoadMediumThreshold = 70;

                if (string.IsNullOrWhiteSpace(setting.ControlMode))
                    setting.ControlMode = "OnOff";

                result[setting.DeviceKey] = setting;
            }

            return result;
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
    }
}

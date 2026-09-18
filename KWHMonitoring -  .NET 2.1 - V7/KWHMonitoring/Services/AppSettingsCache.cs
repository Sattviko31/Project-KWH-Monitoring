using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Singleton in-memory snapshot of the AppSettings table.
    /// Warmed up once at startup and refreshed in the background when stale,
    /// so Razor views and services never issue synchronous DB queries per request.
    /// </summary>
    public class AppSettingsCache
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppSettingsCache> _logger;
        private readonly object _lock = new object();
        private Dictionary<string, string> _settings = new Dictionary<string, string>();
        private DateTime _loadedAtUtc = DateTime.MinValue;
        private int _refreshInProgress;

        public AppSettingsCache(IServiceScopeFactory scopeFactory, ILogger<AppSettingsCache> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            ApplicationDbContext.AppSettingsChanged = ScheduleRefresh;
        }

        /// <summary>
        /// Load all AppSettings from the database. Call once during startup.
        /// </summary>
        public async Task WarmUpAsync()
        {
            await LoadFromDatabaseAsync();
        }

        /// <summary>
        /// Force an immediate reload. Call after code paths that write AppSettings rows.
        /// </summary>
        public async Task ReloadAsync()
        {
            await LoadFromDatabaseAsync();
        }

        private async Task LoadFromDatabaseAsync()
        {
            try
            {
                Dictionary<string, string> records;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    records = await context.AppSettingsRecords
                        .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
                }

                lock (_lock)
                {
                    _settings = records;
                    _loadedAtUtc = DateTime.UtcNow;
                }

                _logger.LogInformation("AppSettingsCache loaded {Count} settings.", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load AppSettings into cache. Keeping previous snapshot.");
            }
        }

        /// <summary>
        /// Get all settings as a copy. Never blocks on the database: if the snapshot is
        /// stale a background refresh is triggered and the current snapshot is returned.
        /// </summary>
        public Dictionary<string, string> GetAll()
        {
            TriggerRefreshIfStale();

            lock (_lock)
            {
                return new Dictionary<string, string>(_settings);
            }
        }

        /// <summary>
        /// Get a single setting value with fallback. Memory-only, never touches the database.
        /// </summary>
        public string GetValue(string key, string defaultValue = "")
        {
            TriggerRefreshIfStale();

            lock (_lock)
            {
                string value;
                return _settings.TryGetValue(key, out value) ? value : defaultValue;
            }
        }

        private void TriggerRefreshIfStale()    
        {
            bool stale;
            lock (_lock)
            {
                stale = DateTime.UtcNow - _loadedAtUtc > RefreshInterval;
            }

            if (stale) ScheduleRefresh();
        }

        /// <summary>
        /// Fire-and-forget reload; at most one refresh runs at a time.
        /// </summary>
        private void ScheduleRefresh()
        {
            if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0) return;

            Task.Run(async () =>
            {
                try
                {
                    await LoadFromDatabaseAsync();
                }
                finally
                {
                    Interlocked.Exchange(ref _refreshInProgress, 0);
                }
            });
        }
    }
}

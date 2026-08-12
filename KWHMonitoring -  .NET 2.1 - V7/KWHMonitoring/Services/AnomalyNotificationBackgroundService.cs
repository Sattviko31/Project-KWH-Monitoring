using System;
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
    public class AnomalyNotificationBackgroundService : BackgroundService
    {
        private readonly ILogger<AnomalyNotificationBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer _reportTimer;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private DateTime _lastHourlyReportTime;
        private DateTime _lastDailyReportSentDate;   // date when daily report was last sent
        private DateTime _lastMonthlyReportSentDate;  // date when monthly report was last sent

        private const string LastDailyReportSentKey = "Notification.LastDailyReportSentDate";
        private const string LastMonthlyReportSentKey = "Notification.LastMonthlyReportSentDate";

        public AnomalyNotificationBackgroundService(
            ILogger<AnomalyNotificationBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _lastHourlyReportTime = DateTime.MinValue;
            _lastDailyReportSentDate = DateTime.MinValue;
            _lastMonthlyReportSentDate = DateTime.MinValue;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Anomaly Notification Background Service is starting");

            // Restore last sent dates from database so restarts don't re-send reports
            await RestoreLastSentDatesAsync();

            _reportTimer = new Timer(DoWork, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        // [BUG] async void - exception yang tidak tertangkap bisa crash proses.
        // Sebaiknya ubah ke async Task dan handle dengan proper error handling.
        private async void DoWork(object state)
        {
            // Prevent concurrent execution: if previous DoWork is still running, skip this tick
            if (!await _lock.WaitAsync(0))
                return;

            try
            {
                var now = DateTime.Now;

                using (var scope = _serviceProvider.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                    var settings = notificationService.GetSettings();

                    _logger.LogInformation("Notification settings check: SendHourlyReport={0} (every hour at :00), SendDailyReport={1}, DailyTime={2}, SendMonthlyReport={3}, MonthlyDay={4}, MonthlyTime={5}, SendInstantAlert={6}",
                        settings.SendHourlyReport,
                        settings.SendDailyReport, settings.DailyReportTime,
                        settings.SendMonthlyReport, settings.MonthlyReportDay, settings.MonthlyReportTime,
                        settings.SendInstantAlert);

                    // Hourly report - server clock alignment
                    // Memastikan laporan terkirim pada waktu yang presisi sesuai jam server
                    if (settings.SendHourlyReport)
                    {
                        await SendHourlyReportAlignedAsync(notificationService, settings, now);
                    }

                    // Daily report - send at the configured time (DailyReportTime) once per day
                    if (settings.SendDailyReport)
                    {
                        var dailyTime = ParseDailyReportTime(settings.DailyReportTime);
                        var todayReportTime = now.Date.Add(dailyTime);

                        if (now >= todayReportTime && _lastDailyReportSentDate != now.Date)
                        {
                            _lastDailyReportSentDate = now.Date;
                            await PersistLastSentDateAsync(LastDailyReportSentKey, now.Date);
                            _logger.LogInformation("Sending scheduled daily report at {Time}", now);
                            await notificationService.SendRealtimeDailyReportAsync(isTest: false);
                        }
                    }

                    // Monthly report - send on the configured day of month at the configured time
                    if (settings.SendMonthlyReport)
                    {
                        var monthlyReportDay = settings.MonthlyReportDay > 0 && settings.MonthlyReportDay <= 28
                            ? settings.MonthlyReportDay : 1;
                        var monthlyTime = ParseDailyReportTime(settings.MonthlyReportTime);
                        var monthlyReportTime = now.Date.Add(monthlyTime);

                        if (now.Day == monthlyReportDay && now >= monthlyReportTime && _lastMonthlyReportSentDate != now.Date)
                        {
                            _lastMonthlyReportSentDate = now.Date;
                            await PersistLastSentDateAsync(LastMonthlyReportSentKey, now.Date);
                            _logger.LogInformation("Sending scheduled monthly report at {Time}", now);
                            await notificationService.SendRealtimeMonthlyReportAsync(isTest: false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification background service");
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Mengirim laporan perjam tepat pada awal jam (menit ke-0).
        /// Laporan dikirim setiap jam pada HH:00.
        /// </summary>
        private async Task SendHourlyReportAlignedAsync(NotificationService notificationService, NotificationSettings settings, DateTime now)
        {
            // Hanya kirim saat menit ke-0 (awal jam)
            if (now.Minute != 0)
                return;

            // Slot waktu untuk jam ini (cegah duplikat pengiriman)
            var targetSlot = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            if (_lastHourlyReportTime == targetSlot)
                return;

            _lastHourlyReportTime = targetSlot;
            _logger.LogInformation("Sending scheduled hourly report at {Time} (every hour at :00)", now);
            await notificationService.SendRealtimeHourlyReportAsync(isTest: false);
        }

        private async Task RestoreLastSentDatesAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    _lastDailyReportSentDate = await ReadLastSentDateAsync(db, LastDailyReportSentKey);
                    _lastMonthlyReportSentDate = await ReadLastSentDateAsync(db, LastMonthlyReportSentKey);
                    _logger.LogInformation("Restored last sent dates from DB: Daily={0}, Monthly={1}",
                        _lastDailyReportSentDate.ToShortDateString(),
                        _lastMonthlyReportSentDate.ToShortDateString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore last sent dates from DB, using DateTime.MinValue");
            }
        }

        private static async Task<DateTime> ReadLastSentDateAsync(ApplicationDbContext db, string key)
        {
            var record = await db.AppSettingsRecords
                .Where(r => r.SettingKey == key)
                .FirstOrDefaultAsync();

            if (record != null && DateTime.TryParseExact(record.SettingValue, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }

            return DateTime.MinValue;
        }

        private async Task PersistLastSentDateAsync(string key, DateTime date)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var record = await db.AppSettingsRecords
                        .Where(r => r.SettingKey == key)
                        .FirstOrDefaultAsync();

                    if (record == null)
                    {
                        db.AppSettingsRecords.Add(new AppSettingsRecord
                        {
                            SettingKey = key,
                            SettingValue = date.ToString("yyyy-MM-dd"),
                            UpdatedAt = DateTime.Now
                        });
                    }
                    else
                    {
                        record.SettingValue = date.ToString("yyyy-MM-dd");
                        record.UpdatedAt = DateTime.Now;
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist last sent date for {Key}", key);
            }
        }

        private TimeSpan ParseDailyReportTime(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr))
                return new TimeSpan(8, 0, 0); // default 08:00

            try
            {
                var parts = timeStr.Split(':');
                if (parts.Length >= 2)
                {
                    var hours = int.Parse(parts[0]);
                    var minutes = int.Parse(parts[1]);
                    return new TimeSpan(hours, minutes, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse DailyReportTime: {0}, using default 08:00", timeStr);
            }

            return new TimeSpan(8, 0, 0);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Anomaly Notification Background Service is stopping");
            _reportTimer?.Change(Timeout.Infinite, 0);
            return base.StopAsync(cancellationToken);
        }
    }
}

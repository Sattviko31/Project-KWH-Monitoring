using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KWHMonitoring.Controllers;
using KWHMonitoring.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Services
{
    public sealed class AnomalyDetectionBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AnomalyDetectionBackgroundService> _logger;

        public AnomalyDetectionBackgroundService(IServiceProvider services, ILogger<AnomalyDetectionBackgroundService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Server-side anomaly detection background service is starting.");
            await RunOnceSafelyAsync(stoppingToken);
            using (var timer = new PeriodicTimerCompat(TimeSpan.FromSeconds(10)))
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await RunOnceSafelyAsync(stoppingToken);
            }
        }

        private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
        {
            try { await DetectAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogError(ex, "Server-side anomaly detection scan failed."); }
        }

        private async Task DetectAsync(CancellationToken cancellationToken)
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var api = scope.ServiceProvider.GetRequiredService<ApiController>();
                var telemetryDeviceKeys = await db.KWH_Monitoring.AsNoTracking()
                    .Where(x => x.DeviceKey != null && x.DeviceKey != string.Empty)
                    .Select(x => x.DeviceKey).Distinct().ToListAsync(cancellationToken);
                var stateDeviceKeys = await db.ActiveAnomalyStates.AsNoTracking()
                    .Where(x => x.DeviceKey != null && x.DeviceKey != string.Empty)
                    .Select(x => x.DeviceKey).Distinct().ToListAsync(cancellationToken);
                var alertDeviceKeys = await db.AppSettingsRecords.AsNoTracking()
                    .Where(x => x.SettingKey.StartsWith("AnomalyAlert.Active."))
                    .Select(x => x.SettingKey.Substring("AnomalyAlert.Active.".Length))
                    .Where(x => x != null && x != string.Empty)
                    .Distinct().ToListAsync(cancellationToken);
                var deviceKeys = telemetryDeviceKeys
                    .Concat(stateDeviceKeys)
                    .Concat(alertDeviceKeys)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                foreach (var deviceKey in deviceKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ReconcileActiveEpisodeArtifactsAsync(db, deviceKey, cancellationToken);
                    await ProcessDeviceSilenceAsync(api, deviceKey, cancellationToken);
                    var samples = await db.KWH_Monitoring.AsNoTracking().Where(x => x.DeviceKey == deviceKey)
                        .OrderBy(x => x.Id).ToListAsync(cancellationToken);
                    if (samples.Count == 0) continue;

                    var watermarkKey = "AnomalyDetector.LastSampleId." + deviceKey;
                    var watermark = await db.AppSettingsRecords.FirstOrDefaultAsync(x => x.SettingKey == watermarkKey, cancellationToken);
                    long lastProcessedId;
                    if (watermark == null || !long.TryParse(watermark.SettingValue, out lastProcessedId))
                    {
                        await SaveWatermarkAsync(db, watermarkKey, watermark, samples[samples.Count - 1].Id, cancellationToken);
                        continue;
                    }

                    var newSamples = samples.Where(x => x.Id > lastProcessedId)
                        .OrderBy(x => x.Waktu_Server).ThenBy(x => x.Id).ToList();
                    foreach (var sample in newSamples)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var overload = await api.LogAnomaly(new AnomalyLogRequest
                        {
                            DeviceKey = deviceKey, SampleId = sample.Id,
                            SampleTime = sample.Waktu_Server, AnomalyType = "OVERLOAD"
                        });
                        if (IsSuccess(overload))
                        {
                            await CompleteAfterSnapshotAsync(db, deviceKey, cancellationToken);
                            await SaveWatermarkAsync(db, watermarkKey, watermark, sample.Id, cancellationToken);
                            continue;
                        }
                        if (!IsExpectedValidationRejection(overload)) break;

                        var drop = await api.LogAnomaly(new AnomalyLogRequest
                        {
                            DeviceKey = deviceKey, SampleId = sample.Id,
                            SampleTime = sample.Waktu_Server, AnomalyType = "DROP"
                        });
                        if (IsSuccess(drop))
                        {
                            await CompleteAfterSnapshotAsync(db, deviceKey, cancellationToken);
                            await SaveWatermarkAsync(db, watermarkKey, watermark, sample.Id, cancellationToken);
                            continue;
                        }
                        if (!IsExpectedValidationRejection(drop)) break;

                        var reset = await api.ResetAnomalyAlert(new ResetAnomalyAlertRequest
                        {
                            DeviceKey = deviceKey, SampleId = sample.Id, SampleTime = sample.Waktu_Server
                        });
                        if (!IsSuccess(reset)) break;
                        await SaveWatermarkAsync(db, watermarkKey, watermark, sample.Id, cancellationToken);
                    }
                }
            }
        }

        private async Task ProcessDeviceSilenceAsync(ApiController api, string deviceKey, CancellationToken cancellationToken)
        {
            var assessment = await api.AssessDeviceSilenceAsync(deviceKey);
            if (assessment == null)
                return;

            if (assessment.IsSilent)
            {
                var result = await api.LogAnomaly(new AnomalyLogRequest
                {
                    DeviceKey = deviceKey,
                    SampleTime = assessment.LastSampleTime,
                    AnomalyType = "DEVICE_OFFLINE",
                    PowerValue = assessment.LastPowerValue
                });

                if (!IsSuccess(result) && !IsExpectedValidationRejection(result))
                    _logger.LogWarning("Offline detection failed for device {DeviceKey}.", deviceKey);

                return;
            }

            // Recovery is accepted only when AssessDeviceSilenceAsync proves that
            // telemetry is arriving again. The endpoint removes the offline state
            // and active flag only when the sample is newer than the alert time.
            cancellationToken.ThrowIfCancellationRequested();
            var recovery = await api.GetAnomalyDeviceHealth(deviceKey);
            if (!IsSuccess(recovery))
                _logger.LogWarning("Offline recovery check failed for device {DeviceKey}.", deviceKey);
        }

        private static async Task ReconcileActiveEpisodeArtifactsAsync(ApplicationDbContext db, string deviceKey, CancellationToken cancellationToken)
        {
            var state = await db.ActiveAnomalyStates.FirstOrDefaultAsync(x => x.DeviceKey == deviceKey, cancellationToken);
            var activeKey = "AnomalyAlert.Active." + deviceKey;
            var alert = await db.AppSettingsRecords.FirstOrDefaultAsync(x => x.SettingKey == activeKey, cancellationToken);
            var type = alert == null || alert.SettingValue == null
                ? string.Empty
                : alert.SettingValue.Split('|').FirstOrDefault() ?? string.Empty;
            var offline = type == "DEVICE_OFFLINE" || type == "DEVICE_DROP";

            if (alert != null && !offline
                && (state == null || !state.IsConfirmed || !state.AnomalyLogId.HasValue
                    || !string.Equals(state.AnomalyType, type, StringComparison.Ordinal)))
            {
                db.AppSettingsRecords.Remove(alert);
            }

            // A confirmed state without a log can never be completed safely and
            // would suppress every subsequent episode indefinitely.
            if (state != null && state.IsConfirmed && !state.AnomalyLogId.HasValue)
                db.ActiveAnomalyStates.Remove(state);

            // Pending state is only valid for a short confirmation window. This
            // prevents an old browser/server residue from blocking new samples.
            if (state != null && !state.IsConfirmed && alert == null
                && state.LastDetectedAt < DateTime.Now.AddMinutes(-30))
                db.ActiveAnomalyStates.Remove(state);

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task SaveWatermarkAsync(ApplicationDbContext db, string key, AppSettingsRecord record, long sampleId, CancellationToken cancellationToken)
        {
            if (record != null && long.TryParse(record.SettingValue, out var existingSampleId)
                && existingSampleId >= sampleId)
                return;

            if (record == null)
            {
                db.AppSettingsRecords.Add(new AppSettingsRecord { SettingKey = key, SettingValue = sampleId.ToString(), UpdatedAt = DateTime.Now });
            }
            else
            {
                record.SettingValue = sampleId.ToString();
                record.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task CompleteAfterSnapshotAsync(ApplicationDbContext db, string deviceKey, CancellationToken cancellationToken)
        {
            var state = await db.ActiveAnomalyStates.AsNoTracking().FirstOrDefaultAsync(x => x.DeviceKey == deviceKey && x.AnomalyLogId.HasValue, cancellationToken);
            if (state == null) return;
            var snapshot = await db.AnomalyChartSnapshots.FirstOrDefaultAsync(x => x.AnomalyLogId == state.AnomalyLogId.Value, cancellationToken);
            if (snapshot == null || snapshot.SnapshotStatus == "complete") return;
            var after = string.IsNullOrWhiteSpace(snapshot.AfterDataJson)
                ? new List<ChartDataPoint>()
                : Newtonsoft.Json.JsonConvert.DeserializeObject<List<ChartDataPoint>>(snapshot.AfterDataJson) ?? new List<ChartDataPoint>();
            var timestamps = new HashSet<DateTime>(after.Where(x => x.Timestamp.HasValue).Select(x => x.Timestamp.Value));
            var rows = await db.KWH_Monitoring.AsNoTracking().Where(x => x.DeviceKey == deviceKey && x.Daya_Watt.HasValue && x.Waktu_Server > state.FirstDetectedAt)
                .OrderBy(x => x.Waktu_Server).ThenBy(x => x.Id).Take(50).ToListAsync(cancellationToken);
            foreach (var row in rows)
                if (!timestamps.Contains(row.Waktu_Server)) after.Add(new ChartDataPoint { Timestamp = row.Waktu_Server, Power = row.Daya_Watt.Value });
            after = after.OrderBy(x => x.Timestamp).Take(50).ToList();
            snapshot.AfterDataJson = Newtonsoft.Json.JsonConvert.SerializeObject(after);
            snapshot.SnapshotStatus = after.Count >= 50 ? "complete" : "partial";
            snapshot.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        private static bool IsSuccess(IActionResult result) { return result is OkObjectResult || result is OkResult; }
        private static bool IsExpectedValidationRejection(IActionResult result) { return result is BadRequestObjectResult || result is BadRequestResult; }

        private sealed class PeriodicTimerCompat : IDisposable
        {
            private readonly TimeSpan _interval;
            private Timer _timer;
            private TaskCompletionSource<bool> _completion;
            public PeriodicTimerCompat(TimeSpan interval) { _interval = interval; }
            public Task<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
            {
                _completion = new TaskCompletionSource<bool>();
                _timer = new Timer(_ => _completion.TrySetResult(true), null, _interval, Timeout.InfiniteTimeSpan);
                cancellationToken.Register(() => _completion.TrySetCanceled());
                return _completion.Task;
            }
            public void Dispose() { _timer?.Dispose(); }
        }
    }
}
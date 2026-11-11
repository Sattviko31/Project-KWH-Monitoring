using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Optional projection worker. It only writes the two projection tables and is
    /// completely inactive while EnergyProjection:Enabled is false.
    /// </summary>
    public class EnergyProjectionBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EnergyProjectionBackgroundService> _logger;
        private readonly EnergyProjectionOptions _options;
        private int _runGate;

        public EnergyProjectionBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<EnergyProjectionBackgroundService> logger,
            IOptions<EnergyProjectionOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value ?? new EnergyProjectionOptions();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_options.Enabled && Interlocked.CompareExchange(ref _runGate, 1, 0) == 0)
                    {
                        try { await RunOnceAsync(stoppingToken); }
                        finally { Interlocked.Exchange(ref _runGate, 0); }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { _logger.LogError(ex, "Energy projection worker failed."); }

                var seconds = _options.SafeWorkerIntervalSeconds;
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            }
        }

        private async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var projectionService = scope.ServiceProvider.GetRequiredService<IHourlyEnergyProjectionService>();
                if (!_options.HasSafeValues) return;

                var now = DateTime.UtcNow;
                var threshold = Math.Max(_options.SafeMinimumGapSeconds,
                    (int)Math.Ceiling(_options.SafeExpectedIntervalSeconds * Math.Max(1m, _options.GapMultiplier)));

                var latestByDevice = await context.KWH_Monitoring.AsNoTracking()
                    .Where(x => x.Waktu_Server != default(DateTime))
                    .GroupBy(x => x.DeviceKey)
                    .Select(g => new { DeviceKey = g.Key, LastSample = g.Max(x => x.Waktu_Server) })
                    .ToListAsync(cancellationToken);

                foreach (var latest in latestByDevice)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var lastSampleUtc = DateTime.SpecifyKind(latest.LastSample, DateTimeKind.Utc);
                    var silenceSeconds = (now - lastSampleUtc).TotalSeconds;
                    if (silenceSeconds <= threshold) continue;

                    var openGap = await context.EnergyProjectionGaps
                        .Where(x => x.DeviceKey == latest.DeviceKey && x.Status == "OPEN")
                        .OrderByDescending(x => x.GapStartUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    var gapStart = lastSampleUtc.AddSeconds(_options.SafeExpectedIntervalSeconds);
                    if (openGap == null)
                    {
                        openGap = new EnergyProjectionGap
                        {
                            DeviceKey = latest.DeviceKey,
                            GapStartUtc = gapStart,
                            Status = "OPEN",
                            DetectionSource = "KWHDATA_SILENCE",
                            Reason = "No valid telemetry observed beyond configured offline threshold.",
                            CreatedAtUtc = DateTime.UtcNow,
                            UpdatedAtUtc = DateTime.UtcNow
                        };
                        context.EnergyProjectionGaps.Add(openGap);
                        await context.SaveChangesAsync(cancellationToken);
                    }

                    openGap.GapEndUtc = now;
                    openGap.UpdatedAtUtc = DateTime.UtcNow;
                    var firstBucket = new DateTime(gapStart.Year, gapStart.Month, gapStart.Day, gapStart.Hour, 0, 0, DateTimeKind.Utc);
                    var lastBucket = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-1);
                    var maxHours = _options.SafeMaximumProjectedHoursPerRun;
                    var processed = 0;

                    for (var bucket = firstBucket; bucket <= lastBucket && processed < maxHours; bucket = bucket.AddHours(1))
                    {
                        processed++;
                        var actual = await context.HourlyEnergy.AsNoTracking()
                            .AnyAsync(x => x.DeviceKey == latest.DeviceKey && x.Hour == bucket, cancellationToken);
                        var existing = await context.EnergyProjectionValues
                            .FirstOrDefaultAsync(x => x.DeviceKey == latest.DeviceKey
                                && x.BucketStartUtc == bucket
                                && x.CalculationVersion == SafeVersion(), cancellationToken);

                        if (actual)
                        {
                            if (existing != null && existing.Status != "RECONCILED")
                            {
                                existing.Status = "RECONCILED";
                                existing.ReconciledAtUtc = DateTime.UtcNow;
                                existing.CalculatedAtUtc = DateTime.UtcNow;
                            }
                            if (openGap.GapEndUtc.HasValue)
                            {
                                openGap.Status = "RECONCILED";
                                openGap.ClosedAtUtc = DateTime.UtcNow;
                                openGap.UpdatedAtUtc = DateTime.UtcNow;
                            }
                            continue;
                        }

                        if (existing != null) continue;
                        var candidate = await projectionService.CalculateAsync(latest.DeviceKey, bucket);
                        // NO_BASELINE is metadata only. It must not be persisted as
                        // a synthetic 0 kWh value because that would look complete.
                        if (candidate.Status != "PROJECTED") continue;

                        context.EnergyProjectionValues.Add(new EnergyProjectionValue
                        {
                            DeviceKey = latest.DeviceKey,
                            BucketStartUtc = bucket,
                            ProjectedKWh = candidate.ProjectedKWh ?? 0m,
                            LowerBoundKWh = candidate.LowerBoundKWh,
                            UpperBoundKWh = candidate.UpperBoundKWh,
                            ConfidenceScore = candidate.ConfidenceScore,
                            ConfidenceLevel = candidate.ConfidenceLevel,
                            Status = candidate.Status,
                            ProjectionMethod = candidate.ProjectionMethod,
                            SampleCount = candidate.SampleCount,
                            GapId = openGap.Id,
                            SourcePeriodStartUtc = bucket.AddDays(-Math.Max(7, _options.HistoricalLookbackDays)),
                            SourcePeriodEndUtc = bucket,
                            CalculationVersion = SafeVersion(),
                            CalculatedAtUtc = DateTime.UtcNow
                        });
                    }

                    if (processed > 0)
                    {
                        openGap.Status = "PROJECTED";
                        await context.SaveChangesAsync(cancellationToken);
                    }
                }

                await ReconcileActualsAsync(context, cancellationToken);
            }
        }

        private async Task ReconcileActualsAsync(ApplicationDbContext context, CancellationToken cancellationToken)
        {
            var candidates = await context.EnergyProjectionValues
                .Where(x => x.Status == "PROJECTED" || x.Status == "NO_BASELINE")
                .Take(2000)
                .ToListAsync(cancellationToken);
            foreach (var item in candidates)
            {
                var actualExists = await context.HourlyEnergy.AsNoTracking()
                    .AnyAsync(x => x.DeviceKey == item.DeviceKey && x.Hour == item.BucketStartUtc, cancellationToken);
                if (actualExists)
                {
                    item.Status = "RECONCILED";
                    item.ReconciledAtUtc = DateTime.UtcNow;
                    if (item.GapId.HasValue)
                    {
                        var gap = await context.EnergyProjectionGaps
                            .FirstOrDefaultAsync(x => x.Id == item.GapId.Value, cancellationToken);
                        if (gap != null && gap.Status != "RECONCILED")
                        {
                            gap.Status = "RECONCILED";
                            gap.ClosedAtUtc = DateTime.UtcNow;
                            gap.UpdatedAtUtc = DateTime.UtcNow;
                        }
                    }
                }
            }
            if (candidates.Count > 0) await context.SaveChangesAsync(cancellationToken);
        }

        private string SafeVersion()
        {
            return string.IsNullOrWhiteSpace(_options.CalculationVersion) ? "v1" : _options.CalculationVersion.Trim();
        }
    }
}
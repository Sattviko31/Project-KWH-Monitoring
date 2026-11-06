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

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Server-side billing detector. It only evaluates ERP billing against the
    /// existing monthly energy estimate and delegates persistence/notification
    /// to the idempotent billing workflow.
    /// </summary>
    public sealed class BillingAnomalyDetectionBackgroundService : BackgroundService
    {
        private const decimal DefaultTariffPerKWh = 1444.70m;
        private readonly IServiceProvider _services;
        private readonly ILogger<BillingAnomalyDetectionBackgroundService> _logger;

        public BillingAnomalyDetectionBackgroundService(
            IServiceProvider services,
            ILogger<BillingAnomalyDetectionBackgroundService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Billing anomaly detection background service is starting.");
            await RunOnceSafelyAsync(stoppingToken);

            using (var timer = new PeriodicTimerCompat(TimeSpan.FromMinutes(1)))
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await RunOnceSafelyAsync(stoppingToken);
            }
        }

        private async Task RunOnceSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await DetectAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Billing anomaly detection background scan failed.");
            }
        }

        private async Task DetectAsync(CancellationToken cancellationToken)
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var titikLokasi = scope.ServiceProvider.GetRequiredService<ITitikLokasiService>();
                var settingsService = scope.ServiceProvider.GetRequiredService<IDeviceSettingsService>();
                var evaluator = scope.ServiceProvider.GetRequiredService<IBillingAnomalyDetectionService>();
                var workflow = scope.ServiceProvider.GetRequiredService<IBillingTrendWorkflowService>();

                var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var historyStart = currentMonth.AddMonths(-3);
                var historyEnd = currentMonth.AddMonths(1);
                var monthlyEnergy = await db.MonthlyEnergy.AsNoTracking()
                    .Where(x => (x.Year > historyStart.Year || (x.Year == historyStart.Year && x.Month >= historyStart.Month))
                        && (x.Year < historyEnd.Year || (x.Year == historyEnd.Year && x.Month < historyEnd.Month)))
                    .Select(x => new { x.DeviceKey, x.Year, x.Month, x.EnergyKWh })
                    .ToListAsync(cancellationToken);

                var deviceKeys = monthlyEnergy.Select(x => x.DeviceKey)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (deviceKeys.Count == 0)
                    return;

                var settings = await settingsService.GetAllEffectiveAsync();
                var billingByDevice = await titikLokasi.GetBillingHistoryAsync(
                    deviceKeys, historyStart.AddMonths(1), historyEnd.AddMonths(1));

                var energyByPeriod = monthlyEnergy.ToDictionary(
                    x => string.Format("{0}|{1:0000}-{2:00}", x.DeviceKey, x.Year, x.Month),
                    x => x.EnergyKWh,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var deviceKey in deviceKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!billingByDevice.TryGetValue(deviceKey, out var rows) || rows == null)
                        continue;

                    var validRows = rows.Where(x => x.TagihanListrikID > 0 && x.JumlahTagihan >= 0m
                            && x.Periode != DateTime.MinValue)
                        .OrderBy(x => x.Periode).ThenBy(x => x.TagihanListrikID).ToList();
                    var byPaymentMonth = validRows
                        .GroupBy(x => string.Format("{0:0000}-{1:00}", x.Periode.Year, x.Periode.Month), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.TagihanListrikID).First(), StringComparer.OrdinalIgnoreCase);

                    foreach (var billing in validRows)
                    {
                        var consumptionPeriod = new DateTime(billing.Periode.Year, billing.Periode.Month, 1).AddMonths(-1);
                        if (consumptionPeriod < historyStart || consumptionPeriod >= historyEnd)
                            continue;

                        var energyKey = string.Format("{0}|{1:0000}-{2:00}", deviceKey, consumptionPeriod.Year, consumptionPeriod.Month);
                        if (!energyByPeriod.TryGetValue(energyKey, out var energy))
                            continue;

                        var tariff = settings.TryGetValue(deviceKey, out var deviceSettings)
                            && deviceSettings.TariffPerKWh > 0m
                            ? deviceSettings.TariffPerKWh : DefaultTariffPerKWh;
                        var estimated = Math.Round(energy * tariff, 0, MidpointRounding.AwayFromZero);
                        var previousPeriod = billing.Periode.Date.AddMonths(-1);
                        byPaymentMonth.TryGetValue(string.Format("{0:0000}-{1:00}", previousPeriod.Year, previousPeriod.Month), out var previous);
                        var evaluation = await evaluator.EvaluateAsync(deviceKey, consumptionPeriod, estimated, billing, previous,
                            effectiveSettings: settings.TryGetValue(deviceKey, out var effective) ? effective : null);
                        if (!evaluation.IsValidBilling || !evaluation.IsAnomaly)
                            continue;

                        var types = new List<string>();
                        if (evaluation.EstimateVsErpIsAnomaly) types.Add("ESTIMATE_VS_ERP");
                        if (evaluation.PeriodOverPeriodIsAnomaly) types.Add("PERIOD_OVER_PERIOD");
                        await workflow.EnsureDetectedAnomalyAsync(deviceKey, consumptionPeriod, billing.TagihanListrikID,
                            estimated, billing.JumlahTagihan, evaluation.Difference ?? 0m, evaluation.DifferencePercent,
                            evaluation.Threshold, string.Join(",", types), evaluation.PreviousBilling,
                            evaluation.PreviousBillingPeriod, evaluation.PreviousErpBillingId);
                    }
                }
            }
        }

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
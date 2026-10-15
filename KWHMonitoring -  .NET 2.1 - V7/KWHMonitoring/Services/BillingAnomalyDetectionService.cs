using System;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Services
{
    public class BillingAnomalyDetectionService : IBillingAnomalyDetectionService
    {
        public const string AnomalyType = "BILLING_VARIANCE";
        private const string ThresholdSettingKey = "UsageStatistics.BillingAnomalyThreshold";
        private const decimal DefaultThreshold = 2000000m;

        private readonly ApplicationDbContext _context;
        private readonly AppSettingsCache _settingsCache;
        private readonly IConfiguration _configuration;
        private readonly NotificationService _notificationService;
        private readonly ILogger<BillingAnomalyDetectionService> _logger;

        public BillingAnomalyDetectionService(
            ApplicationDbContext context,
            AppSettingsCache settingsCache,
            IConfiguration configuration,
            NotificationService notificationService,
            ILogger<BillingAnomalyDetectionService> logger)
        {
            _context = context;
            _settingsCache = settingsCache;
            _configuration = configuration;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<BillingAnomalyEvaluation> EvaluateAsync(
            string deviceKey,
            DateTime billingPeriod,
            decimal estimatedBilling,
            ErpBillingRecord erpBilling,
            ErpBillingRecord previousErpBilling = null,
            bool persistAnomaly = true)
        {
            var result = new BillingAnomalyEvaluation
            {
                Threshold = await GetThresholdAsync(deviceKey)
            };

            if (string.IsNullOrWhiteSpace(deviceKey)
                || erpBilling == null
                || erpBilling.TagihanListrikID <= 0
                || estimatedBilling < 0m
                || erpBilling.JumlahTagihan < 0m)
            {
                return result;
            }

            result.IsValidBilling = true;
            result.Difference = Math.Round(erpBilling.JumlahTagihan - estimatedBilling, 2, MidpointRounding.AwayFromZero);
            result.DifferencePercent = estimatedBilling == 0m
                ? (erpBilling.JumlahTagihan == 0m ? 0m : (decimal?)null)
                : Math.Round(result.Difference.Value / estimatedBilling * 100m, 4, MidpointRounding.AwayFromZero);

            var normalizedBillingPeriod = billingPeriod.Date;
            result.EstimateVsErpIsAnomaly = Math.Abs(result.Difference.Value) > result.Threshold;
            if (result.EstimateVsErpIsAnomaly)
            {
                if (persistAnomaly)
                {
                    result.EstimateVsErpLogId = await SaveBillingAnomalyAsync(
                        deviceKey, normalizedBillingPeriod, estimatedBilling, erpBilling,
                        result.Difference.Value, result.DifferencePercent, result.Threshold,
                        "ESTIMATE_VS_ERP", null);
                }
                result.LogId = result.EstimateVsErpLogId;
                result.IsAnomaly = true;
            }

            if (previousErpBilling != null && previousErpBilling.TagihanListrikID > 0
                && previousErpBilling.JumlahTagihan >= 0m)
            {
                result.PreviousBilling = previousErpBilling.JumlahTagihan;
                result.PreviousBillingPeriod = previousErpBilling.Periode.Date;
                result.PreviousErpBillingId = previousErpBilling.TagihanListrikID;
                result.PeriodDifference = Math.Round(erpBilling.JumlahTagihan - previousErpBilling.JumlahTagihan, 2, MidpointRounding.AwayFromZero);
                result.PeriodDifferencePercent = previousErpBilling.JumlahTagihan == 0m
                    ? (erpBilling.JumlahTagihan == 0m ? 0m : (decimal?)null)
                    : Math.Round(result.PeriodDifference.Value / previousErpBilling.JumlahTagihan * 100m, 4, MidpointRounding.AwayFromZero);
                // Period-over-period billing anomaly hanya berlaku ketika tagihan
                // periode terpilih meningkat melebihi threshold. Penurunan besar
                // tetap ditampilkan sebagai delta negatif, tetapi bukan anomaly ini.
                result.PeriodOverPeriodIsAnomaly = result.PeriodDifference.Value > result.Threshold;

                if (result.PeriodOverPeriodIsAnomaly)
                {
                    if (persistAnomaly)
                    {
                        result.PeriodOverPeriodLogId = await SaveBillingAnomalyAsync(
                            deviceKey, normalizedBillingPeriod, previousErpBilling.JumlahTagihan, erpBilling,
                            result.PeriodDifference.Value, result.PeriodDifferencePercent, result.Threshold,
                            "PERIOD_OVER_PERIOD", previousErpBilling);
                    }
                    result.IsAnomaly = true;
                    if (!result.LogId.HasValue)
                        result.LogId = result.PeriodOverPeriodLogId;
                }
            }

            return result;
        }

        private async Task<long> SaveBillingAnomalyAsync(
            string deviceKey,
            DateTime billingPeriod,
            decimal comparisonBilling,
            ErpBillingRecord erpBilling,
            decimal difference,
            decimal? differencePercent,
            decimal threshold,
            string billingAnomalyType,
            ErpBillingRecord previousErpBilling)
        {
            var existing = await _context.AnomalyLogs
                .AsNoTracking()
                .Where(x => x.AnomalyType == AnomalyType
                    && x.BillingAnomalyType == billingAnomalyType
                    && x.DeviceKey == deviceKey
                    && x.BillingPeriod == billingPeriod
                    && x.ErpBillingId == erpBilling.TagihanListrikID)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();
            if (existing != null)
                return existing.Id;

            var severity = Math.Abs(differencePercent ?? 0m) > 50m ? "high" : "medium";
            var direction = difference >= 0m ? "lebih tinggi" : "lebih rendah";
            decimal? estimatedBillingValue = null;
            if (billingAnomalyType == "ESTIMATE_VS_ERP")
            {
                estimatedBillingValue = comparisonBilling;
            }

            decimal? previousBillingValue = null;
            if (billingAnomalyType == "PERIOD_OVER_PERIOD")
            {
                previousBillingValue = comparisonBilling;
            }

            var log = new AnomalyLog
            {
                DeviceKey = deviceKey,
                DeviceId = deviceKey,
                AnomalyType = AnomalyType,
                BillingAnomalyType = billingAnomalyType,
                PowerValue = erpBilling.JumlahTagihan,
                ThresholdValue = threshold,
                Deviation = Math.Round(Math.Abs(differencePercent ?? 0m), 2, MidpointRounding.AwayFromZero),
                DetectedTime = DateTime.Now,
                ThresholdMode = "billing-absolute",
                Acknowledged = false,
                Severity = severity,
                EstimatedBilling = estimatedBillingValue,
                ActualBilling = erpBilling.JumlahTagihan,
                BillingDifference = difference,
                BillingDifferencePercent = differencePercent,
                BillingPeriod = billingPeriod,
                ErpBillingId = erpBilling.TagihanListrikID,
                PreviousBilling = previousBillingValue,
                PreviousBillingPeriod = billingAnomalyType == "PERIOD_OVER_PERIOD" && previousErpBilling != null ? previousErpBilling.Periode.Date : (DateTime?)null,
                PreviousErpBillingId = billingAnomalyType == "PERIOD_OVER_PERIOD" && previousErpBilling != null ? previousErpBilling.TagihanListrikID : (long?)null,
                RootCause = billingAnomalyType == "PERIOD_OVER_PERIOD"
                    ? "Tagihan ERP periode berjalan " + direction + " dari tagihan ERP periode sebelumnya melebihi ambang selisih yang ditentukan."
                    : "Tagihan ERP " + direction + " dari estimasi sistem melebihi ambang selisih yang ditentukan.",
                RecommendedAction = "Verifikasi periode tagihan, tarif, konsumsi energi, dan komponen biaya pada ERP.",
                Notes = string.Format(CultureInfo.InvariantCulture,
                    "Billing anomaly. Subtype={0}; Period={1:yyyy-MM}; ERPPeriod={2:yyyy-MM-dd}; ERPId={3}; Comparison={4:N2}; Actual={5:N2}; Difference={6:N2}; Threshold={7:N2}.",
                    billingAnomalyType, billingPeriod, erpBilling.Periode, erpBilling.TagihanListrikID,
                    comparisonBilling, erpBilling.JumlahTagihan, difference, threshold)
            };

            _context.AnomalyLogs.Add(log);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException saveEx) when (IsBillingDuplicate(saveEx))
            {
                _context.Entry(log).State = EntityState.Detached;
                var duplicate = await _context.AnomalyLogs.AsNoTracking()
                    .Where(x => x.AnomalyType == AnomalyType
                        && x.BillingAnomalyType == billingAnomalyType
                        && x.DeviceKey == deviceKey
                        && x.BillingPeriod == billingPeriod
                        && x.ErpBillingId == erpBilling.TagihanListrikID)
                    .Select(x => new { x.Id })
                    .FirstOrDefaultAsync();
                if (duplicate != null)
                    return duplicate.Id;
                throw;
            }

            try
            {
                await _notificationService.SendBillingAnomalyAlertAsync(
                    deviceKey, billingPeriod, comparisonBilling, erpBilling.JumlahTagihan,
                    difference, differencePercent, threshold, erpBilling.TagihanListrikID);
            }
            catch (Exception notificationEx)
            {
                _logger.LogWarning(notificationEx, "Billing anomaly notification failed for {DeviceKey}, ERP billing {ErpBillingId}.", deviceKey, erpBilling.TagihanListrikID);
            }
            return log.Id;
        }

        private async Task<decimal> GetThresholdAsync(string deviceKey)
        {
            if (!string.IsNullOrWhiteSpace(deviceKey))
            {
                var deviceThreshold = await _context.DeviceSettings
                    .AsNoTracking()
                    .Where(x => x.DeviceKey == deviceKey)
                    .Select(x => x.BillingAnomalyThreshold)
                    .FirstOrDefaultAsync();

                // Nilai 0 pada data settings lama berarti belum dikonfigurasi.
                // Jangan menjadikannya threshold aktif karena itu membuat setiap
                // kenaikan positif terbaca sebagai anomaly.
                if (deviceThreshold.HasValue && deviceThreshold.Value > 0m)
                    return deviceThreshold.Value;
            }

            var cached = _settingsCache.GetValue(ThresholdSettingKey, null);
            if (decimal.TryParse(cached, NumberStyles.Number, CultureInfo.InvariantCulture, out var cachedValue) && cachedValue > 0m)
                return cachedValue;

            var configured = _configuration["UsageStatistics:BillingAnomalyThreshold"];
            if (decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var configuredValue) && configuredValue > 0m)
                return configuredValue;

            return DefaultThreshold;
        }

        private static bool IsBillingDuplicate(DbUpdateException exception)
        {
            var sqlException = exception.InnerException as SqlException;
            return sqlException != null && sqlException.Errors.Cast<SqlError>().Any(error =>
                error.Number == 2601 || error.Number == 2627);
        }
    }
}
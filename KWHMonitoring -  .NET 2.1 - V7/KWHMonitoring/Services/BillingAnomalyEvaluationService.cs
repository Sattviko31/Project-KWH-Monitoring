using System;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.Extensions.Configuration;

namespace KWHMonitoring.Services
{
    public interface IBillingAnomalyEvaluationService
    {
        Task<BillingAnomalyEvaluation> EvaluateAsync(
            string deviceKey,
            DateTime billingPeriod,
            decimal estimatedBilling,
            ErpBillingRecord erpBilling,
            ErpBillingRecord previousErpBilling = null,
            DeviceSettings effectiveSettings = null);
    }

    /// <summary>
    /// Read-only billing evaluator. It never writes AnomalyLogs, trend state,
    /// outbox rows, or calls a notification provider.
    /// </summary>
    public sealed class BillingAnomalyEvaluationService : IBillingAnomalyEvaluationService
    {
        private readonly IDeviceSettingsService _settingsService;
        private readonly AppSettingsCache _appSettingsCache;
        private readonly IConfiguration _configuration;

        public BillingAnomalyEvaluationService(
            IDeviceSettingsService settingsService,
            AppSettingsCache appSettingsCache,
            IConfiguration configuration)
        {
            _settingsService = settingsService;
            _appSettingsCache = appSettingsCache;
            _configuration = configuration;
        }

        public async Task<BillingAnomalyEvaluation> EvaluateAsync(
            string deviceKey,
            DateTime billingPeriod,
            decimal estimatedBilling,
            ErpBillingRecord erpBilling,
            ErpBillingRecord previousErpBilling = null,
            DeviceSettings effectiveSettings = null)
        {
            var settings = effectiveSettings ?? await _settingsService.GetEffectiveAsync(deviceKey);
            var globalThreshold = ReadPositiveDecimal(
                _appSettingsCache.GetValue("UsageStatistics.BillingAnomalyThreshold"),
                _configuration["UsageStatistics:BillingAnomalyThreshold"]);
            var threshold = BillingConfigurationResolver.ResolveThreshold(
                settings == null ? null : settings.BillingAnomalyThresholdMode,
                settings != null && settings.BillingAnomalyThreshold.HasValue
                    ? settings.BillingAnomalyThreshold
                    : globalThreshold,
                settings == null ? null : settings.BillingAnomalyThresholdPercent,
                erpBilling == null ? (decimal?)null : erpBilling.JumlahTagihan);

            var result = new BillingAnomalyEvaluation { Threshold = threshold.EffectiveAmount };
            if (string.IsNullOrWhiteSpace(deviceKey) || erpBilling == null
                || erpBilling.TagihanListrikID <= 0 || estimatedBilling < 0m
                || erpBilling.JumlahTagihan < 0m)
                return result;

            result.IsValidBilling = true;
            result.Difference = Math.Round(erpBilling.JumlahTagihan - estimatedBilling, 2, MidpointRounding.AwayFromZero);
            result.DifferencePercent = estimatedBilling == 0m
                ? (erpBilling.JumlahTagihan == 0m ? 0m : (decimal?)null)
                : Math.Round(result.Difference.Value / estimatedBilling * 100m, 4, MidpointRounding.AwayFromZero);
            result.EstimateVsErpIsAnomaly = Math.Abs(result.Difference.Value) > result.Threshold;

            if (previousErpBilling != null && previousErpBilling.TagihanListrikID > 0 && previousErpBilling.JumlahTagihan >= 0m)
            {
                result.PreviousBilling = previousErpBilling.JumlahTagihan;
                result.PreviousBillingPeriod = previousErpBilling.Periode.Date;
                result.PreviousErpBillingId = previousErpBilling.TagihanListrikID;
                result.PeriodDifference = Math.Round(erpBilling.JumlahTagihan - previousErpBilling.JumlahTagihan, 2, MidpointRounding.AwayFromZero);
                result.PeriodDifferencePercent = previousErpBilling.JumlahTagihan == 0m
                    ? (erpBilling.JumlahTagihan == 0m ? 0m : (decimal?)null)
                    : Math.Round(result.PeriodDifference.Value / previousErpBilling.JumlahTagihan * 100m, 4, MidpointRounding.AwayFromZero);
                result.PeriodOverPeriodIsAnomaly = result.PeriodDifference.Value > result.Threshold;
            }

            result.IsAnomaly = result.EstimateVsErpIsAnomaly || result.PeriodOverPeriodIsAnomaly;
            return result;
        }

        private static decimal? ReadPositiveDecimal(params string[] values)
        {
            foreach (var value in values)
            {
                decimal parsed;
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed) && parsed > 0m)
                    return parsed;
            }
            return null;
        }
    }
}
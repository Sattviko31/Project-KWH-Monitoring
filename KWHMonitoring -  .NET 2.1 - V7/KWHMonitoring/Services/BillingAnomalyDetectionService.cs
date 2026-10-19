using System;
using System.Threading.Tasks;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Backward-compatible facade for existing consumers. Persistence belongs
    /// to the billing workflow; this service is intentionally read-only.
    /// </summary>
    public class BillingAnomalyDetectionService : IBillingAnomalyDetectionService
    {
        public const string AnomalyType = "BILLING_VARIANCE";
        private readonly IBillingAnomalyEvaluationService _evaluator;

        public BillingAnomalyDetectionService(IBillingAnomalyEvaluationService evaluator)
        {
            _evaluator = evaluator;
        }

        public Task<BillingAnomalyEvaluation> EvaluateAsync(
            string deviceKey,
            DateTime billingPeriod,
            decimal estimatedBilling,
            ErpBillingRecord erpBilling,
            ErpBillingRecord previousErpBilling = null,
            bool persistAnomaly = false,
            DeviceSettings effectiveSettings = null)
        {
            // The legacy flag is retained only for source compatibility. It
            // cannot enable side effects anymore.
            return _evaluator.EvaluateAsync(deviceKey, billingPeriod, estimatedBilling, erpBilling, previousErpBilling, effectiveSettings);
        }
    }
}
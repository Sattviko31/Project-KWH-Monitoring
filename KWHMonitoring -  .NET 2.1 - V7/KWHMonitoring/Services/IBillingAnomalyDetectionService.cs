using System;
using System.Threading.Tasks;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public interface IBillingAnomalyDetectionService
    {
        Task<BillingAnomalyEvaluation> EvaluateAsync(
            string deviceKey,
            DateTime billingPeriod,
            decimal estimatedBilling,
            ErpBillingRecord erpBilling,
            ErpBillingRecord previousErpBilling = null);
    }

    public class BillingAnomalyEvaluation
    {
        public bool IsValidBilling { get; set; }
        public bool IsAnomaly { get; set; }
        public decimal Threshold { get; set; }
        public decimal? Difference { get; set; }
        public decimal? DifferencePercent { get; set; }
        public long? LogId { get; set; }
        public bool IsDuplicate { get; set; }
        public bool EstimateVsErpIsAnomaly { get; set; }
        public long? EstimateVsErpLogId { get; set; }
        public bool PeriodOverPeriodIsAnomaly { get; set; }
        public long? PeriodOverPeriodLogId { get; set; }
        public decimal? PeriodDifference { get; set; }
        public decimal? PeriodDifferencePercent { get; set; }
        public decimal? PreviousBilling { get; set; }
        public DateTime? PreviousBillingPeriod { get; set; }
        public long? PreviousErpBillingId { get; set; }
    }
}
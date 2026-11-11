using System;

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Safe defaults for the MQTT-gap projection feature. Enabled must remain false
    /// until staging validation and an explicit rollout decision are complete.
    /// </summary>
    public class EnergyProjectionOptions
    {
        public bool Enabled { get; set; } = false;
        public int MinimumHistoricalSamples { get; set; } = 4;
        public int HistoricalLookbackDays { get; set; } = 90;
        public decimal MinimumConfidence { get; set; } = 0.50m;
        public int ExpectedIntervalSeconds { get; set; } = 60;
        public decimal GapMultiplier { get; set; } = 3m;
        public int MinimumGapSeconds { get; set; } = 300;
        public int OfflineThresholdSeconds { get; set; } = 900;
        public int WorkerIntervalSeconds { get; set; } = 60;
        public int MaximumProjectedHoursPerRun { get; set; } = 168;
        public string CalculationVersion { get; set; } = "v1";

        /// <summary>
        /// Invalid projection configuration must fail closed. This prevents a bad
        /// deployment setting from creating synthetic energy values.
        /// </summary>
        public bool HasSafeValues
        {
            get
            {
                return ExpectedIntervalSeconds > 0
                    && GapMultiplier > 0m
                    && MinimumGapSeconds > 0
                    && OfflineThresholdSeconds > 0
                    && WorkerIntervalSeconds > 0
                    && MaximumProjectedHoursPerRun > 0
                    && HistoricalLookbackDays > 0
                    && MinimumHistoricalSamples > 0
                    && MinimumConfidence >= 0m
                    && MinimumConfidence <= 1m
                    && !string.IsNullOrWhiteSpace(CalculationVersion);
            }
        }

        public int SafeExpectedIntervalSeconds { get { return Math.Max(1, Math.Min(86400, ExpectedIntervalSeconds)); } }
        public int SafeMinimumGapSeconds { get { return Math.Max(1, Math.Min(86400, MinimumGapSeconds)); } }
        public int SafeOfflineThresholdSeconds { get { return Math.Max(SafeMinimumGapSeconds, Math.Min(7 * 86400, OfflineThresholdSeconds)); } }
        public int SafeWorkerIntervalSeconds { get { return Math.Max(10, Math.Min(3600, WorkerIntervalSeconds)); } }
        public int SafeMaximumProjectedHoursPerRun { get { return Math.Max(1, Math.Min(24 * 31, MaximumProjectedHoursPerRun)); } }
    }
}
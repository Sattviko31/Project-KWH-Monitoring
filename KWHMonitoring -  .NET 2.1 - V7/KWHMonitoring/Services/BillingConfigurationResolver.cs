using System;

namespace KWHMonitoring.Services
{
    public static class BillingConfigurationResolver
    {
        public const decimal InvalidConfigurationFallback = 1000000m;
        public const string AmountMode = "AMOUNT";
        public const string PercentageMode = "PERCENTAGE";

        public static BillingResolvedValue ResolveThreshold(
            string mode,
            decimal? amount,
            decimal? percent,
            decimal? referenceAmount)
        {
            return Resolve(mode, amount, percent, referenceAmount, "threshold");
        }

        public static BillingResolvedValue ResolveTolerance(
            string mode,
            decimal? amount,
            decimal? percent,
            decimal? referenceAmount)
        {
            return Resolve(mode, amount, percent, referenceAmount, "tolerance");
        }

        public static decimal ResolveBudget(decimal? acceptedBaseline, decimal? existingBudget)
        {
            if (acceptedBaseline.HasValue && IsPositiveFinite(acceptedBaseline.Value))
                return DecimalMoney(acceptedBaseline.Value);

            if (existingBudget.HasValue && IsPositiveFinite(existingBudget.Value))
                return DecimalMoney(existingBudget.Value);

            return InvalidConfigurationFallback;
        }

        private static BillingResolvedValue Resolve(
            string mode,
            decimal? amount,
            decimal? percent,
            decimal? referenceAmount,
            string purpose)
        {
            var normalizedMode = (mode ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedMode == AmountMode && amount.HasValue && IsPositiveFinite(amount.Value))
            {
                return new BillingResolvedValue
                {
                    Mode = AmountMode,
                    ConfiguredAmount = amount,
                    ConfiguredPercent = percent,
                    ReferenceAmount = referenceAmount,
                    EffectiveAmount = DecimalMoney(amount.Value),
                    UsedFallback = false,
                    Purpose = purpose
                };
            }

            if (normalizedMode == PercentageMode
                && percent.HasValue
                && IsPositiveFinite(percent.Value)
                && referenceAmount.HasValue
                && IsPositiveFinite(referenceAmount.Value))
            {
                var calculated = referenceAmount.Value * percent.Value / 100m;
                if (IsPositiveFinite(calculated))
                {
                    return new BillingResolvedValue
                    {
                        Mode = PercentageMode,
                        ConfiguredAmount = amount,
                        ConfiguredPercent = percent,
                        ReferenceAmount = referenceAmount,
                        EffectiveAmount = DecimalMoney(calculated),
                        UsedFallback = false,
                        Purpose = purpose
                    };
                }
            }

            return new BillingResolvedValue
            {
                Mode = string.IsNullOrEmpty(normalizedMode) ? null : normalizedMode,
                ConfiguredAmount = amount,
                ConfiguredPercent = percent,
                ReferenceAmount = referenceAmount,
                EffectiveAmount = InvalidConfigurationFallback,
                UsedFallback = true,
                Purpose = purpose
            };
        }

        private static bool IsPositiveFinite(decimal value)
        {
            return value > 0m;
        }

        private static decimal DecimalMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class BillingResolvedValue
    {
        public string Mode { get; set; }
        public decimal? ConfiguredAmount { get; set; }
        public decimal? ConfiguredPercent { get; set; }
        public decimal? ReferenceAmount { get; set; }
        public decimal EffectiveAmount { get; set; }
        public bool UsedFallback { get; set; }
        public string Purpose { get; set; }
    }
}
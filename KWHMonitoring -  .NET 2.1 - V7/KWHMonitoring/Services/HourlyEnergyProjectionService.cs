using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KWHMonitoring.Services
{
    public interface IHourlyEnergyProjectionService
    {
        bool IsEnabled { get; }
        string CalculationVersion { get; }
        Task<HourlyEnergyProjectionResult> CalculateAsync(string deviceKey, DateTime bucketStart);
    }

    public class HourlyEnergyProjectionResult
    {
        public string DeviceKey { get; set; }
        public DateTime BucketStart { get; set; }
        public string Status { get; set; } = "NO_DATA";
        public decimal? ProjectedKWh { get; set; }
        public decimal? LowerBoundKWh { get; set; }
        public decimal? UpperBoundKWh { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public string ConfidenceLevel { get; set; }
        public string ProjectionMethod { get; set; }
        public int SampleCount { get; set; }
        public string CalculationVersion { get; set; }
    }

    /// <summary>
    /// Calculates a candidate value from actual HourlyEnergy only. It does not write,
    /// overwrite, or reinterpret actual data and deliberately never reads projections
    /// as historical baseline input.
    /// </summary>
    public class HourlyEnergyProjectionService : IHourlyEnergyProjectionService
    {
        private readonly ApplicationDbContext _context;
        private readonly EnergyProjectionOptions _options;
        private readonly Dictionary<string, HourlyEnergyProjectionResult> _requestCache =
            new Dictionary<string, HourlyEnergyProjectionResult>(StringComparer.Ordinal);
        private readonly object _requestCacheLock = new object();

        public HourlyEnergyProjectionService(
            ApplicationDbContext context,
            IOptions<EnergyProjectionOptions> options)
        {
            _context = context;
            _options = options.Value ?? new EnergyProjectionOptions();
        }

        public bool IsEnabled => _options.Enabled && _options.HasSafeValues;
        public string CalculationVersion => SafeCalculationVersion();

        public async Task<HourlyEnergyProjectionResult> CalculateAsync(string deviceKey, DateTime bucketStart)
        {
            var cacheKey = BuildCacheKey(deviceKey, bucketStart);
            lock (_requestCacheLock)
            {
                if (_requestCache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            var result = new HourlyEnergyProjectionResult
            {
                DeviceKey = deviceKey,
                BucketStart = bucketStart,
                CalculationVersion = SafeCalculationVersion(),
                ProjectionMethod = "SAME_DEVICE_SAME_HOUR_WEIGHTED_MEDIAN"
            };

            if (!IsEnabled || string.IsNullOrWhiteSpace(deviceKey))
                return CacheResult(cacheKey, result);

            var lookbackDays = Clamp(_options.HistoricalLookbackDays, 7, 366);
            var minimumSamples = Clamp(_options.MinimumHistoricalSamples, 1, 30);
            // HourlyEnergy is stored in the application's server-local time.
            // Do not reinterpret a selected local date as UTC: doing so shifts
            // the comparison window when the user changes the date filter.
            var normalizedBucket = bucketStart.Date.AddHours(bucketStart.Hour);
            var historyStart = normalizedBucket.Date.AddDays(-lookbackDays);
            var targetDayClass = GetDayClass(bucketStart);

            // Read the target bucket and its historical baseline in one query.
            // Callers usually skip actual buckets already, but keeping the target
            // row here preserves the service contract while removing one round trip.
            var samples = await _context.HourlyEnergy.AsNoTracking()
                .Where(x => x.DeviceKey == deviceKey
                    && x.Hour >= historyStart
                    && x.Hour <= normalizedBucket
                    && x.Hour.Hour == normalizedBucket.Hour)
                .Select(x => new HistoricalSample { Hour = x.Hour, EnergyKWh = x.EnergyKWh })
                .ToListAsync();

            var actualSample = samples.FirstOrDefault(x => x.Hour == normalizedBucket);
            if (actualSample != null)
            {
                result.Status = "ACTUAL";
                return CacheResult(cacheKey, result);
            }

            var candidates = samples.Where(x => x.Hour < normalizedBucket).ToList();

            // Same device + same hour + same day class is primary. Weekend is the
            // only currently available day-class source; holidays must be supplied by
            // a configured calendar before being classified as HOLIDAY.
            var sameClass = candidates.Where(x => GetDayClass(x.Hour) == targetDayClass).ToList();
            var selected = sameClass.Count >= minimumSamples ? sameClass : candidates;
            if (selected.Count < minimumSamples)
            {
                result.Status = "NO_BASELINE";
                result.SampleCount = selected.Count;
                return CacheResult(cacheKey, result);
            }

            var weighted = selected.Select(x => new WeightedSample
            {
                Value = Math.Max(0m, x.EnergyKWh),
                Weight = Math.Max(1m, 1m + (decimal)(x.Hour.Date - historyStart.Date).TotalDays / lookbackDays)
            }).OrderBy(x => x.Value).ToList();
            var projected = WeightedMedian(weighted);
            var deviations = selected.Select(x => Math.Abs(Math.Max(0m, x.EnergyKWh) - projected)).OrderBy(x => x).ToList();
            var medianAbsoluteDeviation = Median(deviations);
            var lower = Math.Max(0m, projected - (medianAbsoluteDeviation * 1.4826m));
            var upper = projected + (medianAbsoluteDeviation * 1.4826m);
            var classFactor = sameClass.Count >= minimumSamples ? 1m : 0.75m;
            var sampleFactor = Math.Min(1m, selected.Count / 12m);
            var spreadFactor = projected <= 0m ? 1m : Math.Max(0m, 1m - (medianAbsoluteDeviation / projected));
            var confidence = Math.Round(Math.Min(1m, Math.Max(0m, classFactor * sampleFactor * spreadFactor)), 4);

            if (confidence < Clamp(_options.MinimumConfidence, 0m, 1m))
            {
                result.Status = "NO_BASELINE";
                result.SampleCount = selected.Count;
                result.ConfidenceScore = confidence;
                return CacheResult(cacheKey, result);
            }

            result.Status = "PROJECTED";
            result.ProjectedKWh = Math.Round(projected, 4);
            result.LowerBoundKWh = Math.Round(lower, 4);
            result.UpperBoundKWh = Math.Round(upper, 4);
            result.ConfidenceScore = confidence;
            result.ConfidenceLevel = confidence >= 0.80m ? "HIGH" : confidence >= 0.65m ? "MEDIUM" : "LOW";
            result.SampleCount = selected.Count;
            return CacheResult(cacheKey, result);
        }

        private HourlyEnergyProjectionResult CacheResult(string cacheKey, HourlyEnergyProjectionResult result)
        {
            lock (_requestCacheLock)
            {
                _requestCache[cacheKey] = result;
            }
            return result;
        }

        private static string BuildCacheKey(string deviceKey, DateTime bucketStart)
        {
            return (deviceKey ?? string.Empty) + "|" + bucketStart.Ticks.ToString();
        }

        private static string GetDayClass(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                ? "WEEKEND"
                : "WORKDAY";
        }

        private string SafeCalculationVersion()
        {
            return string.IsNullOrWhiteSpace(_options.CalculationVersion) ? "v1" : _options.CalculationVersion.Trim();
        }

        private static int Clamp(int value, int min, int max) { return Math.Max(min, Math.Min(max, value)); }
        private static decimal Clamp(decimal value, decimal min, decimal max) { return Math.Max(min, Math.Min(max, value)); }

        private static decimal WeightedMedian(List<WeightedSample> samples)
        {
            var halfWeight = samples.Sum(x => x.Weight) / 2m;
            decimal accumulated = 0m;
            foreach (var sample in samples)
            {
                accumulated += sample.Weight;
                if (accumulated >= halfWeight) return sample.Value;
            }
            return samples.Last().Value;
        }

        private static decimal Median(List<decimal> values)
        {
            if (values.Count == 0) return 0m;
            var middle = values.Count / 2;
            return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2m : values[middle];
        }

        private sealed class HistoricalSample
        {
            public DateTime Hour { get; set; }
            public decimal EnergyKWh { get; set; }
        }

        private sealed class WeightedSample
        {
            public decimal Value { get; set; }
            public decimal Weight { get; set; }
        }
    }
}
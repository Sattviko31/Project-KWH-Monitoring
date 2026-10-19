using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace KWHMonitoring.Services
{
    public interface IBillingTrendWorkflowService
    {
        Task<BillingTrendProfile> GetActiveAsync(string deviceKey);
        Task<BillingTrendConfirmationResult> ConfirmAsync(string deviceKey, DateTime billingPeriod, decimal observedAmount, long? anomalyLogId, string decision, string reason, string confirmedBy, string channel);
        Task<BillingTrendConfirmationResult> ConfirmWithTokenAsync(string rawToken, DateTime billingPeriod, decimal observedAmount, long? anomalyLogId, string decision, string reason, string confirmedBy, string channel, int authenticatedUserId);
        Task<BillingConfirmationTokenResult> IssueTokenAsync(string deviceKey, long? anomalyLogId, TimeSpan lifetime);
        Task QueueAnomalyNotificationsAsync(long anomalyLogId, bool forceResend = false);
        Task<BillingConfirmationTokenView> GetTokenViewAsync(string rawToken);
        Task<BillingTrendConfirmationResult> ConfirmFromLinkAsync(string rawToken, string reason, string confirmedBy, int authenticatedUserId);
        Task<long?> EnsureDetectedAnomalyAsync(string deviceKey, DateTime billingPeriod, long erpBillingId,
            decimal estimatedBilling, decimal actualBilling, decimal difference, decimal? differencePercent,
            decimal threshold, string billingAnomalyType, decimal? previousBilling,
            DateTime? previousBillingPeriod, long? previousErpBillingId);
    }

    public sealed class BillingTrendConfirmationResult
    {
        public bool Accepted { get; set; }
        public bool Duplicate { get; set; }
        public bool Reset { get; set; }
        public bool BaselineAccepted { get; set; }
        public decimal? BaselineAmount { get; set; }
        public BillingTrendProfile Profile { get; set; }
    }

    public sealed class BillingConfirmationTokenResult
    {
        public string RawToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public long TrendProfileId { get; set; }
    }

    public sealed class BillingConfirmationTokenView
    {
        public bool Valid { get; set; }
        public string Error { get; set; }
        public long AnomalyLogId { get; set; }
        public string DeviceKey { get; set; }
        public DateTime? BillingPeriod { get; set; }
        public decimal? EstimatedBilling { get; set; }
        public decimal? ActualBilling { get; set; }
        public decimal? BillingDifference { get; set; }
        public decimal? PreviousBilling { get; set; }
        public DateTime? PreviousBillingPeriod { get; set; }
        public decimal? PeriodDifference { get; set; }
        public decimal? PeriodDifferencePercent { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public sealed class BillingTrendWorkflowService : IBillingTrendWorkflowService
    {
        private const int RequiredConfirmations = 3;
        private readonly ApplicationDbContext _context;
        private readonly IDeviceSettingsService _settingsService;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;
        private readonly ITitikLokasiService _titikLokasiService;

        public BillingTrendWorkflowService(ApplicationDbContext context, IDeviceSettingsService settingsService, IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IEmailService emailService, ITitikLokasiService titikLokasiService)
        {
            _context = context;
            _settingsService = settingsService;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _emailService = emailService;
            _titikLokasiService = titikLokasiService;
        }

        public Task<BillingTrendProfile> GetActiveAsync(string deviceKey)
        {
            if (string.IsNullOrWhiteSpace(deviceKey)) return Task.FromResult<BillingTrendProfile>(null);
            return _context.BillingTrendProfiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey && x.Status == "ACTIVE");
        }

        public async Task<BillingTrendConfirmationResult> ConfirmAsync(
            string deviceKey, DateTime billingPeriod, decimal observedAmount, long? anomalyLogId,
            string decision, string reason, string confirmedBy, string channel)
        {
            if (string.IsNullOrWhiteSpace(deviceKey)) throw new ArgumentException("Device key wajib diisi.", nameof(deviceKey));
            if (observedAmount <= 0m) throw new ArgumentOutOfRangeException(nameof(observedAmount));
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3) throw new ArgumentException("Alasan wajib diisi.", nameof(reason));

            var normalizedPeriod = billingPeriod.Date;
            var normalizedDecision = (decision ?? "VALID").Trim().ToUpperInvariant();
            var result = new BillingTrendConfirmationResult();

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                var duplicate = await _context.BillingTrendConfirmations
                    .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey && x.BillingPeriod == normalizedPeriod);
                if (duplicate != null)
                {
                    result.Duplicate = true;
                    result.Accepted = duplicate.Decision == "VALID";
                    result.Profile = await GetActiveAsync(deviceKey);
                    transaction.Commit();
                    return result;
                }

                var profile = await _context.BillingTrendProfiles
                    .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey && x.Status == "ACTIVE");

                if (profile == null)
                {
                    profile = new BillingTrendProfile
                    {
                        DeviceKey = deviceKey,
                        AnchorBillingPeriod = normalizedPeriod,
                        AnchorAmount = observedAmount,
                        BaselineVersion = 0,
                        ValidConfirmationCount = 0
                    };
                    _context.BillingTrendProfiles.Add(profile);
                    await _context.SaveChangesAsync();
                }

                var previousPeriod = await _context.BillingTrendConfirmations
                    .Where(x => x.TrendProfileId == profile.Id && x.Decision == "VALID")
                    .OrderByDescending(x => x.BillingPeriod)
                    .Select(x => (DateTime?)x.BillingPeriod)
                    .FirstOrDefaultAsync();

                if (previousPeriod.HasValue && normalizedPeriod != previousPeriod.Value.AddMonths(1).Date)
                {
                    profile.Status = "RESET";
                    profile.UpdatedAt = DateTime.UtcNow;
                    _context.BillingTrendProfiles.Update(profile);
                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    result.Reset = true;
                    result.Profile = profile;
                    return result;
                }

                if (previousPeriod.HasValue && !await IsWithinToleranceAsync(deviceKey, profile, observedAmount))
                {
                    profile.Status = "RESET";
                    profile.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    transaction.Commit();
                    result.Reset = true;
                    result.Profile = profile;
                    return result;
                }

                var confirmation = new BillingTrendConfirmation
                {
                    TrendProfileId = profile.Id,
                    AnomalyLogId = anomalyLogId,
                    DeviceKey = deviceKey,
                    BillingPeriod = normalizedPeriod,
                    ObservedAmount = observedAmount,
                    Decision = normalizedDecision == "VALID" ? "VALID" : "INVALID",
                    Reason = reason.Trim(),
                    ConfirmedBy = confirmedBy,
                    Channel = channel,
                    CreatedAt = DateTime.UtcNow
                };
                _context.BillingTrendConfirmations.Add(confirmation);

                if (confirmation.Decision == "VALID")
                {
                    profile.ValidConfirmationCount++;
                    if (profile.ValidConfirmationCount >= RequiredConfirmations)
                    {
                        var values = await _context.BillingTrendConfirmations
                            .Where(x => x.TrendProfileId == profile.Id && x.Decision == "VALID")
                            .Select(x => x.ObservedAmount)
                            .ToListAsync();
                        values.Add(observedAmount);
                        values.Sort();
                        var median = values[values.Count / 2];
                        profile.ActiveBaselineAmount = Math.Round(median, 2, MidpointRounding.AwayFromZero);
                        profile.BaselineVersion++;
                        profile.AcceptedAt = DateTime.UtcNow;
                        result.BaselineAccepted = true;
                        result.BaselineAmount = profile.ActiveBaselineAmount;
                        await ApplyBudgetAsync(deviceKey, profile, profile.ActiveBaselineAmount.Value);
                    }
                }

                profile.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                transaction.Commit();
                result.Accepted = confirmation.Decision == "VALID";
                result.Profile = profile;
                return result;
                }
            });
        }

        public async Task<BillingTrendConfirmationResult> ConfirmWithTokenAsync(
            string rawToken, DateTime billingPeriod, decimal observedAmount, long? anomalyLogId,
            string decision, string reason, string confirmedBy, string channel, int authenticatedUserId)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                throw new ArgumentException("Token wajib diisi.", nameof(rawToken));

            var hash = Hash(rawToken);
            var token = await _context.BillingConfirmationTokens
                .FirstOrDefaultAsync(x => x.TokenHash == hash && x.Purpose == "BILLING_CONFIRMATION");
            if (token == null || token.RevokedAt.HasValue || token.UsedAt.HasValue || token.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Token tidak valid, sudah digunakan, dicabut, atau kedaluwarsa.");

            var profile = await _context.BillingTrendProfiles.FirstOrDefaultAsync(x => x.Id == token.TrendProfileId && x.Status == "ACTIVE");
            if (profile == null)
                throw new InvalidOperationException("Trend profile tidak aktif.");

            var currentUser = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == token.UserId && x.IsActive);
            if (currentUser == null || !await _context.BillingConfirmationRecipients.AnyAsync(x => x.UserId == currentUser.Id && x.IsSelected))
                throw new InvalidOperationException("Akun tidak terdaftar sebagai penerima konfirmasi.");
            if (currentUser.Id != authenticatedUserId)
                throw new InvalidOperationException("Token konfirmasi bukan milik akun yang sedang login.");
            if (token.AnomalyLogId.HasValue && anomalyLogId.HasValue && token.AnomalyLogId.Value != anomalyLogId.Value)
                throw new InvalidOperationException("Token tidak terkait dengan anomaly yang dikirim.");

            // ConfirmAsync owns its retriable transaction. Token/anomaly cleanup
            // is persisted after it succeeds, avoiding nested user transactions.
            var freshToken = await _context.BillingConfirmationTokens
                    .FirstOrDefaultAsync(x => x.Id == token.Id && !x.UsedAt.HasValue && !x.RevokedAt.HasValue);
            if (freshToken == null || freshToken.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Token tidak valid, sudah digunakan, dicabut, atau kedaluwarsa.");

            var result = await ConfirmAsync(profile.DeviceKey, billingPeriod, observedAmount, anomalyLogId, decision, reason, currentUser.Email, channel);
            if (anomalyLogId.HasValue)
            {
                var anomaly = await _context.AnomalyLogs.FirstOrDefaultAsync(x => x.Id == anomalyLogId.Value);
                if (anomaly == null) throw new InvalidOperationException("Anomaly tidak ditemukan.");
                anomaly.IsResolved = true;
                anomaly.ResolvedBy = currentUser.Email;
                anomaly.ResolvedTime = DateTime.UtcNow;
                anomaly.Acknowledged = true;
                anomaly.AcknowledgedBy = currentUser.Email;
                anomaly.AcknowledgedTime = DateTime.UtcNow;
                anomaly.OperatorAction = "billing_confirmation";
                anomaly.OperatorNotes = reason;
                _context.BillingAnomalyNotes.Add(new BillingAnomalyNote
                {
                    AnomalyLogId = anomaly.Id,
                    UserId = currentUser.Id,
                    Author = currentUser.Email,
                    NoteType = "CONFIRMATION",
                    Decision = (decision ?? "VALID").Trim().ToUpperInvariant(),
                    Note = reason
                });
                var outboxEventPrefix = anomaly.Id + ":";
                var outboxRows = await _context.BillingNotificationOutbox
                    .Where(x => x.NotificationType == "BILLING_CONFIRMATION" && x.EventKey.StartsWith(outboxEventPrefix))
                    .ToListAsync();
                _context.BillingNotificationOutbox.RemoveRange(outboxRows);
            }
            freshToken.UsedAt = DateTime.UtcNow;
            freshToken.UsedBy = currentUser.Email;
            await _context.SaveChangesAsync();
            return result;
        }

        public async Task<BillingConfirmationTokenResult> IssueTokenAsync(string deviceKey, long? anomalyLogId, TimeSpan lifetime)
        {
            var profile = await _context.BillingTrendProfiles.FirstOrDefaultAsync(x => x.DeviceKey == deviceKey && x.Status == "ACTIVE");
            if (profile == null) return null;
            var recipient = await _context.BillingConfirmationRecipients
                .Where(x => x.IsSelected)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();
            if (recipient == null) return null;
            var raw = Convert.ToBase64String(GenerateRandomBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var expires = DateTime.UtcNow.Add(lifetime);
            _context.BillingConfirmationTokens.Add(new BillingConfirmationToken
            {
                TokenHash = Hash(raw),
                TrendProfileId = profile.Id,
                AnomalyLogId = anomalyLogId,
                UserId = recipient.UserId,
                ExpiresAt = expires
            });
            await _context.SaveChangesAsync();
            return new BillingConfirmationTokenResult { RawToken = raw, ExpiresAt = expires, TrendProfileId = profile.Id };
        }

        public async Task QueueAnomalyNotificationsAsync(long anomalyLogId, bool forceResend = false)
        {
            var anomaly = await _context.AnomalyLogs.FirstOrDefaultAsync(x => x.Id == anomalyLogId);
            if (anomaly == null) throw new InvalidOperationException("Anomaly tidak ditemukan.");
            if (!IsPeriodOverPeriodAnomaly(anomaly.BillingAnomalyType))
                return;
            if (anomaly.IsResolved)
                throw new InvalidOperationException("Anomaly sudah terkonfirmasi. Notifikasi ulang tidak diperbolehkan.");
            if (!forceResend && await _context.BillingNotificationOutbox.AnyAsync(x =>
                x.NotificationType == "BILLING_CONFIRMATION" && x.Channel == "EMAIL" &&
                x.EventKey.StartsWith(anomaly.Id + ":")))
                return;
            var recipients = await (from selection in _context.BillingConfirmationRecipients
                                    join user in _context.ApplicationUsers on selection.UserId equals user.Id
                                    where selection.IsSelected && user.IsActive && user.EmailConfirmed
                                    select user).ToListAsync();
            if (recipients.Count == 0) throw new InvalidOperationException("Belum ada akun penerima email yang dipilih dan aktif.");

            var profile = await _context.BillingTrendProfiles.FirstOrDefaultAsync(x => x.DeviceKey == anomaly.DeviceKey && x.Status == "ACTIVE");
            if (profile == null)
            {
                profile = new BillingTrendProfile
                {
                    DeviceKey = anomaly.DeviceKey,
                    AnchorBillingPeriod = anomaly.BillingPeriod ?? anomaly.DetectedTime.Date,
                    AnchorAmount = anomaly.ActualBilling ?? anomaly.EstimatedBilling ?? 0m,
                    BaselineVersion = 0,
                    ValidConfirmationCount = 0,
                    Status = "ACTIVE"
                };
                _context.BillingTrendProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }
            var registry = await _context.DeviceRegistry.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DeviceKey == anomaly.DeviceKey);
            var lokasiMap = await _titikLokasiService.GetByDeviceKeysAsync(new[] { anomaly.DeviceKey });
            lokasiMap.TryGetValue(anomaly.DeviceKey, out var lokasi);
            var deviceInfo = new BillingDeviceInfo
            {
                GroupName = registry?.GroupName,
                DeviceId = registry?.DeviceId,
                KodeLokasi = lokasi?.KodeLokasi,
                Address = lokasi?.Address,
                KotaProvinsi = lokasi?.KotaProvinsi,
                IDPelanggan = lokasi?.IDPelanggan,
                DayaVA = lokasi?.DayaVA ?? 0m,
                Catatan = lokasi?.Catatan
            };
            foreach (var user in recipients)
            {
                var rawToken = await CreateTokenAsync(profile.Id, anomaly.Id, user.Id, TimeSpan.FromDays(7));
                var confirmationUrl = BuildConfirmationUrl(rawToken);
                var message = _emailService.BuildBillingConfirmationEmail(confirmationUrl, anomaly.DeviceKey, anomaly.BillingPeriod, anomaly.EstimatedBilling, anomaly.ActualBilling, anomaly.PeriodDifference ?? anomaly.BillingDifference, anomaly.PeriodDifferencePercent ?? anomaly.BillingDifferencePercent, anomaly.PreviousBilling, anomaly.PreviousBillingPeriod, DateTime.UtcNow.AddDays(7), deviceInfo);
                var eventKey = forceResend
                    ? anomaly.Id + ":" + user.Id + ":RESEND:" + Guid.NewGuid().ToString("N")
                    : anomaly.Id + ":" + user.Id;
                if (!await _context.BillingNotificationOutbox.AnyAsync(x => x.EventKey == eventKey && x.Channel == "EMAIL" && x.NotificationType == "BILLING_CONFIRMATION"))
                    AddOutbox(eventKey, anomaly.Id, "EMAIL", user.Email, "Konfirmasi anomaly tagihan", message);
                if (!string.IsNullOrWhiteSpace(user.WhatsAppNumber))
                    if (!await _context.BillingNotificationOutbox.AnyAsync(x => x.EventKey == eventKey && x.Channel == "WHATSAPP" && x.NotificationType == "BILLING_CONFIRMATION"))
                        AddOutbox(eventKey, anomaly.Id, "WHATSAPP", user.WhatsAppNumber, null, message);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<long?> EnsureDetectedAnomalyAsync(
            string deviceKey, DateTime billingPeriod, long erpBillingId,
            decimal estimatedBilling, decimal actualBilling, decimal difference, decimal? differencePercent,
            decimal threshold, string billingAnomalyType, decimal? previousBilling,
            DateTime? previousBillingPeriod, long? previousErpBillingId)
        {
            var isPeriodOverPeriod = IsPeriodOverPeriodAnomaly(billingAnomalyType);
            var isEstimateVsErp = IsEstimateVsErpAnomaly(billingAnomalyType);
            var periodDifference = previousBilling.HasValue
                ? actualBilling - previousBilling.Value
                : (decimal?)null;

            // Estimate-vs-actual is a regular note only. It must never enter
            // the three-confirmation trend/budget workflow or create a token.
            // A period-over-period anomaly requires two actual ERP periods.
            if (string.IsNullOrWhiteSpace(deviceKey) || erpBillingId <= 0
                || (!isPeriodOverPeriod && !isEstimateVsErp)
                || (isPeriodOverPeriod
                    ? !periodDifference.HasValue || periodDifference.Value <= threshold
                    : Math.Abs(difference) <= threshold))
                return null;
            var existing = await _context.AnomalyLogs.FirstOrDefaultAsync(x => x.AnomalyType == "BILLING_VARIANCE" && x.DeviceKey == deviceKey && x.BillingPeriod == billingPeriod.Date && x.ErpBillingId == erpBillingId);
            if (existing != null)
            {
                // If a row was first created for estimate-vs-actual and a valid
                // previous actual later becomes available, promote the same row
                // to the trend anomaly and notify it exactly once.
                if (isPeriodOverPeriod && !IsPeriodOverPeriodAnomaly(existing.BillingAnomalyType))
                {
                    existing.BillingAnomalyType = MergeBillingAnomalyTypes(
                        existing.BillingAnomalyType, "PERIOD_OVER_PERIOD");
                    existing.PreviousBilling = previousBilling;
                    existing.PreviousBillingPeriod = previousBillingPeriod;
                    existing.PreviousErpBillingId = previousErpBillingId;
                    existing.Notes = "Billing anomaly aktual periode berjalan dibanding aktual periode sebelumnya.";
                    await _context.SaveChangesAsync();
                }
                if (isPeriodOverPeriod && !existing.IsResolved)
                    await QueueAnomalyNotificationsAsync(existing.Id);
                return existing.Id;
            }
            var anomaly = new AnomalyLog
            {
                DeviceKey = deviceKey, DeviceId = deviceKey, AnomalyType = "BILLING_VARIANCE",
                BillingAnomalyType = string.IsNullOrWhiteSpace(billingAnomalyType) ? "ESTIMATE_VS_ERP" : billingAnomalyType,
                BillingPeriod = billingPeriod.Date, ErpBillingId = erpBillingId,
                EstimatedBilling = estimatedBilling, ActualBilling = actualBilling,
                BillingDifference = difference, BillingDifferencePercent = differencePercent,
                PreviousBilling = previousBilling, PreviousBillingPeriod = previousBillingPeriod,
                PreviousErpBillingId = previousErpBillingId, ThresholdValue = threshold,
                Deviation = differencePercent ?? 0m, DetectedTime = DateTime.UtcNow,
                Severity = Math.Abs(difference) >= threshold * 3m ? "critical" : "high",
                Notes = isPeriodOverPeriod
                    ? "Billing anomaly aktual periode berjalan dibanding aktual periode sebelumnya."
                    : "Catatan anomaly perbedaan estimasi sistem dan aktual ERP."
            };
            _context.AnomalyLogs.Add(anomaly);
            await _context.SaveChangesAsync();
            if (isPeriodOverPeriod)
                await QueueAnomalyNotificationsAsync(anomaly.Id);
            return anomaly.Id;
        }

        private static bool IsPeriodOverPeriodAnomaly(string billingAnomalyType)
        {
            return !string.IsNullOrWhiteSpace(billingAnomalyType)
                && billingAnomalyType.IndexOf("PERIOD_OVER_PERIOD", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string MergeBillingAnomalyTypes(string current, string addition)
        {
            var values = (current ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (!values.Any(x => string.Equals(x, addition, StringComparison.OrdinalIgnoreCase)))
                values.Add(addition);

            return string.Join(",", values);
        }

        private static bool IsEstimateVsErpAnomaly(string billingAnomalyType)
        {
            return !string.IsNullOrWhiteSpace(billingAnomalyType)
                && billingAnomalyType.IndexOf("ESTIMATE_VS_ERP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task<BillingConfirmationTokenView> GetTokenViewAsync(string rawToken)
        {
            var token = await FindValidTokenAsync(rawToken);
            if (token == null)
                return new BillingConfirmationTokenView { Valid = false, Error = "Link konfirmasi tidak valid, sudah digunakan, dicabut, atau kedaluwarsa." };
            var anomaly = token.AnomalyLogId.HasValue
                ? await _context.AnomalyLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == token.AnomalyLogId.Value)
                : null;
            if (anomaly == null || anomaly.IsResolved)
                return new BillingConfirmationTokenView { Valid = false, Error = "Anomaly sudah terkonfirmasi atau tidak ditemukan." };
            return new BillingConfirmationTokenView
            {
                Valid = true, AnomalyLogId = anomaly.Id, DeviceKey = anomaly.DeviceKey,
                BillingPeriod = anomaly.BillingPeriod, EstimatedBilling = anomaly.EstimatedBilling,
                ActualBilling = anomaly.ActualBilling, BillingDifference = anomaly.BillingDifference,
                PreviousBilling = anomaly.PreviousBilling, PreviousBillingPeriod = anomaly.PreviousBillingPeriod,
                PeriodDifference = anomaly.PeriodDifference, PeriodDifferencePercent = anomaly.PeriodDifferencePercent,
                ExpiresAt = token.ExpiresAt
            };
        }

        public async Task<BillingTrendConfirmationResult> ConfirmFromLinkAsync(string rawToken, string reason, string confirmedBy, int authenticatedUserId)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3 || reason.Length > 1000)
                throw new ArgumentException("Alasan konfirmasi wajib 3-1000 karakter.", nameof(reason));
            var token = await FindValidTokenAsync(rawToken);
            if (token == null) throw new InvalidOperationException("Link konfirmasi tidak valid atau sudah kedaluwarsa.");
            var anomaly = await _context.AnomalyLogs.FirstOrDefaultAsync(x => x.Id == token.AnomalyLogId.Value);
            if (anomaly == null || anomaly.IsResolved) throw new InvalidOperationException("Anomaly sudah terkonfirmasi atau tidak ditemukan.");
            if (!IsPeriodOverPeriodAnomaly(anomaly.BillingAnomalyType))
                throw new InvalidOperationException("Anomaly estimasi dan aktual hanya merupakan catatan biasa dan tidak masuk konfirmasi trend.");
            var user = await _context.ApplicationUsers.FirstOrDefaultAsync(x => x.Id == token.UserId && x.IsActive && x.EmailConfirmed);
            if (user == null || !await _context.BillingConfirmationRecipients.AnyAsync(x => x.UserId == user.Id && x.IsSelected))
                throw new InvalidOperationException("Akun penerima tidak lagi aktif atau tidak dipilih.");
            if (user.Id != authenticatedUserId)
                throw new InvalidOperationException("Link konfirmasi bukan milik akun yang sedang login.");

            var executionStrategy = _context.Database.CreateExecutionStrategy();
            await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                anomaly.IsResolved = true;
                anomaly.ResolvedBy = confirmedBy ?? user.Email;
                anomaly.ResolvedTime = DateTime.UtcNow;
                anomaly.Acknowledged = true;
                anomaly.AcknowledgedBy = anomaly.ResolvedBy;
                anomaly.AcknowledgedTime = DateTime.UtcNow;
                anomaly.OperatorAction = "billing_confirmation_link";
                anomaly.OperatorNotes = reason.Trim();
                _context.BillingAnomalyNotes.Add(new BillingAnomalyNote
                {
                    AnomalyLogId = anomaly.Id, UserId = user.Id, Author = anomaly.ResolvedBy,
                    NoteType = "CONFIRMATION", Decision = "VALID", Note = reason.Trim()
                });
                var tokens = await _context.BillingConfirmationTokens
                    .Where(x => x.AnomalyLogId == anomaly.Id && !x.UsedAt.HasValue && !x.RevokedAt.HasValue).ToListAsync();
                foreach (var item in tokens)
                {
                    item.UsedAt = item.Id == token.Id ? DateTime.UtcNow : item.UsedAt;
                    item.RevokedAt = item.Id == token.Id ? item.RevokedAt : DateTime.UtcNow;
                    item.UsedBy = anomaly.ResolvedBy;
                }
                var outboxEventPrefix = anomaly.Id + ":";
                var outboxRows = await _context.BillingNotificationOutbox
                    .Where(x => x.NotificationType == "BILLING_CONFIRMATION" && x.EventKey.StartsWith(outboxEventPrefix))
                    .ToListAsync();
                _context.BillingNotificationOutbox.RemoveRange(outboxRows);
                await _context.SaveChangesAsync();
                transaction.Commit();
                }
            });
            return new BillingTrendConfirmationResult { Accepted = true };
        }

        private async Task<BillingConfirmationToken> FindValidTokenAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken)) return null;
            var token = await _context.BillingConfirmationTokens.FirstOrDefaultAsync(x => x.TokenHash == Hash(rawToken) && x.Purpose == "BILLING_CONFIRMATION");
            return token == null || token.UsedAt.HasValue || token.RevokedAt.HasValue || token.ExpiresAt <= DateTime.UtcNow ? null : token;
        }

        private static string BuildConfirmationEmail(AnomalyLog anomaly, string url)
        {
            var period = anomaly.BillingPeriod.HasValue ? anomaly.BillingPeriod.Value.ToString("yyyy-MM-dd") : "-";
            return "<div style='font-family:Arial,sans-serif;max-width:640px'>" +
                "<h2>Konfirmasi Anomaly Tagihan</h2>" +
                "<p>Anomaly tagihan memerlukan konfirmasi Anda.</p>" +
                "<p><b>Perangkat:</b> " + System.Net.WebUtility.HtmlEncode(anomaly.DeviceKey) + "<br/>" +
                "<b>Periode:</b> " + period + "<br/>" +
                "<b>Selisih:</b> " + (anomaly.BillingDifference.HasValue ? anomaly.BillingDifference.Value.ToString("N2") : "-") + "</p>" +
                "<p><a href='" + System.Net.WebUtility.HtmlEncode(url) + "' style='background:#0d6efd;color:#fff;padding:12px 18px;text-decoration:none;border-radius:5px'>Buka Halaman Konfirmasi</a></p>" +
                "<p>Link berlaku 7 hari dan otomatis tidak berlaku setelah dikonfirmasi.</p></div>";
        }

        private string BuildConfirmationUrl(string rawToken)
        {
            var baseUrl = _configuration["PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = Environment.GetEnvironmentVariable("KWH_PUBLIC_BASE_URL");
            var request = _httpContextAccessor == null ? null : _httpContextAccessor.HttpContext;
            var configuredUri = Uri.TryCreate(baseUrl, UriKind.Absolute, out var configuredBaseUri)
                ? configuredBaseUri
                : null;
            var isLocalConfiguredUrl = configuredUri != null &&
                (string.Equals(configuredUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(configuredUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase));
            if ((string.IsNullOrWhiteSpace(baseUrl) || isLocalConfiguredUrl) && request != null)
                baseUrl = request.Request.Scheme + "://" + request.Request.Host.Value;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(baseUri.Host))
                throw new InvalidOperationException("PublicBaseUrl harus berupa URL absolut, contohnya http://localhost:5190 atau https://monitoring.perusahaan.com.");
            return baseUri.AbsoluteUri.TrimEnd('/') + "/BillingConfirmation?token=" + Uri.EscapeDataString(rawToken);
        }

        private Task<string> CreateTokenAsync(long profileId, long anomalyLogId, int userId, TimeSpan lifetime)
        {
            var raw = Convert.ToBase64String(GenerateRandomBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var token = new BillingConfirmationToken
            {
                TokenHash = Hash(raw), TrendProfileId = profileId, AnomalyLogId = anomalyLogId,
                UserId = userId, ExpiresAt = DateTime.UtcNow.Add(lifetime)
            };
            _context.BillingConfirmationTokens.Add(token);
            return Task.FromResult(raw);
        }

        private void AddOutbox(string eventKey, long anomalyLogId, string channel, string recipient, string subject, string message)
        {
            _context.BillingNotificationOutbox.Add(new BillingNotificationOutbox
            {
                EventKey = eventKey,
                Channel = channel,
                NotificationType = "BILLING_CONFIRMATION",
                PayloadJson = JsonConvert.SerializeObject(new { recipient, subject, message, anomalyLogId }),
                Status = "PENDING"
            });
        }

        private async Task<bool> IsWithinToleranceAsync(string deviceKey, BillingTrendProfile profile, decimal amount)
        {
            var settings = await _settingsService.GetEffectiveAsync(deviceKey);
            var reference = profile.ActiveBaselineAmount ?? profile.AnchorAmount;
            var tolerance = BillingConfigurationResolver.ResolveTolerance(settings.BillingTrendToleranceMode, settings.BillingTrendToleranceAmount, settings.BillingTrendTolerancePercent, reference);
            return Math.Abs(amount - reference) <= tolerance.EffectiveAmount;
        }

        private async Task ApplyBudgetAsync(string deviceKey, BillingTrendProfile profile, decimal baseline)
        {
            var settings = await _context.DeviceSettings.FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);
            if (settings == null || settings.MonthlyBillingBudgetManualOverride) return;
            var amount = BillingConfigurationResolver.ResolveBudget(baseline, settings.MonthlyBillingBudgetAmount);
            var key = deviceKey + ":TREND_AUTO:" + profile.BaselineVersion;
            var exists = await _context.BillingBudgetHistories.AnyAsync(x => x.IdempotencyKey == key);
            if (exists) return;
            _context.BillingBudgetHistories.Add(new BillingBudgetHistory
            {
                DeviceKey = deviceKey,
                PreviousAmount = settings.MonthlyBillingBudgetAmount,
                Amount = amount,
                Source = amount == BillingConfigurationResolver.InvalidConfigurationFallback ? "FALLBACK" : "TREND_AUTO",
                TrendProfileId = profile.Id,
                BaselineVersion = profile.BaselineVersion,
                IdempotencyKey = key,
                ChangedBy = "billing-trend-workflow"
            });
            settings.MonthlyBillingBudgetAmount = amount;
            settings.MonthlyBillingBudgetSource = amount == BillingConfigurationResolver.InvalidConfigurationFallback ? "FALLBACK" : "TREND_AUTO";
            settings.MonthlyBillingBudgetTrendProfileId = profile.Id;
            settings.MonthlyBillingBudgetBaselineVersion = profile.BaselineVersion;
            settings.MonthlyBillingBudgetUpdatedAt = DateTime.UtcNow;
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var item in bytes) builder.Append(item.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
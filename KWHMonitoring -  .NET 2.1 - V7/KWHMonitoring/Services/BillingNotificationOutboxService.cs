using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace KWHMonitoring.Services
{
    public sealed class BillingNotificationOutboxService : BackgroundService
    {
        private const int MaxAttempts = 5;
        private readonly IServiceProvider _services;
        private readonly ILogger<BillingNotificationOutboxService> _logger;

        public BillingNotificationOutboxService(IServiceProvider services, ILogger<BillingNotificationOutboxService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Billing notification outbox batch failed.");
                }
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var notification = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var now = DateTime.UtcNow;
                var staleProcessingCutoff = now.AddMinutes(-10);
                var rows = await db.BillingNotificationOutbox
                    .Where(x => ((x.Status == "PENDING" || x.Status == "RETRY")
                            && (!x.NextAttemptAt.HasValue || x.NextAttemptAt <= now))
                        || (x.Status == "PROCESSING" && x.ClaimedAt.HasValue && x.ClaimedAt <= staleProcessingCutoff))
                    .Where(x => x.AttemptCount < MaxAttempts)
                    .OrderBy(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    row.Status = "PROCESSING";
                    row.ClaimedAt = now;
                    row.AttemptCount++;
                    row.UpdatedAt = now;
                }
                await db.SaveChangesAsync(cancellationToken);

                foreach (var row in rows)
                {
                    try
                    {
                        var payload = JObject.Parse(row.PayloadJson ?? "{}");
                        var anomalyId = (long?)payload["anomalyLogId"];
                        if (string.Equals(row.Channel, "WEB_MODAL", StringComparison.OrdinalIgnoreCase))
                        {
                            row.Status = "SENT";
                            row.UpdatedAt = DateTime.UtcNow;
                            continue;
                        }
                        if (anomalyId.HasValue && await db.AnomalyLogs.AnyAsync(x => x.Id == anomalyId.Value && x.IsResolved, cancellationToken))
                        {
                            db.BillingNotificationOutbox.Remove(row);
                            continue;
                        }
                        var message = (string)payload["message"] ?? row.PayloadJson;
                        var success = false;
                        string providerId = null;
                        if (string.Equals(row.Channel, "EMAIL", StringComparison.OrdinalIgnoreCase))
                        {
                            var emailResult = await notification.SendEmailWithResultAsync((string)payload["recipient"], (string)payload["subject"] ?? "Billing anomaly", message);
                            success = emailResult.Success;
                            providerId = emailResult.Message;
                            row.LastError = success ? null : emailResult.Message;
                        }
                        else if (string.Equals(row.Channel, "WHATSAPP", StringComparison.OrdinalIgnoreCase))
                        {
                            var response = await notification.SendWablasAsync(message, (string)payload["recipient"]);
                            success = response != null && response.Success;
                            providerId = response == null ? null : response.Message;
                        }
                        else throw new InvalidOperationException("Unsupported billing notification channel.");

                        row.Status = success ? "SENT" : (row.AttemptCount >= MaxAttempts ? "FAILED" : "RETRY");
                        row.SentAt = success ? now : (DateTime?)null;
                        row.ProviderMessageId = providerId;
                        row.NextAttemptAt = success ? (DateTime?)null : now.AddMinutes(Math.Min(60, row.AttemptCount * row.AttemptCount));
                        if (!success && string.IsNullOrWhiteSpace(row.LastError))
                            row.LastError = "Provider returned an unsuccessful response.";
                    }
                    catch (Exception ex)
                    {
                        row.Status = row.AttemptCount >= MaxAttempts ? "FAILED" : "RETRY";
                        row.NextAttemptAt = now.AddMinutes(Math.Min(60, row.AttemptCount * row.AttemptCount));
                        row.LastError = ex.Message.Length > 1000 ? ex.Message.Substring(0, 1000) : ex.Message;
                    }
                    row.UpdatedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
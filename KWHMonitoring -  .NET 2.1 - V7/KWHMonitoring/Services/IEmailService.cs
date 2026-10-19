using System;
using System.Threading.Tasks;

namespace KWHMonitoring.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body);
        Task<bool> SendVerificationEmailAsync(string email, string verificationLink);
        Task<bool> SendAccessRequestToAdminAsync(string adminEmail, string userEmail, string displayName, string approvalLink);
        Task<bool> SendApprovalConfirmationAsync(string email, string role);
        Task<bool> SendAccessRejectedNoticeAsync(string email);
        Task<bool> SendPasswordResetEmailAsync(string email, string resetLink);
        Task<bool> SendBillingConfirmationEmailAsync(string email, string confirmationLink, string deviceKey, DateTime? billingPeriod, decimal? estimatedBilling, decimal? actualBilling, decimal? difference, decimal? differencePercent, decimal? previousBilling, DateTime? previousBillingPeriod, DateTime expiresAt, BillingDeviceInfo deviceInfo = null);
        string BuildBillingConfirmationEmail(string confirmationLink, string deviceKey, DateTime? billingPeriod, decimal? estimatedBilling, decimal? actualBilling, decimal? difference, decimal? differencePercent, decimal? previousBilling, DateTime? previousBillingPeriod, DateTime expiresAt, BillingDeviceInfo deviceInfo = null);
        Task<bool> SendCriticalActionNotificationAsync(string actorEmail, string action, string details);
        Task<bool> SendRelayOtpAsync(string email, string code, string groupName, string actionText);
    }
}

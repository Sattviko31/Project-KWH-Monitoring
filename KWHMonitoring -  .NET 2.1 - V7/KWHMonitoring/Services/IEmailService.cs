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
        Task<bool> SendCriticalActionNotificationAsync(string actorEmail, string action, string details);
    }
}

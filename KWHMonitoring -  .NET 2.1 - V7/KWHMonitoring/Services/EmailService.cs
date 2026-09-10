using System;
using System.Net;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Services
{
    public class EmailService : IEmailService
    {
        private readonly NotificationService _notificationService;
        private readonly ILogger<EmailService> _logger;

        public EmailService(NotificationService notificationService, ILogger<EmailService> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            return await _notificationService.SendEmailAsync(to, subject, body);
        }

        public async Task<bool> SendVerificationEmailAsync(string email, string verificationLink)
        {
            var subject = "Verifikasi Email - KWH Monitoring";
            var body = $@"<html><body style='font-family:Arial,sans-serif'>
                <h2 style='color:#0d6efd'>Verifikasi Email Anda</h2>
                <p>Terima kasih telah mendaftar di KWH Monitoring.</p>
                <p>Klik link berikut untuk memverifikasi email Anda:</p>
                <p><a href='{verificationLink}' style='padding:10px 20px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:5px'>Verifikasi Email</a></p>
                <p>Atau salin link ini ke browser:</p>
                <p>{verificationLink}</p>
                <p>Link ini akan kadaluarsa dalam 24 jam.</p>
                <p>Jika email tidak muncul di inbox, periksa juga folder Spam.</p>
                <hr>
                <p style='font-size:12px;color:#666'>Jika Anda tidak mendaftar, abaikan email ini.</p>
            </body></html>";

            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendAccessRequestToAdminAsync(string adminEmail, string userEmail, string displayName, string approvalLink)
        {
            var subject = "[KWH Monitoring] Permintaan Akses Pengguna Baru";
            var body = $@"<html><body style='font-family:Arial,sans-serif'>
                <h2 style='color:#0d6efd'>Permintaan Akses Pengguna Baru</h2>
                <p>Pengguna berikut telah memverifikasi email dan meminta akses ke sistem KWH Monitoring:</p>
                <ul>
                    <li><strong>Email:</strong> {WebUtility.HtmlEncode(userEmail)}</li>
                    <li><strong>Nama:</strong> {WebUtility.HtmlEncode(displayName)}</li>
                </ul>
                <p>Klik link berikut untuk menyetujui atau menolak akses:</p>
                <p><a href='{approvalLink}' style='padding:10px 20px;background:#198754;color:#fff;text-decoration:none;border-radius:5px'>Proses Persetujuan</a></p>
                <p>Atau salin link ini ke browser:</p>
                <p>{approvalLink}</p>
                <p>Link ini akan kadaluarsa dalam 24 jam.</p>
            </body></html>";

            return await _notificationService.SendEmailAsync(adminEmail, subject, body);
        }

        public async Task<bool> SendApprovalConfirmationAsync(string email, string role)
        {
            var subject = "[KWH Monitoring] Akses Anda Telah Disetujui";
            var body = $@"<html><body style='font-family:Arial,sans-serif'>
                <h2 style='color:#198754'>Akses Disetujui</h2>
                <p>Permintaan akses Anda telah disetujui.</p>
                <p><strong>Role:</strong> {WebUtility.HtmlEncode(role)}</p>
                <p>Anda sekarang dapat login ke sistem KWH Monitoring.</p>
            </body></html>";

            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendAccessRejectedNoticeAsync(string email)
        {
            var subject = "[KWH Monitoring] Permintaan Akses Ditolak";
            var body = $@"<html><body style='font-family:Arial,sans-serif'>
                <h2 style='color:#dc3545'>Akses Ditolak</h2>
                <p>Permintaan akses Anda ke sistem KWH Monitoring telah ditolak oleh administrator.</p>
                <p>Jika menurut Anda ini keliru, silakan hubungi administrator sistem.</p>
            </body></html>";

            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var subject = "[KWH Monitoring] Reset Password";
            var body = $@"<html><body style='font-family:Arial,sans-serif'>
                <h2 style='color:#0d6efd'>Reset Password</h2>
                <p>Klik link berikut untuk mereset password Anda:</p>
                <p><a href='{resetLink}' style='padding:10px 20px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:5px'>Reset Password</a></p>
                <p>Atau salin link ini ke browser:</p>
                <p>{resetLink}</p>
                <p>Link ini akan kadaluarsa dalam 1 jam.</p>
                <p>Jika email tidak muncul di inbox, periksa juga folder Spam.</p>
                <p>Jika Anda tidak meminta reset password, abaikan email ini.</p>
            </body></html>";

            return await _notificationService.SendEmailAsync(email, subject, body);
        }
    }
}

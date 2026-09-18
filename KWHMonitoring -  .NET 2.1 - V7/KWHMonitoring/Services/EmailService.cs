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

        /// <summary>
        /// Membungkus konten email dengan template modern, user-friendly dan kompatibel dengan Gmail/Outlook.
        /// </summary>
        private string BuildEmailTemplate(string title, string contentHtml, string accentColor = "#0d6efd")
        {
            return $@"<!DOCTYPE html>
<html lang=""id"">
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>{WebUtility.HtmlEncode(title)}</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f6fb;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" bgcolor=""#f4f6fb"" style=""background-color:#f4f6fb;"">
        <tr>
            <td align=""center"" style=""padding:40px 20px;"">
                <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""width:600px;max-width:600px;background-color:#ffffff;border-radius:16px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;"">
                    <tr>
                        <td style=""padding:40px 40px 0 40px;text-align:center;"">
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" align=""center"" style=""margin:0 auto;background-color:{accentColor};border-radius:16px;"">
                                <tr>
                                    <td style=""width:64px;height:64px;text-align:center;vertical-align:middle;"">
                                        <span style=""font-size:30px;color:#ffffff;line-height:64px;"">&#9889;</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:24px 40px 0 40px;text-align:center;"">
                            <h1 style=""margin:0;font-size:24px;font-weight:700;color:#1a1d23;font-family:Arial,Helvetica,sans-serif;"">{WebUtility.HtmlEncode(title)}</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:24px 40px 32px 40px;font-size:16px;line-height:1.7;color:#495057;"">
                            {contentHtml}
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:0 40px 40px 40px;border-top:1px solid #e9ecef;"">
                            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""padding-top:24px;"">
                                <tr>
                                    <td style=""font-size:12px;color:#6c757d;text-align:center;line-height:1.6;font-family:Arial,Helvetica,sans-serif;"">
                                        <p style=""margin:0 0 8px 0;"">Email ini dikirim secara otomatis oleh sistem <strong>KWH Monitoring</strong>.</p>
                                        <p style=""margin:0;"">Jika Anda tidak merasa melakukan permintaan ini, abaikan email ini atau hubungi administrator.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private string CtaButton(string url, string label, string color = "#0d6efd")
        {
            return $@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-radius:10px;"">
    <tr>
        <td align=""center"" bgcolor=""{color}"" style=""background-color:{color};border-radius:10px;"">
            <a href=""{url}"" style=""display:inline-block;padding:14px 28px;background-color:{color};color:#ffffff;text-decoration:none;border-radius:10px;font-weight:600;font-size:15px;font-family:Arial,Helvetica,sans-serif;"">{WebUtility.HtmlEncode(label)}</a>
        </td>
    </tr>
</table>";
        }

        public async Task<bool> SendVerificationEmailAsync(string email, string verificationLink)
        {
            var subject = "Verifikasi Email - KWH Monitoring";
            var title = "Verifikasi Email Anda";
            var content = $@"<p style=""margin:0 0 16px 0;"">Halo,</p>
<p style=""margin:0 0 16px 0;"">Terima kasih telah mendaftar di <strong>KWH Monitoring</strong>. Untuk melanjutkan, silakan verifikasi email Anda dengan menekan tombol di bawah ini.</p>
<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0;"">
    <tr><td>{CtaButton(verificationLink, "Verifikasi Email")}</td></tr>
</table>
<p style=""margin:0 0 8px 0;"">Atau salin link berikut ke browser Anda:</p>
<p style=""margin:0 0 16px 0;word-break:break-all;""><a href=""{verificationLink}"" style=""color:#0d6efd;text-decoration:none;"">{verificationLink}</a></p>
<p style=""margin:0 0 8px 0;""><i>&#9201;</i> Link ini berlaku selama <strong>24 jam</strong>.</p>
<p style=""margin:0;""><i>&#128231;</i> Jika email tidak muncul di inbox, periksa juga folder Spam.</p>";

            var body = BuildEmailTemplate(title, content, "#0d6efd");
            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendAccessRequestToAdminAsync(string adminEmail, string userEmail, string displayName, string approvalLink)
        {
            var subject = "[KWH Monitoring] Permintaan Akses Pengguna Baru";
            var title = "Permintaan Akses Pengguna Baru";
            var content = $@"<p style=""margin:0 0 16px 0;"">Halo Admin,</p>
<p style=""margin:0 0 16px 0;"">Pengguna berikut telah memverifikasi email dan meminta akses ke sistem <strong>KWH Monitoring</strong>:</p>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background:#f8f9fa;border-radius:10px;margin:16px 0;"">
    <tr>
        <td style=""padding:16px 20px;"">
            <p style=""margin:0 0 8px 0;""><strong>Email:</strong> {WebUtility.HtmlEncode(userEmail)}</p>
            <p style=""margin:0;""><strong>Nama:</strong> {WebUtility.HtmlEncode(displayName)}</p>
        </td>
    </tr>
</table>
<p style=""margin:0 0 16px 0;"">Klik tombol di bawah untuk menyetujui atau menolak akses pengguna:</p>
<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0;"">
    <tr><td>{CtaButton(approvalLink, "Proses Persetujuan", "#198754")}</td></tr>
</table>
<p style=""margin:0 0 8px 0;"">Atau salin link ini ke browser:</p>
<p style=""margin:0 0 16px 0;word-break:break-all;""><a href=""{approvalLink}"" style=""color:#0d6efd;text-decoration:none;"">{approvalLink}</a></p>
<p style=""margin:0;""><i>&#9201;</i> Link ini berlaku selama <strong>24 jam</strong>.</p>";

            var body = BuildEmailTemplate(title, content, "#198754");
            return await _notificationService.SendEmailAsync(adminEmail, subject, body);
        }

        public async Task<bool> SendApprovalConfirmationAsync(string email, string role)
        {
            var subject = "[KWH Monitoring] Akses Anda Telah Disetujui";
            var title = "Akses Disetujui";
            var content = $@"<p style=""margin:0 0 16px 0;"">Halo,</p>
<p style=""margin:0 0 16px 0;"">Selamat! Permintaan akses Anda ke <strong>KWH Monitoring</strong> telah disetujui oleh administrator.</p>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;margin:16px 0;"">
    <tr>
        <td style=""padding:16px 20px;"">
            <p style=""margin:0;""><strong>Role:</strong> {WebUtility.HtmlEncode(role)}</p>
        </td>
    </tr>
</table>
<p style=""margin:0 0 16px 0;"">Anda sekarang dapat login ke sistem dan mulai menggunakan fitur KWH Monitoring.</p>
<div style=""background:#f0fdf4;border:1px solid #bbf7d0;border-radius:10px;padding:16px 20px;margin:16px 0;"">
    <p style=""margin:0;color:#0f5132;"">Silakan kunjungi halaman login KWH Monitoring untuk masuk ke akun Anda.</p>
</div>";

            var body = BuildEmailTemplate(title, content, "#198754");
            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendAccessRejectedNoticeAsync(string email)
        {
            var subject = "[KWH Monitoring] Permintaan Akses Ditolak";
            var title = "Akses Ditolak";
            var content = $@"<p style=""margin:0 0 16px 0;"">Halo,</p>
<p style=""margin:0 0 16px 0;"">Kami informasikan bahwa permintaan akses Anda ke <strong>KWH Monitoring</strong> telah ditolak oleh administrator.</p>
<div style=""background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:16px 20px;margin:16px 0;"">
    <p style=""margin:0;color:#842029;"">Jika Anda merasa ini adalah kesalahan, silakan hubungi administrator sistem untuk informasi lebih lanjut.</p>
</div>";

            var body = BuildEmailTemplate(title, content, "#dc3545");
            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var subject = "[KWH Monitoring] Reset Password";
            var title = "Reset Password";
            var content = $@"<p style=""margin:0 0 16px 0;"">Halo,</p>
<p style=""margin:0 0 16px 0;"">Kami menerima permintaan reset password untuk akun Anda di <strong>KWH Monitoring</strong>. Klik tombol di bawah untuk membuat password baru.</p>
<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin:24px 0;"">
    <tr><td>{CtaButton(resetLink, "Reset Password")}</td></tr>
</table>
<p style=""margin:0 0 8px 0;"">Atau salin link berikut ke browser Anda:</p>
<p style=""margin:0 0 16px 0;word-break:break-all;""><a href=""{resetLink}"" style=""color:#0d6efd;text-decoration:none;"">{resetLink}</a></p>
<p style=""margin:0 0 8px 0;""><i>&#9201;</i> Link ini berlaku selama <strong>1 jam</strong>.</p>
<p style=""margin:0;""><i>&#128683;</i> Jika Anda tidak meminta reset password, abaikan email ini.</p>";

            var body = BuildEmailTemplate(title, content, "#0d6efd");
            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendRelayOtpAsync(string email, string code, string groupName, string actionText)
        {
            var subject = "[KWH Monitoring] Kode OTP Verifikasi Relay";
            var title = "Kode Verifikasi OTP";
            var maskedEmail = email.Length > 3
                ? email.Substring(0, 3) + "***" + email.Substring(email.IndexOf('@'))
                : "***";

            var content = $@"<p style=""margin:0 0 16px 0;"">Halo,</p>
<p style=""margin:0 0 16px 0;"">Anda telah meminta kode verifikasi untuk perintah relay di <strong>KWH Monitoring</strong>:</p>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background:#fef3c7;border:1px solid #fde68a;border-radius:10px;margin:16px 0;"">
    <tr>
        <td style=""padding:16px 20px;text-align:center;"">
            <p style=""margin:0 0 4px 0;font-size:13px;color:#92400e;font-weight:600;text-transform:uppercase;letter-spacing:1px;"">Kode OTP Anda</p>
            <p style=""margin:0;font-size:36px;font-weight:800;color:#1a1d23;letter-spacing:8px;font-family:'Courier New',monospace;"">{WebUtility.HtmlEncode(code)}</p>
        </td>
    </tr>
</table>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse:collapse;background:#ffffff;border:1px solid #e9ecef;border-radius:10px;overflow:hidden;margin:16px 0;"">
    <tr style=""border-bottom:1px solid #e9ecef;"">
        <td style=""padding:12px 16px;width:120px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Perangkat</td>
        <td style=""padding:12px 16px;color:#495057;"">{WebUtility.HtmlEncode(groupName)}</td>
    </tr>
    <tr style=""border-bottom:1px solid #e9ecef;"">
        <td style=""padding:12px 16px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Aksi</td>
        <td style=""padding:12px 16px;color:#dc3545;font-weight:600;"">{WebUtility.HtmlEncode(actionText)}</td>
    </tr>
    <tr>
        <td style=""padding:12px 16px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Waktu</td>
        <td style=""padding:12px 16px;color:#495057;"">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td>
    </tr>
</table>
<div style=""background:#fef2f2;border:1px solid #fecaca;border-radius:10px;padding:16px 20px;margin:16px 0;"">
    <p style=""margin:0 0 8px 0;color:#842029;font-weight:600;"">&#9888;&#65039; Catatan Keamanan:</p>
    <ul style=""margin:0;padding-left:20px;color:#842029;line-height:1.8;"">
        <li>Kode ini berlaku selama <strong>5 menit</strong>.</li>
        <li>Jangan berikan kode ini kepada siapa pun.</li>
        <li>Jika Anda tidak merasa melakukan permintaan ini, segera hubungi administrator.</li>
    </ul>
</div>";

            var body = BuildEmailTemplate(title, content, "#dc3545");
            return await _notificationService.SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendCriticalActionNotificationAsync(string actorEmail, string action, string details)
        {
            var masterAdminEmail = await _notificationService.GetMasterAdminEmailAsync();
            if (string.IsNullOrWhiteSpace(masterAdminEmail))
            {
                _logger.LogWarning("Master admin email not configured; skipping critical action notification.");
                return false;
            }

            var now = DateTime.UtcNow;
            var subject = "[KWH Monitoring] Aksi Kritis Terdeteksi";
            var title = "Aksi Kritis pada KWH Monitoring";
            var content = $@"<p style=""margin:0 0 16px 0;"">Halo Admin,</p>
<p style=""margin:0 0 16px 0;"">Aksi berikut dilakukan oleh pengguna dan memerlukan perhatian Anda:</p>
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse:collapse;background:#ffffff;border:1px solid #e9ecef;border-radius:10px;overflow:hidden;"">
    <tr style=""border-bottom:1px solid #e9ecef;"">
        <td style=""padding:12px 16px;width:120px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Waktu</td>
        <td style=""padding:12px 16px;color:#495057;"">{now:yyyy-MM-dd HH:mm:ss} UTC</td>
    </tr>
    <tr style=""border-bottom:1px solid #e9ecef;"">
        <td style=""padding:12px 16px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Actor</td>
        <td style=""padding:12px 16px;color:#495057;"">{WebUtility.HtmlEncode(actorEmail ?? "unknown")}</td>
    </tr>
    <tr style=""border-bottom:1px solid #e9ecef;"">
        <td style=""padding:12px 16px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Aksi</td>
        <td style=""padding:12px 16px;color:#495057;"">{WebUtility.HtmlEncode(action)}</td>
    </tr>
    <tr>
        <td style=""padding:12px 16px;font-weight:bold;color:#1a1d23;background:#f8f9fa;"">Detail</td>
        <td style=""padding:12px 16px;color:#495057;"">{WebUtility.HtmlEncode(details)}</td>
    </tr>
</table>
<p style=""margin:16px 0 0 0;font-size:13px;color:#6c757d;"">Email ini dikirim secara otomatis oleh sistem KWH Monitoring.</p>";

            var body = BuildEmailTemplate(title, content, "#dc3545");
            return await _notificationService.SendEmailAsync(masterAdminEmail, subject, body);
        }
    }
}

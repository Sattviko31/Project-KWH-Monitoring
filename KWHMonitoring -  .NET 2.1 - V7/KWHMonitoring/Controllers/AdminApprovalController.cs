using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KWHMonitoring.Models;
using KWHMonitoring.Services;

namespace KWHMonitoring.Controllers
{
    public class AdminApprovalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminApprovalController> _logger;

        public AdminApprovalController(
            ApplicationDbContext context,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<AdminApprovalController> logger)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        [Route("AdminApproval/Approve")]
        public async Task<IActionResult> Approve(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return View("Error", new ErrorViewModel { Message = "Link persetujuan tidak valid." });
            }

            var tokenHash = HashToken(token);
            var normalizedEmail = NormalizeEmail(email);

            var approvalToken = await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    x.Purpose == TokenPurpose.ApprovalRequest &&
                    x.User.NormalizedEmail == normalizedEmail);

            if (approvalToken == null || !approvalToken.IsValid)
            {
                return View("Error", new ErrorViewModel { Message = "Link persetujuan tidak valid atau sudah kadaluarsa." });
            }

            var user = approvalToken.User;

            if (user.EmailConfirmed == false)
            {
                return View("Error", new ErrorViewModel { Message = "User belum memverifikasi email." });
            }

            ViewBag.UserEmail = user.Email;
            ViewBag.DisplayName = user.DisplayName;
            ViewBag.RegisteredAt = user.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            ViewBag.Token = token;

            return View();
        }

        [HttpPost]
        [Route("AdminApproval/Approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string email, string token, string role)
        {
            var validRoles = new[] { UserRoles.Viewer, UserRoles.Operator };

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || !validRoles.Contains(role))
            {
                ModelState.AddModelError(string.Empty, "Data persetujuan tidak valid.");
                return View();
            }

            var tokenHash = HashToken(token);
            var normalizedEmail = NormalizeEmail(email);

            var approvalToken = await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    x.Purpose == TokenPurpose.ApprovalRequest &&
                    x.User.NormalizedEmail == normalizedEmail);

            if (approvalToken == null || !approvalToken.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Link persetujuan tidak valid atau sudah kadaluarsa.");
                return View();
            }

            var user = approvalToken.User;

            if (user.EmailConfirmed == false)
            {
                ModelState.AddModelError(string.Empty, "User belum memverifikasi email.");
                return View();
            }

            user.Role = role;
            user.IsActive = true;
            approvalToken.IsUsed = true;
            approvalToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.RoleChanged, true,
                $"Role diubah menjadi {role} oleh master admin", ipAddress, userAgent);

            await _emailService.SendApprovalConfirmationAsync(user.Email, role);

            ViewBag.Approved = true;
            ViewBag.Role = role;
            ViewBag.UserEmail = user.Email;

            return View();
        }

        [HttpGet]
        [Route("AdminApproval/Reject")]
        public async Task<IActionResult> Reject(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return View("Error", new ErrorViewModel { Message = "Link penolakan tidak valid." });
            }

            var tokenHash = HashToken(token);
            var normalizedEmail = NormalizeEmail(email);

            var approvalToken = await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    x.Purpose == TokenPurpose.ApprovalRequest &&
                    x.User.NormalizedEmail == normalizedEmail);

            if (approvalToken == null || !approvalToken.IsValid)
            {
                return View("Error", new ErrorViewModel { Message = "Link penolakan tidak valid atau sudah kadaluarsa." });
            }

            var user = approvalToken.User;

            user.IsActive = false;
            approvalToken.IsUsed = true;
            approvalToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.AccessDenied, true,
                "Akses ditolak oleh master admin", ipAddress, userAgent);

            ViewBag.Rejected = true;
            ViewBag.UserEmail = user.Email;

            return View();
        }

        private async Task LogSecurityActionAsync(int? userId, string email, SecurityAction action, bool success,
            string details, string ipAddress, string userAgent)
        {
            _context.SecurityAuditLogs.Add(new SecurityAuditLog
            {
                UserId = userId,
                Email = email ?? string.Empty,
                Action = action,
                Success = success,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        private static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            var trimmed = email.Trim().ToLowerInvariant();
            var regex = new System.Text.RegularExpressions.Regex("[^a-z0-9.@_+-]");
            return regex.Replace(trimmed, "");
        }

        private static string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(token);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
    }
}

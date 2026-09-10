using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KWHMonitoring.Models;
using KWHMonitoring.Services;

namespace KWHMonitoring.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public UserManagementController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private async Task<string> GetMasterAdminEmailAsync()
        {
            var record = await _context.AppSettingsRecords
                .FirstOrDefaultAsync(x => x.SettingKey == "Notification.MasterAdminEmail");
            return record?.SettingValue ?? string.Empty;
        }

        private async Task<bool> IsMasterAdminAsync()
        {
            var masterAdminEmail = await GetMasterAdminEmailAsync();
            return !string.IsNullOrEmpty(masterAdminEmail) &&
                string.Equals(User.Identity.Name, masterAdminEmail, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> Index()
        {
            if (!await IsMasterAdminAsync())
            {
                return PartialView("_AccessDenied");
            }

            var masterAdminEmail = await GetMasterAdminEmailAsync();
            var users = await _context.ApplicationUsers
                .Where(x => x.Email != masterAdminEmail)
                .OrderBy(x => x.Email)
                .ToListAsync();

            return PartialView("_UserManagementTab", users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(int id, string role)
        {
            if (!await IsMasterAdminAsync())
            {
                return Json(new { success = false, message = "Akses ditolak." });
            }

            var validRoles = new[] { UserRoles.Viewer, UserRoles.Operator, UserRoles.Admin };
            if (!validRoles.Contains(role))
            {
                return Json(new { success = false, message = "Role tidak valid." });
            }

            var user = await _context.ApplicationUsers.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User tidak ditemukan." });
            }

            var masterAdminEmail = await GetMasterAdminEmailAsync();
            if (string.Equals(user.Email, masterAdminEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Tidak dapat mengubah master admin." });
            }

            var previousRole = user.Role;
            user.Role = role;
            await _context.SaveChangesAsync();

            await LogSecurityActionAsync(SecurityAction.RoleChanged,
                $"Role user {user.Email} diubah dari {previousRole} menjadi {role}", true);

            await _emailService.SendCriticalActionNotificationAsync(
                User.Identity.Name,
                "Role Changed",
                $"User {user.Email} role changed from {previousRole} to {role}.");

            return Json(new { success = true, message = "Role berhasil diperbarui." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, bool isActive)
        {
            if (!await IsMasterAdminAsync())
            {
                return Json(new { success = false, message = "Akses ditolak." });
            }

            var user = await _context.ApplicationUsers.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User tidak ditemukan." });
            }

            var masterAdminEmail = await GetMasterAdminEmailAsync();
            if (string.Equals(user.Email, masterAdminEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Tidak dapat mengubah status master admin." });
            }

            user.IsActive = isActive;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Status user berhasil diperbarui." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsMasterAdminAsync())
            {
                return Json(new { success = false, message = "Akses ditolak." });
            }

            var user = await _context.ApplicationUsers.FindAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User tidak ditemukan." });
            }

            var masterAdminEmail = await GetMasterAdminEmailAsync();
            if (string.Equals(user.Email, masterAdminEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Tidak dapat menghapus master admin." });
            }

            // Remove related tokens before deleting the user
            var tokens = await _context.EmailVerificationTokens
                .Where(x => x.UserId == id)
                .ToListAsync();
            if (tokens.Any())
            {
                _context.EmailVerificationTokens.RemoveRange(tokens);
                await _context.SaveChangesAsync();
            }

            _context.ApplicationUsers.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "User berhasil dihapus." });
        }

        private async Task LogSecurityActionAsync(SecurityAction action, string details, bool success)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? userId = int.TryParse(userIdClaim, out var parsedId) ? (int?)parsedId : null;
                var email = User.Identity.Name ?? "unknown";
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                _context.SecurityAuditLogs.Add(new SecurityAuditLog
                {
                    UserId = userId,
                    Email = email,
                    Action = action,
                    Success = success,
                    Details = details,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Audit log failure should not break the main flow
            }
        }
    }
}

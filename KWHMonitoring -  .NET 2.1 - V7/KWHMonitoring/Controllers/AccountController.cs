using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KWHMonitoring.Models;
using KWHMonitoring.Services;

namespace KWHMonitoring.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        public AccountController(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<AccountController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        [Route("Account/Login")]
        public IActionResult Login(string returnUrl = "")
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [Route("Account/Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = "")
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = NormalizeEmail(model.Email);
            var user = await _context.ApplicationUsers
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            if (user == null)
            {
                await LogSecurityActionAsync(null, model.Email, SecurityAction.LoginFailed, false,
                    "User tidak ditemukan", ipAddress, userAgent);
                ModelState.AddModelError(string.Empty, "Email atau password salah.");
                return View(model);
            }

            if (user.IsLockedOut)
            {
                await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.Lockout, false,
                    $"Akun terkunci hingga {user.LockoutEnd:yyyy-MM-dd HH:mm:ss}", ipAddress, userAgent);
                ModelState.AddModelError(string.Empty, $"Akun terkunci. Coba lagi setelah {user.LockoutEnd:yyyy-MM-dd HH:mm:ss}.");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.LoginFailed, false,
                    "Email belum diverifikasi", ipAddress, userAgent);
                ModelState.AddModelError(string.Empty, "Email belum diverifikasi. Silakan cek kotak masuk email Anda.");
                return View(model);
            }

            if (!user.IsActive)
            {
                await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.LoginFailed, false,
                    "Akun tidak aktif", ipAddress, userAgent);
                ModelState.AddModelError(string.Empty, "Akun tidak aktif. Silakan hubungi administrator.");
                return View(model);
            }

            if (user.Role == UserRoles.None)
            {
                await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.LoginFailed, false,
                    "Akun menunggu persetujuan admin", ipAddress, userAgent);
                ModelState.AddModelError(string.Empty, "Akun Anda masih menunggu persetujuan admin.");
                return View(model);
            }

            if (!PasswordHasher.VerifyPassword(model.Password, user.PasswordHash))
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.Lockout, false,
                        "Terlalu banyak percobaan gagal", ipAddress, userAgent);
                }

                await _context.SaveChangesAsync();
                await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.LoginFailed, false,
                    "Password salah", ipAddress, userAgent);
                ModelState.AddModelError(string.Empty, "Email atau password salah.");
                return View(model);
            }

            user.AccessFailedCount = 0;
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await SignInUserAsync(user, model.RememberMe);
            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.Login, true,
                "Login berhasil", ipAddress, userAgent);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Monitoring");
        }

        [HttpGet]
        [Route("Account/Register")]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [Route("Account/Register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = NormalizeEmail(model.Email);

            if (await _context.ApplicationUsers.AnyAsync(x => x.NormalizedEmail == normalizedEmail))
            {
                ModelState.AddModelError(string.Empty, "Email sudah terdaftar.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                Email = model.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                DisplayName = model.DisplayName.Trim(),
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                Role = UserRoles.None,
                EmailConfirmed = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.ApplicationUsers.Add(user);
            await _context.SaveChangesAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.Register, true,
                "Registrasi berhasil", ipAddress, userAgent);

            await SendVerificationEmailAsync(user);

            return RedirectToAction("RegistrationSuccess", new { email = user.Email });
        }

        [HttpGet]
        [Route("Account/RegistrationSuccess")]
        public IActionResult RegistrationSuccess(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpGet]
        [Route("Account/VerifyEmail")]
        public async Task<IActionResult> VerifyEmail(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                return View("Error", new ErrorViewModel { Message = "Link verifikasi tidak valid." });
            }

            var tokenHash = HashToken(token);
            var normalizedEmail = NormalizeEmail(email);

            var verificationToken = await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    x.Purpose == TokenPurpose.EmailVerification &&
                    x.User.NormalizedEmail == normalizedEmail);

            if (verificationToken == null || !verificationToken.IsValid)
            {
                return View("Error", new ErrorViewModel { Message = "Link verifikasi tidak valid atau sudah kadaluarsa." });
            }

            var user = verificationToken.User;
            user.EmailConfirmed = true;
            verificationToken.IsUsed = true;
            verificationToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.EmailVerified, true,
                "Email berhasil diverifikasi", ipAddress, userAgent);

            await SendAccessRequestToAdminAsync(user);

            return View("VerificationSuccess");
        }

        [HttpPost]
        [Route("Account/ResendVerification")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendVerification(string email)
        {
            var normalizedEmail = NormalizeEmail(email);
            var user = await _context.ApplicationUsers
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

            if (user != null && !user.EmailConfirmed)
            {
                await SendVerificationEmailAsync(user);
            }

            return RedirectToAction("Login");
        }

        [HttpGet]
        [Route("Account/ForgotPassword")]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [Route("Account/ForgotPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var normalizedEmail = NormalizeEmail(model.Email);
            var user = await _context.ApplicationUsers
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            if (user != null && user.EmailConfirmed && user.IsActive)
            {
                await SendPasswordResetEmailAsync(user);
                await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.PasswordResetRequested, true,
                    "Request reset password", ipAddress, userAgent);
            }

            return RedirectToAction("ForgotPasswordConfirmation");
        }

        [HttpGet]
        [Route("Account/ForgotPasswordConfirmation")]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [Route("Account/ResetPassword")]
        public IActionResult ResetPassword(string email, string token)
        {
            ViewBag.Email = email;
            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [Route("Account/ResetPassword")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Email = model.Email;
                ViewBag.Token = model.Token;
                return View(model);
            }

            var normalizedEmail = NormalizeEmail(model.Email);
            var tokenHash = HashToken(model.Token);

            var resetToken = await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.TokenHash == tokenHash &&
                    x.Purpose == TokenPurpose.PasswordReset &&
                    x.User.NormalizedEmail == normalizedEmail);

            if (resetToken == null || !resetToken.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Link reset password tidak valid atau sudah kadaluarsa.");
                return View(model);
            }

            var user = resetToken.User;
            user.PasswordHash = PasswordHasher.HashPassword(model.Password);
            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.PasswordReset, true,
                "Password berhasil direset", ipAddress, userAgent);

            return RedirectToAction("ResetPasswordSuccess");
        }

        [HttpGet]
        [Route("Account/ResetPasswordSuccess")]
        public IActionResult ResetPasswordSuccess()
        {
            return View();
        }

        [HttpPost]
        [Route("Account/Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userEmail = User.Identity.Name ?? "unknown";
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await HttpContext.SignOutAsync("Cookies");

            await LogSecurityActionAsync(null, userEmail, SecurityAction.Logout, true,
                "Logout berhasil", ipAddress, userAgent);

            return RedirectToAction("Login");
        }

        [HttpGet]
        [Route("Account/AccessDenied")]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task SendVerificationEmailAsync(ApplicationUser user)
        {
            var token = GenerateSecureToken();
            var tokenEntity = new EmailVerificationToken
            {
                UserId = user.Id,
                Email = user.Email,
                TokenHash = HashToken(token),
                Purpose = TokenPurpose.EmailVerification,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                CreatedAt = DateTime.UtcNow
            };

            _context.EmailVerificationTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            var verificationLink = Url.Action("VerifyEmail", "Account",
                new { email = user.Email, token = token },
                protocol: Request.Scheme);

            await _emailService.SendVerificationEmailAsync(user.Email, verificationLink);

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.EmailVerificationSent, true,
                "Email verifikasi dikirim", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                HttpContext.Request.Headers["User-Agent"].ToString());
        }

        private async Task SendAccessRequestToAdminAsync(ApplicationUser user)
        {
            var token = GenerateSecureToken();
            var tokenEntity = new EmailVerificationToken
            {
                UserId = user.Id,
                Email = user.Email,
                TokenHash = HashToken(token),
                Purpose = TokenPurpose.ApprovalRequest,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                CreatedAt = DateTime.UtcNow
            };

            _context.EmailVerificationTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            var approvalLink = Url.Action("Approve", "AdminApproval",
                new { email = user.Email, token = token },
                protocol: Request.Scheme);

            var masterAdminEmail = await GetMasterAdminEmailAsync();
            if (!string.IsNullOrWhiteSpace(masterAdminEmail))
            {
                await _emailService.SendAccessRequestToAdminAsync(masterAdminEmail, user.Email, user.DisplayName, approvalLink);
            }

            await LogSecurityActionAsync(user.Id, user.Email, SecurityAction.AccessRequestSent, true,
                $"Request akses dikirim ke admin {masterAdminEmail}",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                HttpContext.Request.Headers["User-Agent"].ToString());
        }

        private async Task<string> GetMasterAdminEmailAsync()
        {
            var setting = await _context.AppSettingsRecords
                .Where(x => x.SettingKey == "Notification.MasterAdminEmail")
                .Select(x => x.SettingValue)
                .FirstOrDefaultAsync();

            return setting ?? string.Empty;
        }

        private async Task SendPasswordResetEmailAsync(ApplicationUser user)
        {
            var token = GenerateSecureToken();
            var tokenEntity = new EmailVerificationToken
            {
                UserId = user.Id,
                Email = user.Email,
                TokenHash = HashToken(token),
                Purpose = TokenPurpose.PasswordReset,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow
            };

            _context.EmailVerificationTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            var resetLink = Url.Action("ResetPassword", "Account",
                new { email = user.Email, token = token },
                protocol: Request.Scheme);

            await _emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
        }

        private async Task SignInUserAsync(ApplicationUser user, bool rememberMe)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddMinutes(30)
            });
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
            var regex = new Regex("[^a-z0-9.@_+-]");
            return regex.Replace(trimmed, "");
        }

        private static string GenerateSecureToken()
        {
            var bytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
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

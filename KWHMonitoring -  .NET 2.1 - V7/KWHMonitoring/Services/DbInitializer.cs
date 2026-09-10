using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public static class DbInitializer
    {
        // Minimum password complexity for seeded master admin
        private const int MinimumPasswordLength = 12;

        public static void SeedAdminUser(ApplicationDbContext context, IConfiguration configuration)
        {
            try
            {
                // Prefer environment variables, fall back to appsettings (useUserSecrets in development)
                var adminEmail = (configuration["Admin:Email"] ??
                                  configuration["KWH_ADMIN_EMAIL"] ??
                                  Environment.GetEnvironmentVariable("KWH_ADMIN_EMAIL"))?.Trim();

                var adminPassword = (configuration["Admin:Password"] ??
                                     configuration["KWH_ADMIN_PASSWORD"] ??
                                     Environment.GetEnvironmentVariable("KWH_ADMIN_PASSWORD"))?.Trim();

                // No configured admin -> skip seeding. A public hardcoded default admin is a security risk.
                if (string.IsNullOrWhiteSpace(adminEmail))
                {
                    System.Diagnostics.Debug.WriteLine("DbInitializer: Admin email is not configured. Skipping master admin seed.");
                    return;
                }

                if (!IsValidEmail(adminEmail))
                {
                    System.Diagnostics.Debug.WriteLine($"DbInitializer: Configured admin email '{adminEmail}' is not a valid email address. Skipping seed.");
                    return;
                }

                var normalizedAdminEmail = adminEmail.ToLowerInvariant();
                var existingUser = context.ApplicationUsers
                    .AsNoTracking()
                    .FirstOrDefault(x => x.NormalizedEmail == normalizedAdminEmail);

                if (existingUser == null)
                {
                    // Only create the admin if a password has been explicitly configured.
                    if (string.IsNullOrWhiteSpace(adminPassword))
                    {
                        System.Diagnostics.Debug.WriteLine("DbInitializer: Admin email is configured but no password is set. Skipping master admin creation. Set Admin:Password (or KWH_ADMIN_PASSWORD environment variable) to create the initial admin.");
                        return;
                    }

                    if (!IsStrongPassword(adminPassword))
                    {
                        System.Diagnostics.Debug.WriteLine($"DbInitializer: Configured admin password does not meet complexity requirements (min {MinimumPasswordLength} chars). Skipping seed.");
                        return;
                    }

                    var adminUser = new ApplicationUser
                    {
                        Email = adminEmail,
                        NormalizedEmail = normalizedAdminEmail,
                        DisplayName = "Master Administrator",
                        PasswordHash = PasswordHasher.HashPassword(adminPassword),
                        Role = UserRoles.Admin,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.ApplicationUsers.Add(adminUser);
                    context.SaveChanges();

                    System.Diagnostics.Debug.WriteLine("DbInitializer: Master admin user created successfully.");
                }
                else
                {
                    // IMPORTANT: Never reset the existing admin's password here.
                    // Only enforce role/activation flags so the admin can still log in.
                    existingUser.Role = UserRoles.Admin;
                    existingUser.EmailConfirmed = true;
                    existingUser.IsActive = true;
                    context.SaveChanges();

                    System.Diagnostics.Debug.WriteLine("DbInitializer: Existing master admin verified.");
                }
            }
            catch (Exception ex)
            {
                // Log or swallow to prevent blocking startup
                System.Diagnostics.Debug.WriteLine($"Failed to seed admin user: {ex.Message}");
            }
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsStrongPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return false;
            if (password.Length < MinimumPasswordLength) return false;
            return true;
        }
    }
}

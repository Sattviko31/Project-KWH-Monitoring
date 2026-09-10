using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    public static class DbInitializer
    {
        public static void SeedAdminUser(ApplicationDbContext context)
        {
            try
            {
                var adminEmail = "sattvikoramdhani@gmail.com";
                var normalizedAdminEmail = adminEmail.ToLowerInvariant();
                var existingUser = context.ApplicationUsers
                    .FirstOrDefault(x => x.NormalizedEmail == normalizedAdminEmail);

                if (existingUser == null)
                {
                    var adminUser = new ApplicationUser
                    {
                        Email = adminEmail,
                        NormalizedEmail = normalizedAdminEmail,
                        DisplayName = "Master Administrator",
                        PasswordHash = PasswordHasher.HashPassword("MasterAdmin2024!"),
                        Role = UserRoles.Admin,
                        EmailConfirmed = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.ApplicationUsers.Add(adminUser);
                }
                else
                {
                    // Ensure master admin has the expected password and role
                    existingUser.PasswordHash = PasswordHasher.HashPassword("MasterAdmin2024!");
                    existingUser.Role = UserRoles.Admin;
                    existingUser.EmailConfirmed = true;
                    existingUser.IsActive = true;
                }

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log or swallow to prevent blocking startup
                System.Diagnostics.Debug.WriteLine($"Failed to seed admin user: {ex.Message}");
            }
        }
    }
}

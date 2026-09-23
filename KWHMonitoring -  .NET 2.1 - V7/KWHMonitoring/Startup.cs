using System.Linq;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using KWHMonitoring.Models;
using KWHMonitoring.Services;
using KWHMonitoring.Filters;

namespace KWHMonitoring
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.Lax;
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"),
                    sql => sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: System.TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null)));

            services.AddMemoryCache();

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            services.AddScoped<NotificationService>();
            services.AddScoped<AesEncryptionService>();
            services.AddSingleton<MqttService>();
            services.AddScoped<IAnomalyAnalysisService, AnomalyAnalysisService>();
            services.AddSingleton<AppSettingsCache>();

            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IDeviceSettingsService, DeviceSettingsService>();

            services.AddAuthentication("Cookies")
                .AddCookie("Cookies", options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.LogoutPath = "/Account/Logout";
                    options.ExpireTimeSpan = System.TimeSpan.FromMinutes(30);
                    options.SlidingExpiration = true;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
                options.AddPolicy("RequireOperator", policy => policy.RequireRole("Operator", "Admin"));
                options.AddPolicy("RequireViewer", policy => policy.RequireRole("Viewer", "Operator", "Admin"));
            });

            // Optional: disable background notification service for testing/staging
            // Set environment variable KWH_DISABLE_NOTIFICATION_BG=1 to skip registration.
            var disableNotificationBg = Environment.GetEnvironmentVariable("KWH_DISABLE_NOTIFICATION_BG");
            if (string.IsNullOrEmpty(disableNotificationBg) || disableNotificationBg == "0" || disableNotificationBg.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                services.AddHostedService<AnomalyNotificationBackgroundService>();
            }

            // =========================================================
            // TAMBAHAN KHUSUS CHATBOT QWEN
            // Mendaftarkan HttpClientFactory agar efisien dan aman
            // =========================================================
            services.AddHttpClient("QwenClient");

            services.AddMvc(options =>
            {
                options.Filters.Add<DatabaseExceptionFilter>();
            }).SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            // Auto-create database and apply migrations on first run
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = loggerFactory.CreateLogger("DatabaseMigration");
                try
                {
                    var pending = context.Database.GetPendingMigrations().ToList();
                    if (pending.Count > 0)
                    {
                        logger.LogInformation("Applying {Count} pending migration(s)...", pending.Count);
                        context.Database.Migrate();
                        logger.LogInformation("Database migration completed successfully.");
                    }
                    else
                    {
                        logger.LogInformation("Database is up to date. No pending migrations.");
                    }
                }
                catch (System.Exception ex)
                {
                    logger.LogWarning(ex, "Migration failed. Attempting EnsureCreated as fallback...");
                    try
                    {
                        context.Database.EnsureCreated();
                        logger.LogInformation("Database ready via EnsureCreated.");
                    }
                    catch (System.Exception ex2)
                    {
                        logger.LogCritical(ex2, "Failed to initialize database. Application cannot start.");
                        throw;
                    }
                }
            }

            using (var seedScope = app.ApplicationServices.CreateScope())
            {
                try
                {
                    var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var seedConfig = seedScope.ServiceProvider.GetRequiredService<IConfiguration>();
                    DbInitializer.SeedAdminUser(seedContext, seedConfig);
                }
                catch (System.Exception ex)
                {
                    var seedLogger = loggerFactory.CreateLogger("DbInitializer");
                    seedLogger.LogWarning(ex, "Failed to seed admin user.");
                }
            }

            using (var deviceSettingsScope = app.ApplicationServices.CreateScope())
            {
                try
                {
                    var deviceSettingsContext = deviceSettingsScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    DbInitializer.SeedDeviceSettingsAsync(deviceSettingsContext).GetAwaiter().GetResult();
                }
                catch (System.Exception ex)
                {
                    var seedLogger = loggerFactory.CreateLogger("DbInitializer");
                    seedLogger.LogWarning(ex, "Failed to seed device settings.");
                }
            }

            // Warm up AppSettings cache (sync-over-async is safe here: no SynchronizationContext at startup)
            try
            {
                var settingsCache = app.ApplicationServices.GetRequiredService<AppSettingsCache>();
                settingsCache.WarmUpAsync().GetAwaiter().GetResult();
            }
            catch (System.Exception ex)
            {
                var cacheLogger = loggerFactory.CreateLogger("AppSettingsCache");
                cacheLogger.LogWarning(ex, "Failed to warm up AppSettingsCache. Will load on demand.");
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseResponseCompression();
            app.UseAuthentication();

            // Security headers middleware
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
                context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com; font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; img-src 'self' data: blob:; connect-src 'self'; frame-ancestors 'self'; base-uri 'self'; form-action 'self';";
                await next();
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Monitoring}/{action=Index}/{id?}");
            });
        }
    }
}

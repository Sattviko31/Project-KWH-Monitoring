using System.Linq;
using System;
using System.Data.SqlClient;
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

            // CATATAN PENTING (kasus "aplikasi stuck"):
            // EnableRetryOnFailure menambah 3 percobaan ulang per operasi. Bila database aplikasi
            // tidak terjangkau, total waktu tunggu = (Connect Timeout x 4 percobaan) + delay retry.
            // Karena Connect Timeout default SqlClient 15 detik, satu operasi bisa tertahan +/- 1 menit.
            // Supaya tetap terkendali: ConnectionStrings:DefaultConnection memakai
            // "Connect Timeout=5;ConnectRetryCount=0;" (ConnectRetryCount=0 juga direkomendasikan
            // Microsoft agar retry internal SqlClient tidak menumpuk dengan retry EF).

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

            // Data master titik lokasi dari database ERP (WWMERP2019.dbo.TitikLokasi)
            // untuk info lokasi pada tooltip header kartu panel monitoring.
            services.AddScoped<ITitikLokasiService, TitikLokasiService>();

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
            // Auto-create database and apply migrations on first run.
            //
            // Probe koneksi cepat lebih dulu: bila database aplikasi tidak terjangkau,
            // GetPendingMigrations()/Migrate()/EnsureCreated() masing-masing akan menunggu
            // (retry EF x Connect Timeout) sehingga aplikasi terasa "stuck" dan bahkan gagal
            // start. Dengan probe ini, database yang mati terdeteksi +/- 3 detik: inisialisasi
            // database dilewati dan aplikasi tetap dijalankan supaya halaman menampilkan pesan
            // error database (lihat DatabaseExceptionFilter), bukan proses yang menggantung.
            var databaseReady = CanReachDatabase(
                Configuration.GetConnectionString("DefaultConnection"),
                loggerFactory.CreateLogger("DatabaseMigration"));

            if (!databaseReady)
            {
                loggerFactory.CreateLogger("DatabaseMigration").LogCritical(
                    "Database aplikasi tidak dapat dihubungi saat startup " +
                    "(ConnectionStrings:DefaultConnection). Migrasi & pengisian data awal dilewati; " +
                    "restart aplikasi setelah database tersedia.");
            }
            else
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
                        // Dulu: throw; -> aplikasi tidak pernah listen sehingga browser seperti
                        // menggantung tanpa penjelasan. Sekarang cukup dicatat dan seeding dilewati.
                        databaseReady = false;
                        logger.LogCritical(ex2,
                            "Inisialisasi database gagal. Migrasi & pengisian data awal dilewati.");
                    }
                }
            }

            using (var seedScope = app.ApplicationServices.CreateScope())
            {
                try
                {
                    if (databaseReady)
                    {
                        var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var seedConfig = seedScope.ServiceProvider.GetRequiredService<IConfiguration>();
                        DbInitializer.SeedAdminUser(seedContext, seedConfig);
                    }
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
                    if (databaseReady)
                    {
                        var deviceSettingsContext = deviceSettingsScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        DbInitializer.SeedDeviceSettingsAsync(deviceSettingsContext).GetAwaiter().GetResult();
                    }
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
                if (databaseReady)
                {
                    var settingsCache = app.ApplicationServices.GetRequiredService<AppSettingsCache>();
                    settingsCache.WarmUpAsync().GetAwaiter().GetResult();
                }
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

        /// <summary>
        /// Probe koneksi cepat ke database aplikasi: satu kali Open() dengan Connect Timeout kecil
        /// (3 detik) dan tanpa retry. Dipakai supaya startup tidak tertahan lama (retry EF x
        /// Connect Timeout) ketika database mati/tidak terjangkau.
        /// Mengembalikan false bila connection string kosong atau koneksi gagal.
        /// </summary>
        private static bool CanReachDatabase(string connectionString, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                logger.LogWarning(
                    "Connection string database aplikasi (ConnectionStrings:DefaultConnection) kosong. " +
                    "Inisialisasi database dilewati.");
                return false;
            }

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 3,
                    ConnectRetryCount = 0
                };

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Probe koneksi database aplikasi gagal. Migrasi/pengisian data awal dilewati.");
                return false;
            }
        }
    }
}

using System.Linq;
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
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddMemoryCache();

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            services.AddScoped<NotificationService>();
            services.AddScoped<AesEncryptionService>();

            services.AddHostedService<EnergyAggregationBackgroundService>();
            services.AddHostedService<AnomalyNotificationBackgroundService>();

            // =========================================================
            // TAMBAHAN KHUSUS CHATBOT QWEN
            // Mendaftarkan HttpClientFactory agar efisien dan aman
            // =========================================================
            services.AddHttpClient("QwenClient");

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
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

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Monitoring}/{action=Index}/{id?}");
            });
        }
    }
}

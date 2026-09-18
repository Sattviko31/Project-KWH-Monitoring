using System;
using System.Threading.Tasks;
using KWHMonitoring.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KWHMonitoring.Controllers
{
    public class HealthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HealthController> _logger;

        public HealthController(ApplicationDbContext context, ILogger<HealthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Db()
        {
            try
            {
                await _context.AppSettingsRecords.AnyAsync();
                return Json(new { status = "Healthy", database = true, timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check: database unreachable");
                Response.StatusCode = 503;
                return Json(new { status = "Unhealthy", database = false, timestamp = DateTime.Now });
            }
        }
    }
}

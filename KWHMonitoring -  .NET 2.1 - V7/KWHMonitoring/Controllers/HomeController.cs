using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KWHMonitoring.Models;

namespace KWHMonitoring.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // [UNUSED] Privacy action - tidak ada link navigasi ke halaman ini
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

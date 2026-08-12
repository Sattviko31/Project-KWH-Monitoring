using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KWHMonitoring.Models;

// [UNUSED] - Razor Page model tidak digunakan karena project memakai MVC Controllers
namespace KWHMonitoring.Views.Monitoring
{
    public class _PanelCardModel : PageModel
    {
        [BindProperty]
        public PanelViewModel Panel { get; set; } = new PanelViewModel();

        public void OnGet()
        {
        }
    }
}

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using KWHMonitoring.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KWHMonitoring.Controllers
{
    [AllowAnonymous]
    public class BillingConfirmationController : Controller
    {
        private readonly IBillingTrendWorkflowService _workflow;

        public BillingConfirmationController(IBillingTrendWorkflowService workflow)
        {
            _workflow = workflow;
        }

        [HttpGet("BillingConfirmation")]
        public async Task<IActionResult> Index(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return View("Index", new BillingConfirmationPageModel { Error = "Link konfirmasi tidak lengkap." });

            var details = await _workflow.GetTokenViewAsync(token);
            if (!details.Valid)
                return View("Index", new BillingConfirmationPageModel { Error = details.Error });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userId, out _))
                return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });

            return View("Index", new BillingConfirmationPageModel { Token = token, Details = details });
        }

        [HttpPost("BillingConfirmation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(string token, string reason)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userId, out var authenticatedUserId))
                    return RedirectToAction("Login", "Account", new { returnUrl = Request.Path + Request.QueryString });

                await _workflow.ConfirmFromLinkAsync(token, reason, User.Identity.Name, authenticatedUserId);
                return View("Index", new BillingConfirmationPageModel { Success = "Anomaly berhasil dikonfirmasi. Link ini sudah tidak dapat digunakan kembali." });
            }
            catch (Exception ex)
            {
                return View("Index", new BillingConfirmationPageModel { Error = ex.Message });
            }
        }
    }

    public sealed class BillingConfirmationPageModel
    {
        public string Token { get; set; }
        public string Error { get; set; }
        public string Success { get; set; }
        public BillingConfirmationTokenView Details { get; set; }
    }
}
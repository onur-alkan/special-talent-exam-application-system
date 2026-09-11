using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using OzelYetenekSinavSistemi.Web.Models;

namespace OzelYetenekSinavSistemi.Web.Controllers;

public class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        var role = User.GetRole();
        return role switch
        {
            DomainConstants.RoleNames.Candidate => RedirectToAction("Index", "CandidateDashboard"),
            DomainConstants.RoleNames.SuperAdmin or DomainConstants.RoleNames.ApplicationManager
                => RedirectToAction("Index", "AdminDashboard"),
            _ => RedirectToAction("Login", "Account")
        };
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // Yalnızca güvenli TraceIdentifier; exception/stack/SQL gösterilmez.
        return View(new ErrorViewModel { RequestId = SanitizeRequestId(HttpContext.TraceIdentifier) });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        if (code == 404)
        {
            return View("NotFound");
        }

        return View("Error", new ErrorViewModel { RequestId = SanitizeRequestId(HttpContext.TraceIdentifier) });
    }

    private static string? SanitizeRequestId(string? traceIdentifier)
    {
        if (string.IsNullOrWhiteSpace(traceIdentifier))
            return null;

        var cleaned = traceIdentifier.Trim();
        if (cleaned.Length > 64)
            cleaned = cleaned[..64];

        return cleaned;
    }
}

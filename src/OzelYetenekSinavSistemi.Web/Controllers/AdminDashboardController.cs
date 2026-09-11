using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "AdminArea")]
public sealed class AdminDashboardController : Controller
{
    private readonly IExamPeriodService _examPeriodService;

    public AdminDashboardController(IExamPeriodService examPeriodService)
    {
        _examPeriodService = examPeriodService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _examPeriodService.GetVisibleForUserAsync(User.GetUserId(), User.GetRole(), cancellationToken);

        ViewBag.TotalExams = exams.Count;
        ViewBag.ActiveExams = exams.Count(e => e.IsActive && !e.IsClosed && !e.IsDeleted);
        ViewBag.ClosedExams = exams.Count(e => e.IsClosed);
        ViewBag.Exams = exams;

        ViewData["Title"] = "Yönetim Paneli";
        return View();
    }
}

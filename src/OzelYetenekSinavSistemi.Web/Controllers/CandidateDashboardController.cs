using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "CandidateOnly")]
public sealed class CandidateDashboardController : Controller
{
    private readonly ICandidateApplicationService _applicationService;

    public CandidateDashboardController(ICandidateApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var activeExams = await _applicationService.GetActiveExamPeriodsAsync(cancellationToken);
        var myApplications = await _applicationService.GetMyApplicationsAsync(User.GetUserId(), cancellationToken);

        ViewBag.ActiveExamCount = activeExams.Count;
        ViewBag.MyApplicationCount = myApplications.Count;
        ViewBag.ActiveExams = activeExams;
        ViewBag.MyApplications = myApplications;

        ViewData["Title"] = "Ana Sayfa";
        return View();
    }
}

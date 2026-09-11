using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "AdminArea")]
public sealed class CandidateManagementController : Controller
{
    private readonly IExamPeriodService _examPeriodService;
    private readonly IExamResultService _examResultService;

    public CandidateManagementController(IExamPeriodService examPeriodService, IExamResultService examResultService)
    {
        _examPeriodService = examPeriodService;
        _examResultService = examResultService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _examPeriodService.GetVisibleForUserAsync(User.GetUserId(), User.GetRole(), cancellationToken);
        ViewData["Title"] = "Aday Başvuruları";
        return View(exams);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid examPeriodId, CancellationToken cancellationToken)
    {
        var examResult = await _examPeriodService.GetByIdForUserAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
        {
            TempData["ToastError"] = examResult.ErrorMessage ?? "Sınav dönemine erişiminiz yok.";
            return RedirectToAction(nameof(Index));
        }

        var candidates = await _examResultService.GetCandidatesForExamAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!candidates.Success || candidates.Data is null)
        {
            TempData["ToastError"] = candidates.ErrorMessage ?? "Aday listesi getirilemedi.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.ExamPeriod = examResult.Data;
        ViewData["Title"] = "Aday Başvuruları";
        return View(candidates.Data);
    }
}

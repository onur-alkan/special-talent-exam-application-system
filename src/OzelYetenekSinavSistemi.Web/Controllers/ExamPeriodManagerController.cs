using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public sealed class ExamPeriodManagerController : Controller
{
    private readonly IExamPeriodService _examPeriodService;

    public ExamPeriodManagerController(IExamPeriodService examPeriodService)
    {
        _examPeriodService = examPeriodService;
    }

    private string? Ip => HttpContext.GetClientIpString();

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _examPeriodService.GetAllAsync(cancellationToken);
        ViewData["Title"] = "Başvuru Yöneticileri";
        return View(exams);
    }

    [HttpGet]
    public async Task<IActionResult> Manage(Guid examPeriodId, CancellationToken cancellationToken)
    {
        var examResult = await _examPeriodService.GetByIdForUserAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
        {
            TempData["ToastError"] = examResult.ErrorMessage ?? "Sınav dönemi bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.ExamPeriod = examResult.Data;
        ViewBag.AssignedManagers = await _examPeriodService.GetManagersAsync(examPeriodId, cancellationToken);
        ViewBag.AssignableManagers = await _examPeriodService.GetAssignableManagersAsync(cancellationToken);
        ViewData["Title"] = "Başvuru Yöneticileri";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Assign(Guid examPeriodId, Guid managerUserId, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.AssignManagerAsync(
            examPeriodId, managerUserId, User.GetUserId(), Ip, HttpContext.TraceIdentifier, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Yönetici atandı." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Manage), new { examPeriodId });
    }

    [HttpPost]
    public async Task<IActionResult> Remove(Guid examPeriodId, Guid managerUserId, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.RemoveManagerAsync(
            examPeriodId, managerUserId, User.GetUserId(), Ip, HttpContext.TraceIdentifier, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Yönetici ataması kaldırıldı." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Manage), new { examPeriodId });
    }
}

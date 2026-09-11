using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "AdminArea")]
public sealed class ExamPeriodController : Controller
{
    private readonly IExamPeriodService _examPeriodService;

    public ExamPeriodController(IExamPeriodService examPeriodService)
    {
        _examPeriodService = examPeriodService;
    }

    private string? Ip => HttpContext.GetClientIpString();

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _examPeriodService.GetVisibleForUserAsync(User.GetUserId(), User.GetRole(), cancellationToken);
        ViewData["Title"] = "Sınav Dönemleri";
        return View(exams);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.GetByIdForUserAsync(id, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Sınav dönemi bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Options = await _examPeriodService.GetPreferenceOptionsAsync(id, cancellationToken);
        ViewData["Title"] = "Sınav Dönemi Detayı";
        return View(result.Data);
    }

    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    public IActionResult Create()
    {
        PrepareCreateViewData();
        return View(new ExamPeriodFormViewModel());
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Create(ExamPeriodFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PrepareCreateViewData();
            return View(model);
        }

        var result = await _examPeriodService.CreateAsync(model, User.GetUserId(), Ip, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Sınav dönemi oluşturulamadı.");
            PrepareCreateViewData();
            return View(model);
        }

        TempData["ToastSuccess"] = "Sınav dönemi oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.GetByIdForUserAsync(id, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Sınav dönemi bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var period = result.Data;
        var model = new ExamPeriodFormViewModel
        {
            Id = period.Id,
            Title = period.Title,
            Description = period.Description,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            MaxPreferences = period.MaxPreferences,
            IsActive = period.IsActive
        };

        PrepareEditViewData();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ExamPeriodFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PrepareEditViewData();
            return View(model);
        }

        var result = await _examPeriodService.UpdateAsync(model, User.GetUserId(), User.GetRole(), Ip, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Sınav dönemi güncellenemedi.");
            PrepareEditViewData();
            return View(model);
        }

        TempData["ToastSuccess"] = "Sınav dönemi güncellendi.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.SetActiveAsync(id, isActive, User.GetUserId(), Ip, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Sınav dönemi durumu güncellendi." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.CloseAsync(id, User.GetUserId(), Ip, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Sınav dönemi kapatıldı." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _examPeriodService.SoftDeleteAsync(id, User.GetUserId(), Ip, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Sınav dönemi silindi." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Index));
    }

    private void PrepareCreateViewData()
    {
        ViewData["Title"] = "Sınav Dönemi Ekle";
    }

    private void PrepareEditViewData()
    {
        ViewData["Title"] = "Sınav Dönemini Düzenle";
    }
}

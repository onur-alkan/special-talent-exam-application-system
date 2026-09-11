using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public sealed class ExamPreferenceOptionController : Controller
{
    private readonly IExamPeriodService _examPeriodService;

    public ExamPreferenceOptionController(IExamPeriodService examPeriodService)
    {
        _examPeriodService = examPeriodService;
    }

    private string? Ip => HttpContext.GetClientIpString();
    private string CorrelationId => HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _examPeriodService.GetVisibleForUserAsync(User.GetUserId(), User.GetRole(), cancellationToken);
        ViewData["Title"] = "Tercih Seçenekleri";
        return View(exams);
    }

    [HttpGet]
    public async Task<IActionResult> Manage(Guid examPeriodId, CancellationToken cancellationToken)
    {
        var pageModel = await BuildManagePageAsync(examPeriodId, cancellationToken);
        if (pageModel is null)
        {
            TempData["ToastError"] = "Sınav dönemi bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        SetManageViewData(pageModel);
        return View(pageModel);
    }

    /// <summary>DataTables server-side liste endpoint'i (salt okunur).</summary>
    [HttpGet]
    public async Task<IActionResult> Data(Guid examPeriodId, CancellationToken cancellationToken)
    {
        var examResult = await _examPeriodService.GetByIdForUserAsync(
            examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
        {
            return Json(new PreferenceOptionDataTablesResponse
            {
                Draw = ParseNonNegativeInt(Request.Query["draw"]),
                Error = "Sınav dönemi bulunamadı veya erişim yetkiniz yok."
            });
        }

        var query = new PreferenceOptionDataTablesQuery
        {
            ExamPeriodId = examPeriodId,
            Draw = ParseNonNegativeInt(Request.Query["draw"]),
            Start = ParseNonNegativeInt(Request.Query["start"]),
            Length = ParseNonNegativeInt(Request.Query["length"], fallback: 10),
            SearchValue = Request.Query["search[value]"].ToString(),
            OrderColumnIndex = ParseNonNegativeInt(Request.Query["order[0][column]"]),
            OrderDirection = Request.Query["order[0][dir]"].ToString()
        };

        var result = await _examPeriodService.SearchPreferenceOptionsForDataTablesAsync(query, cancellationToken);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(PreferenceOptionFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var invalidPage = await BuildManagePageAsync(model.ExamPeriodId, cancellationToken, model);
            if (invalidPage is null)
            {
                TempData["ToastError"] = "Sınav dönemi bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            SetManageViewData(invalidPage);
            return View(nameof(Manage), invalidPage);
        }

        var result = await _examPeriodService.AddPreferenceOptionAsync(
            model, User.GetUserId(), Ip, CorrelationId, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "İşlem başarısız.");
            var failPage = await BuildManagePageAsync(model.ExamPeriodId, cancellationToken, model);
            if (failPage is null)
            {
                TempData["ToastError"] = result.ErrorMessage ?? "İşlem başarısız.";
                return RedirectToAction(nameof(Index));
            }

            SetManageViewData(failPage);
            return View(nameof(Manage), failPage);
        }

        TempData["ToastSuccess"] = "Tercih seçeneği eklendi.";
        return RedirectToAction(nameof(Manage), new { examPeriodId = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var option = await _examPeriodService.GetPreferenceOptionAsync(id, cancellationToken);
        if (option is null)
        {
            TempData["ToastError"] = "Tercih seçeneği bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var examResult = await _examPeriodService.GetByIdForUserAsync(
            option.ExamPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
        {
            TempData["ToastError"] = examResult.ErrorMessage ?? "Sınav dönemi bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Tercih Seçeneğini Düzenle";
        ViewData["BreadcrumbParentText"] = "Tercih Seçenekleri";
        ViewData["BreadcrumbParentUrl"] = Url.Action(nameof(Index));
        ViewData["Breadcrumb"] = examResult.Data.Title;

        return View(ToFormViewModel(option));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PreferenceOptionFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Id is null)
        {
            TempData["ToastError"] = "Tercih seçeneği bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var option = await _examPeriodService.GetPreferenceOptionAsync(model.Id.Value, cancellationToken);
        if (option is null)
        {
            TempData["ToastError"] = "Tercih seçeneği bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        // Sınav dönemi optionId üzerinden çözülür; posted ExamPeriodId mutasyon için güvenilmez.
        model.ExamPeriodId = option.ExamPeriodId;
        if (!ModelState.IsValid)
        {
            var examResult = await _examPeriodService.GetByIdForUserAsync(
                option.ExamPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
            ViewData["Title"] = "Tercih Seçeneğini Düzenle";
            ViewData["BreadcrumbParentText"] = "Tercih Seçenekleri";
            ViewData["BreadcrumbParentUrl"] = Url.Action(nameof(Index));
            ViewData["Breadcrumb"] = examResult.Data?.Title ?? "Tercih";
            return View(model);
        }

        var result = await _examPeriodService.UpdatePreferenceOptionAsync(
            model, User.GetUserId(), Ip, CorrelationId, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Tercih seçeneği güncellendi." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Manage), new
        {
            examPeriodId = result.Success ? result.Data : option.ExamPeriodId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid optionId, bool isActive, CancellationToken cancellationToken)
    {
        var option = await _examPeriodService.GetPreferenceOptionAsync(optionId, cancellationToken);
        if (option is null)
        {
            TempData["ToastError"] = "Tercih seçeneği bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _examPeriodService.SetPreferenceOptionActiveAsync(
            optionId, isActive, User.GetUserId(), Ip, CorrelationId, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success
                ? (isActive ? "Tercih seçeneği aktif yapıldı." : "Tercih seçeneği pasif yapıldı.")
                : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Manage), new
        {
            examPeriodId = result.Success ? result.Data : option.ExamPeriodId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid optionId, CancellationToken cancellationToken)
    {
        var option = await _examPeriodService.GetPreferenceOptionAsync(optionId, cancellationToken);
        if (option is null)
        {
            TempData["ToastError"] = "Tercih seçeneği bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _examPeriodService.DeletePreferenceOptionAsync(
            optionId, User.GetUserId(), Ip, CorrelationId, cancellationToken);
        TempData[result.Success ? "ToastSuccess" : "ToastError"] =
            result.Success ? "Tercih seçeneği silindi." : result.ErrorMessage ?? "İşlem başarısız.";
        return RedirectToAction(nameof(Manage), new
        {
            examPeriodId = result.Success ? result.Data : option.ExamPeriodId
        });
    }

    private async Task<PreferenceOptionManagePageViewModel?> BuildManagePageAsync(
        Guid examPeriodId,
        CancellationToken cancellationToken,
        PreferenceOptionFormViewModel? addFormOverride = null)
    {
        var examResult = await _examPeriodService.GetByIdForUserAsync(
            examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
            return null;

        // Satırlar DataTables AJAX ile gelir; burada yalnız sonraki sıra numarası hesaplanır.
        var nextOrder = await _examPeriodService.GetNextPreferenceDisplayOrderAsync(examPeriodId, cancellationToken);

        var addForm = addFormOverride ?? new PreferenceOptionFormViewModel
        {
            ExamPeriodId = examPeriodId,
            DisplayOrder = nextOrder,
            IsActive = true
        };
        addForm.ExamPeriodId = examPeriodId;

        return new PreferenceOptionManagePageViewModel
        {
            ExamPeriodId = examPeriodId,
            ExamPeriodTitle = examResult.Data.Title,
            ExamPeriod = examResult.Data,
            ExistingOptions = Array.Empty<PreferenceOptionListItemViewModel>(),
            AddForm = addForm
        };
    }

    private void SetManageViewData(PreferenceOptionManagePageViewModel pageModel)
    {
        ViewData["Title"] = "Tercih Seçenekleri";
        ViewData["BreadcrumbParentText"] = "Tercih Seçenekleri";
        ViewData["BreadcrumbParentUrl"] = Url.Action(nameof(Index));
        ViewData["Breadcrumb"] = pageModel.ExamPeriodTitle;
    }

    private static PreferenceOptionFormViewModel ToFormViewModel(ExamPreferenceOption option) => new()
    {
        Id = option.Id,
        ExamPeriodId = option.ExamPeriodId,
        PreferenceName = option.PreferenceName,
        DisplayOrder = option.DisplayOrder,
        IsActive = option.IsActive
    };

    private static int ParseNonNegativeInt(string? raw, int fallback = 0)
    {
        if (!int.TryParse(raw, out var value))
            return fallback;
        return value < 0 ? fallback : value;
    }
}

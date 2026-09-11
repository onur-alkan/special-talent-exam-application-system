using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Applications;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "CandidateOnly")]
public sealed class CandidateApplicationController : Controller
{
    private readonly ICandidateApplicationService _applicationService;

    public CandidateApplicationController(ICandidateApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    private string? Ip => HttpContext.GetClientIpString();
    private string CorrelationId => HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _applicationService.GetActiveExamPeriodsAsync(cancellationToken);
        ViewData["Title"] = "Aktif Başvurular";
        return View(exams);
    }

    [HttpGet]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        var applications = await _applicationService.GetMyApplicationsAsync(User.GetUserId(), cancellationToken);
        ViewData["Title"] = "Başvurularım";
        return View(applications);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetMyApplicationDetailAsync(id, User.GetUserId(), cancellationToken);
        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Başvuru bulunamadı.";
            return RedirectToAction(nameof(My));
        }

        ViewData["Title"] = "Başvuru Detayı";
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Apply(Guid id, CancellationToken cancellationToken)
    {
        var formResult = await _applicationService.GetApplicationFormAsync(id, User.GetUserId(), cancellationToken);
        if (!formResult.Success || formResult.Data is null)
        {
            TempData["ToastError"] = formResult.ErrorMessage ?? "Başvuru formu açılamadı.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = formResult.Data.AlreadyApplied ? "Başvuru Güncelle" : "Başvuru Oluştur";
        return View(formResult.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Apply(CreateApplicationViewModel model, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (!ModelState.IsValid)
        {
            return await RedisplayApplyFormAsync(
                model,
                "En az bir tercih seçiniz.",
                cancellationToken);
        }

        var result = await _applicationService.ApplyAsync(userId, model, Ip, CorrelationId, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return await RedisplayApplyFormAsync(
                model,
                result.ErrorMessage ?? "Başvuru oluşturulamadı.",
                cancellationToken);
        }

        TempData["ToastSuccess"] = "Tercihleriniz başarıyla kaydedildi.";
        return RedirectToAction(nameof(Detail), new { id = result.Data.ApplicationId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var detail = await _applicationService.GetMyApplicationDetailAsync(id, userId, cancellationToken);
        if (!detail.Success || detail.Data is null)
        {
            TempData["ToastError"] = detail.ErrorMessage ?? "Başvuru bulunamadı.";
            return RedirectToAction(nameof(My));
        }

        var formResult = await _applicationService.GetApplicationFormAsync(detail.Data.ExamPeriodId, userId, cancellationToken);
        if (!formResult.Success || formResult.Data is null)
        {
            TempData["ToastError"] = formResult.ErrorMessage ?? "Başvuru formu açılamadı.";
            return RedirectToAction(nameof(My));
        }

        ViewData["Title"] = "Başvuru Güncelle";
        return View("Apply", formResult.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, CreateApplicationViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedisplayEditFormAsync(
                id,
                model,
                "En az bir tercih seçiniz.",
                cancellationToken);
        }

        var result = await _applicationService.UpdateApplicationAsync(User.GetUserId(), id, model, Ip, cancellationToken);
        if (!result.Success)
        {
            return await RedisplayEditFormAsync(
                id,
                model,
                result.ErrorMessage ?? "Başvuru güncellenemedi.",
                cancellationToken);
        }

        TempData["ToastSuccess"] = "Tercihleriniz başarıyla güncellendi.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<IActionResult> RedisplayApplyFormAsync(
        CreateApplicationViewModel model,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        TempData["ToastError"] = errorMessage;
        var form = await BuildFormWithPostedSelectionsAsync(model.ExamPeriodId, model.SelectedPreferenceOptionIds, cancellationToken);
        if (form is null)
            return RedirectToAction(nameof(Apply), new { id = model.ExamPeriodId });

        ViewData["Title"] = form.AlreadyApplied ? "Başvuru Güncelle" : "Başvuru Oluştur";
        return View("Apply", form);
    }

    private async Task<IActionResult> RedisplayEditFormAsync(
        Guid applicationId,
        CreateApplicationViewModel model,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        TempData["ToastError"] = errorMessage;
        var examPeriodId = model.ExamPeriodId;
        if (examPeriodId == Guid.Empty)
        {
            var detail = await _applicationService.GetMyApplicationDetailAsync(applicationId, User.GetUserId(), cancellationToken);
            if (!detail.Success || detail.Data is null)
                return RedirectToAction(nameof(Edit), new { id = applicationId });
            examPeriodId = detail.Data.ExamPeriodId;
        }

        var form = await BuildFormWithPostedSelectionsAsync(examPeriodId, model.SelectedPreferenceOptionIds, cancellationToken);
        if (form is null)
            return RedirectToAction(nameof(Edit), new { id = applicationId });

        ViewData["Title"] = "Başvuru Güncelle";
        return View("Apply", form);
    }

    private async Task<ApplicationFormData?> BuildFormWithPostedSelectionsAsync(
        Guid examPeriodId,
        IReadOnlyList<Guid>? postedSelections,
        CancellationToken cancellationToken)
    {
        var formResult = await _applicationService.GetApplicationFormAsync(examPeriodId, User.GetUserId(), cancellationToken);
        if (!formResult.Success || formResult.Data is null)
            return null;

        var source = formResult.Data;
        return new ApplicationFormData
        {
            ExamPeriod = source.ExamPeriod,
            Options = source.Options,
            ExistingApplicationId = source.ExistingApplicationId,
            ExistingSelectedOptionIds = postedSelections?.Where(id => id != Guid.Empty).ToList()
                                        ?? source.ExistingSelectedOptionIds
        };
    }
}

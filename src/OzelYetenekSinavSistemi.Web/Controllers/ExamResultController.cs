using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "AdminArea")]
public sealed class ExamResultController : Controller
{
    private readonly IExamPeriodService _examPeriodService;
    private readonly IExamResultService _examResultService;
    private readonly IExcelExportService _excelExportService;
    private readonly IPdfExportService _pdfExportService;
    private readonly IAuditService _auditService;
    private readonly ICountryCatalog _countryCatalog;
    private readonly ISensitiveDataMaskingService _masking;

    public ExamResultController(
        IExamPeriodService examPeriodService,
        IExamResultService examResultService,
        IExcelExportService excelExportService,
        IPdfExportService pdfExportService,
        IAuditService auditService,
        ICountryCatalog countryCatalog,
        ISensitiveDataMaskingService masking)
    {
        _examPeriodService = examPeriodService;
        _examResultService = examResultService;
        _excelExportService = excelExportService;
        _pdfExportService = pdfExportService;
        _auditService = auditService;
        _countryCatalog = countryCatalog;
        _masking = masking;
    }

    private string? Ip => HttpContext.GetClientIpString();
    private string CorrelationId => HttpContext.TraceIdentifier;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _examPeriodService.GetVisibleForUserAsync(User.GetUserId(), User.GetRole(), cancellationToken);
        ViewData["Title"] = "Sınav Sonuçları";
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
        ViewData["Title"] = "Sınav Sonuçları";
        return View(candidates.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _examResultService.GetCandidateForEvaluationAsync(id, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Aday bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        var candidate = result.Data;
        var model = new ExamResultFormViewModel
        {
            ApplicationId = candidate.ApplicationId,
            AttendanceStatus = candidate.AttendanceStatus ?? Domain.Enums.AttendanceStatus.Attended,
            ExamScore = candidate.ExamScore,
            AdminDescription = candidate.AdminDescription,
            IsDescriptionVisibleToCandidate = candidate.IsDescriptionVisibleToCandidate,
            ExpectedUpdatedDate = candidate.EvaluatedDate
        };

        PrepareEvaluateViewData(candidate);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Save(ExamResultFormViewModel model, Guid examPeriodId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var reload = await _examResultService.GetCandidateForEvaluationAsync(model.ApplicationId, User.GetUserId(), User.GetRole(), cancellationToken);
            PrepareEvaluateViewData(reload.Data);
            return View(nameof(Evaluate), model);
        }

        var result = await _examResultService.SaveResultAsync(
            model, User.GetUserId(), User.GetRole(), Ip, CorrelationId, cancellationToken);

        if (!result.Success)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Sonuç kaydedilemedi.";
            // Eşzamanlılık çatışmasında güncel ekrana yönlendir; üzerine yazma.
            return RedirectToAction(nameof(Evaluate), new { id = model.ApplicationId });
        }

        TempData["ToastSuccess"] = "Sonuç kaydedildi.";

        var detail = await _examResultService.GetCandidateForEvaluationAsync(
            model.ApplicationId, User.GetUserId(), User.GetRole(), cancellationToken);
        var redirectExamPeriodId = detail.Data?.ExamPeriodId
            ?? (examPeriodId != Guid.Empty ? examPeriodId : Guid.Empty);

        if (redirectExamPeriodId != Guid.Empty)
            return RedirectToAction(nameof(List), new { examPeriodId = redirectExamPeriodId });

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(Guid examPeriodId, CancellationToken cancellationToken)
    {
        var examResult = await _examPeriodService.GetByIdForUserAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
            return Forbid();

        var candidates = await _examResultService.GetCandidatesForExamAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!candidates.Success || candidates.Data is null)
            return Forbid();

        var bytes = _excelExportService.ExportCandidateResults(examResult.Data.Title, candidates.Data);
        ApplySensitiveExportHeaders();

        try
        {
            await _auditService.LogAsync(
                "ExamResultsExportedExcel",
                $"ExamPeriodId={examPeriodId}",
                User.GetUserId(),
                Ip,
                null,
                CorrelationId,
                cancellationToken);
        }
        catch
        {
            // Audit hatası dosya içeriğini veya SQL metnini sızdırmamalı.
        }

        var fileName = $"sinav-sonuclari-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(Guid examPeriodId, CancellationToken cancellationToken)
    {
        var examResult = await _examPeriodService.GetByIdForUserAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!examResult.Success || examResult.Data is null)
            return Forbid();

        var candidates = await _examResultService.GetCandidatesForExamAsync(examPeriodId, User.GetUserId(), User.GetRole(), cancellationToken);
        if (!candidates.Success || candidates.Data is null)
            return Forbid();

        var bytes = _pdfExportService.ExportCandidateResults(examResult.Data.Title, candidates.Data);
        ApplySensitiveExportHeaders();

        try
        {
            await _auditService.LogAsync(
                "ExamResultsExportedPdf",
                $"ExamPeriodId={examPeriodId}",
                User.GetUserId(),
                Ip,
                null,
                CorrelationId,
                cancellationToken);
        }
        catch
        {
            // Audit hatası dosya içeriğini veya SQL metnini sızdırmamalı.
        }

        var fileName = $"sinav-sonuclari-{DateTime.Now:yyyyMMdd-HHmm}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private void ApplySensitiveExportHeaders()
    {
        Response.Headers.CacheControl = "private, no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    private void PrepareEvaluateViewData(CandidateApplicationDetail? candidate)
    {
        ViewBag.Candidate = candidate;
        ViewBag.CandidateIdentity = candidate is null
            ? null
            : IdentityDisplayBuilder.BuildFromApplicationDetail(candidate, _countryCatalog, maskIdentity: true, _masking);
        ViewData["Title"] = "Değerlendirme";
    }
}

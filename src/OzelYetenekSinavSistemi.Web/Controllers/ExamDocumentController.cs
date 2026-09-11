using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize]
public sealed class ExamDocumentController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly IPublicUrlBuilder _publicUrlBuilder;

    public ExamDocumentController(IDocumentService documentService, IPublicUrlBuilder publicUrlBuilder)
    {
        _documentService = documentService;
        _publicUrlBuilder = publicUrlBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> View(Guid id, CancellationToken cancellationToken)
    {
        var verificationBaseUrl = _publicUrlBuilder.BuildDocumentVerificationBaseUrl();

        var result = await _documentService.GetEntranceDocumentAsync(
            id, User.GetUserId(), User.GetRole(), verificationBaseUrl, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Sınava giriş belgesine erişilemedi.";
            return RedirectToAction("My", "CandidateApplication");
        }

        ViewData["Title"] = "Sınava Giriş Belgesi";
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Result(Guid id, CancellationToken cancellationToken)
    {
        var verificationBaseUrl = _publicUrlBuilder.BuildDocumentVerificationBaseUrl();

        var result = await _documentService.GetResultDocumentAsync(
            id, User.GetUserId(), User.GetRole(), verificationBaseUrl, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Sınav sonuç belgesine erişilemedi.";
            return RedirectToAction("My", "CandidateApplication");
        }

        ViewData["Title"] = "Sınav Sonuç Belgesi";
        return View(result.Data);
    }
}

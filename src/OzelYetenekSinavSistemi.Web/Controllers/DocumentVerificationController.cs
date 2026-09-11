using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[AllowAnonymous]
public sealed class DocumentVerificationController : Controller
{
    private readonly IDocumentVerificationService _verificationService;

    public DocumentVerificationController(IDocumentVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    [HttpGet]
    [EnableRateLimiting("document-verification")]
    public async Task<IActionResult> Verify(string? code, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        var result = await _verificationService.VerifyAsync(code, cancellationToken);
        ViewData["Title"] = "Belge Doğrulama";
        return View(result);
    }
}

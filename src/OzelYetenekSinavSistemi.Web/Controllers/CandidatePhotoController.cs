using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize]
public sealed class CandidatePhotoController : Controller
{
    private readonly ICandidatePhotoService _candidatePhotoService;

    public CandidatePhotoController(ICandidatePhotoService candidatePhotoService)
    {
        _candidatePhotoService = candidatePhotoService;
    }

    [HttpGet]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var result = await _candidatePhotoService
            .GetCurrentUserPhotoAsync(User.GetUserId(), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success || result.Data is null)
            return NotFound();

        ApplySecurePhotoHeaders(result.Data.FileName);
        return File(result.Data.Content, result.Data.ContentType);
    }

    [HttpGet]
    public async Task<IActionResult> Application(Guid applicationId, CancellationToken cancellationToken)
    {
        var result = await _candidatePhotoService
            .GetApplicationPhotoAsync(applicationId, User.GetUserId(), User.GetRole(), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success || result.Data is null)
            return NotFound();

        ApplySecurePhotoHeaders(result.Data.FileName);
        return File(result.Data.Content, result.Data.ContentType);
    }

    private void ApplySecurePhotoHeaders(string fileName)
    {
        Response.Headers[HeaderNames.CacheControl] = "private, no-store, no-cache, must-revalidate";
        Response.Headers[HeaderNames.Pragma] = "no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = fileName
        }.ToString();
    }
}

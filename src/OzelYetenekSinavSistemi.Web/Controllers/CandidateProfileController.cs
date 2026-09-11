using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Web.Controllers;

[Authorize(Policy = "CandidateOnly")]
public sealed class CandidateProfileController : Controller
{
    private const string PhotoFieldKey = "Photo";

    private readonly IUserService _userService;
    private readonly IYgsYearRepository _ygsYearRepository;
    private readonly IPhotoUploadService _photoUploadService;

    public CandidateProfileController(
        IUserService userService,
        IYgsYearRepository ygsYearRepository,
        IPhotoUploadService photoUploadService)
    {
        _userService = userService;
        _ygsYearRepository = ygsYearRepository;
        _photoUploadService = photoUploadService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _userService.GetProfileAsync(User.GetUserId(), cancellationToken);
        if (!result.Success || result.Data is null)
        {
            TempData["ToastError"] = result.ErrorMessage ?? "Profil bilgileri getirilemedi.";
            return RedirectToAction("Index", "CandidateDashboard");
        }

        await PrepareIndexViewDataAsync(cancellationToken);
        return View(result.Data);
    }

    [HttpPost]
    [RequestSizeLimit(PhotoUploadLimits.MaxMultipartRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = PhotoUploadLimits.MaxMultipartRequestBytes,
        ValueLengthLimit = (int)PhotoUploadLimits.MaxMultipartRequestBytes,
        MemoryBufferThreshold = PhotoUploadLimits.MemoryBufferThresholdBytes)]
    public async Task<IActionResult> Index(
        ProfileUpdateViewModel model,
        IFormFile? Photo,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        DisabilityFormBinding.Normalize(
            model.HasDisability,
            value => model.DisabilityDetails = value,
            ModelState);

        var hasUploadedPhoto = HasUploadedPhoto(Photo);
        if (hasUploadedPhoto)
        {
            ClearPhotoFieldErrors(ModelState, PhotoFieldKey);
            ClearPhotoFieldErrors(ModelState, "photo");
            await ApplyPhotoValidationAsync(Photo!, cancellationToken);
        }

        if (!ModelState.IsValid)
        {
            return await ReturnProfileWithPostedEditsAsync(userId, model, cancellationToken);
        }

        PhotoUploadRequest? photoRequest = null;
        OperationResult result;
        try
        {
            if (hasUploadedPhoto)
                photoRequest = PhotoUploadFormMapper.ToRequest(Photo!);

            result = await _userService.UpdateProfileAsync(userId, model, photoRequest, cancellationToken);
        }
        finally
        {
            photoRequest?.Content.Dispose();
        }

        if (!result.Success)
        {
            if (PhotoUploadMessages.IsPhotoError(result.ErrorMessage))
                ModelState.AddModelError(PhotoFieldKey, result.ErrorMessage!);
            else
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Profil güncellenemedi.");

            return await ReturnProfileWithPostedEditsAsync(userId, model, cancellationToken);
        }

        TempData["ToastSuccess"] = hasUploadedPhoto
            ? PhotoUploadMessages.ProfileUpdated
            : PhotoUploadMessages.ProfileUpdatedGeneral;
        return RedirectToAction(nameof(Index));
    }

    private async Task ApplyPhotoValidationAsync(IFormFile photo, CancellationToken cancellationToken)
    {
        PhotoUploadRequest? request = null;
        try
        {
            request = PhotoUploadFormMapper.ToRequest(photo);
            var validation = await _photoUploadService.ValidatePhotoAsync(request, cancellationToken);
            if (!validation.Success)
            {
                AddModelErrorIfMissing(
                    PhotoFieldKey,
                    validation.UserMessage ?? PhotoUploadMessages.GenericFailure);
            }
        }
        catch
        {
            AddModelErrorIfMissing(PhotoFieldKey, PhotoUploadMessages.GenericFailure);
        }
        finally
        {
            request?.Content.Dispose();
        }
    }

    private void AddModelErrorIfMissing(string key, string message)
    {
        if (ModelState.TryGetValue(key, out var entry)
            && entry.Errors.Any(e => string.Equals(e.ErrorMessage, message, StringComparison.Ordinal)))
        {
            return;
        }

        ModelState.AddModelError(key, message);
    }

    private static bool HasUploadedPhoto(IFormFile? photo)
        => photo is not null
           && photo.Length > 0
           && !string.IsNullOrWhiteSpace(photo.FileName);

    private static void ClearPhotoFieldErrors(ModelStateDictionary modelState, string photoFieldKey)
    {
        if (!modelState.TryGetValue(photoFieldKey, out var entry))
            return;

        entry.Errors.Clear();
        entry.ValidationState = ModelValidationState.Valid;
    }

    private async Task<IActionResult> ReturnProfileWithPostedEditsAsync(
        Guid userId,
        ProfileUpdateViewModel posted,
        CancellationToken cancellationToken)
    {
        var profileResult = await _userService.GetProfileAsync(userId, cancellationToken);
        if (!profileResult.Success || profileResult.Data is null)
        {
            TempData["ToastError"] = profileResult.ErrorMessage ?? "Profil bilgileri getirilemedi.";
            return RedirectToAction("Index", "CandidateDashboard");
        }

        profileResult.Data.ApplyEditableFields(posted);
        await PrepareIndexViewDataAsync(cancellationToken);
        return View(profileResult.Data);
    }

    private async Task PrepareIndexViewDataAsync(CancellationToken cancellationToken)
    {
        ViewBag.YgsYears = await _ygsYearRepository.GetActiveAsync(cancellationToken);
        ViewData["Title"] = "Profilim";
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using IAuthenticationService = OzelYetenekSinavSistemi.Application.Interfaces.Services.IAuthenticationService;

namespace OzelYetenekSinavSistemi.Web.Controllers;

public sealed class AccountController : Controller
{
    private const string CaptchaSessionKey = "LoginCaptchaCode";

    private readonly IAuthenticationService _authService;
    private readonly IUserService _userService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly ICaptchaService _captchaService;
    private readonly IYgsYearRepository _ygsYearRepository;
    private readonly IPublicUrlBuilder _publicUrlBuilder;
    private readonly ICountryCatalog _countryCatalog;
    private readonly IIdentityDocumentValidator _identityDocumentValidator;
    private readonly IPhotoUploadService _photoUploadService;

    public AccountController(
        IAuthenticationService authService,
        IUserService userService,
        IPasswordResetService passwordResetService,
        ICaptchaService captchaService,
        IYgsYearRepository ygsYearRepository,
        IPublicUrlBuilder publicUrlBuilder,
        ICountryCatalog countryCatalog,
        IIdentityDocumentValidator identityDocumentValidator,
        IPhotoUploadService photoUploadService)
    {
        _authService = authService;
        _userService = userService;
        _passwordResetService = passwordResetService;
        _captchaService = captchaService;
        _ygsYearRepository = ygsYearRepository;
        _publicUrlBuilder = publicUrlBuilder;
        _countryCatalog = countryCatalog;
        _identityDocumentValidator = identityDocumentValidator;
        _photoUploadService = photoUploadService;
    }

    private string? Ip => HttpContext.GetClientIpString();
    private string CorrelationId => HttpContext.TraceIdentifier;

    // -----------------------------------------------------------------------
    // Kayıt
    // -----------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        var model = new RegisterViewModel
        {
            IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
            NationalityCountryCode = "TR",
            PhoneCountryCode = MobilePhoneNumber.DefaultRegionCode
        };
        PopulateCountryLists(model);
        BirthDateFormBinding.PopulateYearOptions(model, RegisterToday);
        await PopulateYgsYearsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(PhotoUploadLimits.MaxMultipartRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = PhotoUploadLimits.MaxMultipartRequestBytes,
        ValueLengthLimit = (int)PhotoUploadLimits.MaxMultipartRequestBytes,
        MemoryBufferThreshold = PhotoUploadLimits.MemoryBufferThresholdBytes)]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        var photoFieldKey = nameof(RegisterViewModel.Photo);

        DisabilityFormBinding.Normalize(
            model.HasDisability,
            value => model.DisabilityDetails = value,
            ModelState);

        // Gün/ay/yıldan BirthDate üret; istemci gizli tarih alanına güvenilmez.
        BirthDateFormBinding.Apply(model, ModelState, RegisterToday);

        var hasUploadedPhoto = HasUploadedPhoto(model.Photo);

        if (hasUploadedPhoto)
        {
            ClearPhotoFieldErrors(ModelState, photoFieldKey);
            await ApplyPhotoValidationAsync(model.Photo!, photoFieldKey, cancellationToken);
        }
        else
        {
            ModelState.AddModelError(photoFieldKey, PhotoUploadMessages.Required);
        }

        // Kimlik doğrulaması diğer alan hatalarından bağımsız çalışmalı; aksi halde
        // (ör. eksik fotoğraf) geçersiz T.C. numarası alan mesajı hiç yüzeye çıkmaz.
        ApplyIdentityDocumentValidation(model);

        if (!ModelState.IsValid)
        {
            await PopulateRegisterErrorViewAsync(model, cancellationToken);
            return View(model);
        }

        PhotoUploadRequest? photoRequest = null;
        try
        {
            photoRequest = PhotoUploadFormMapper.ToRequest(model.Photo!);
            var result = await _authService.RegisterCandidateAsync(model, photoRequest, Ip, CorrelationId, cancellationToken);

            if (!result.Success)
            {
                var mappedErrors = result.ValidationErrors.DefaultIfEmpty(result.ErrorMessage ?? "Kayıt işlemi başarısız.").ToList();
                foreach (var error in mappedErrors)
                {
                    var field = RegisterIdentityFieldMapper.MapRegistrationErrorToField(error);
                    if (!string.IsNullOrEmpty(field))
                        ModelState.AddModelError(field, error!);
                    else if (PhotoUploadMessages.IsPhotoError(error))
                        ModelState.AddModelError(photoFieldKey, error!);
                    else
                        ModelState.AddModelError(string.Empty, error!);
                }

                await PopulateRegisterErrorViewAsync(model, cancellationToken);
                return View(model);
            }
        }
        finally
        {
            photoRequest?.Content.Dispose();
        }

        TempData["ToastSuccess"] = "Kaydınız oluşturuldu. Giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
    }

    private static IdentityDocumentValidationRequest ToIdentityRequest(RegisterViewModel model) =>
        new()
        {
            DocumentType = model.IdentityDocumentType ?? IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = model.IdentityNumber,
            NationalityCountryCode = model.NationalityCountryCode,
            IssuingCountryCode = model.IssuingCountryCode,
            PassportExpiryDate = model.PassportExpiryDate
        };

    private void ApplyIdentityDocumentValidation(RegisterViewModel model)
    {
        var identityValidation = _identityDocumentValidator.Validate(ToIdentityRequest(model));
        if (identityValidation.IsValid)
            return;

        var field = RegisterIdentityFieldMapper.MapValidationErrorToField(
            identityValidation.ErrorMessage,
            model.IdentityDocumentType ?? IdentityDocumentType.TurkishIdentityNumber);
        AddModelErrorIfMissing(field, identityValidation.ErrorMessage!);
    }

    private async Task ApplyPhotoValidationAsync(
        IFormFile photo,
        string photoFieldKey,
        CancellationToken cancellationToken)
    {
        PhotoUploadRequest? request = null;
        try
        {
            request = PhotoUploadFormMapper.ToRequest(photo);
            var validation = await _photoUploadService.ValidatePhotoAsync(request, cancellationToken);
            if (!validation.Success)
            {
                AddModelErrorIfMissing(
                    photoFieldKey,
                    validation.UserMessage ?? PhotoUploadMessages.GenericFailure);
            }
        }
        catch
        {
            AddModelErrorIfMissing(photoFieldKey, PhotoUploadMessages.GenericFailure);
        }
        finally
        {
            request?.Content.Dispose();
        }
    }

    private void AddModelErrorIfMissing(string field, string message)
    {
        if (ModelState.TryGetValue(field, out var entry)
            && entry.Errors.Any(e => string.Equals(e.ErrorMessage, message, StringComparison.Ordinal)))
        {
            return;
        }

        ModelState.AddModelError(field, message);
    }

    private void PopulateCountryLists(RegisterViewModel model)
    {
        model.ForeignNationalityCountries = _countryCatalog.GetForeignNationalities();
        model.IssuingCountries = _countryCatalog.GetAll();
        model.PhoneCountries = _countryCatalog.GetPhoneCountries();
        if (string.IsNullOrWhiteSpace(model.PhoneCountryCode))
            model.PhoneCountryCode = MobilePhoneNumber.DefaultRegionCode;
    }

    private static bool HasUploadedPhoto(IFormFile? photo)
        => photo is not null
           && photo.Length > 0
           && !string.IsNullOrWhiteSpace(photo.FileName);

    private static void ClearLoginSensitiveFields(LoginViewModel model, ModelStateDictionary modelState)
    {
        model.Password = string.Empty;
        model.CaptchaInput = string.Empty;

        foreach (var key in new[] { nameof(LoginViewModel.Password), nameof(LoginViewModel.CaptchaInput) })
        {
            if (!modelState.TryGetValue(key, out var entry))
                continue;

            entry.RawValue = string.Empty;
            entry.AttemptedValue = string.Empty;
        }
    }

    private static void ClearPhotoFieldErrors(ModelStateDictionary modelState, string photoFieldKey)
    {
        if (!modelState.TryGetValue(photoFieldKey, out var entry))
            return;

        entry.Errors.Clear();
        entry.ValidationState = ModelValidationState.Valid;
    }

    private async Task PopulateRegisterErrorViewAsync(RegisterViewModel model, CancellationToken cancellationToken)
    {
        model.Password = string.Empty;
        model.ConfirmPassword = string.Empty;
        model.Photo = null;
        BirthDateFormBinding.SyncPartsFromComposedDate(model);
        PopulateCountryLists(model);
        BirthDateFormBinding.PopulateYearOptions(model, RegisterToday);
        await PopulateYgsYearsAsync(cancellationToken);
    }

    private DateOnly RegisterToday
    {
        get
        {
            var timeProvider = TimeProvider.System;
            if (HttpContext?.RequestServices is { } services
                && services.GetService(typeof(TimeProvider)) is TimeProvider resolved)
            {
                timeProvider = resolved;
            }

            return DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        }
    }

    // -----------------------------------------------------------------------
    // Giriş
    // -----------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var expectedCaptcha = HttpContext.Session.GetString(CaptchaSessionKey);
        HttpContext.Session.Remove(CaptchaSessionKey);

        if (!ModelState.IsValid)
        {
            ClearLoginSensitiveFields(model, ModelState);
            return View(model);
        }

        if (!_captchaService.Validate(expectedCaptcha, model.CaptchaInput))
        {
            ModelState.AddModelError(nameof(model.CaptchaInput), "Güvenlik kodu hatalı. Yeniden deneyiniz.");
            ClearLoginSensitiveFields(model, ModelState);
            return View(model);
        }

        if (!LoginIdentifierHelper.TryNormalizeLoginIdentifier(model.LoginIdentifier, out var normalizedIdentifier))
        {
            ModelState.AddModelError(string.Empty, LoginIdentifierHelper.LoginFailureMessage);
            ClearLoginSensitiveFields(model, ModelState);
            return View(model);
        }

        var result = await _authService.ValidateCredentialsAsync(normalizedIdentifier, model.Password, Ip, CorrelationId, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Giriş başarısız.");
            ClearLoginSensitiveFields(model, ModelState);
            return View(model);
        }

        await SignInUserAsync(result.Data, model.RememberMe);

        if (result.Data.MustChangePassword)
        {
            TempData["ToastWarning"] = "Güvenliğiniz için parolanızı değiştiriniz.";
            return RedirectToAction(nameof(ChangePassword));
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToHome(result.Data.RoleId);
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Captcha()
    {
        var challenge = _captchaService.Generate();
        HttpContext.Session.SetString(CaptchaSessionKey, challenge.Code);
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        return File(challenge.ImageBytes, challenge.ContentType);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("password-reset-request")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (LoginIdentifierHelper.TryNormalizeLoginIdentifier(model.TcNoOrEmail, out var normalizedIdentifier))
        {
            var resetBaseUrl = _publicUrlBuilder.BuildPasswordResetBaseUrl();
            await _passwordResetService.RequestResetAsync(
                normalizedIdentifier, resetBaseUrl, Ip, CorrelationId, cancellationToken);
        }

        TempData["ToastInfo"] = LoginIdentifierHelper.ForgotPasswordSuccessMessage;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ToastError"] = "Geçersiz parola sıfırlama bağlantısı.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("password-reset-submit")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _passwordResetService.ResetPasswordAsync(model, Ip, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Parola sıfırlanamadı.");
            return View(model);
        }

        TempData["ToastSuccess"] = "Parolanız güncellendi. Yeni parolanızla giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.ChangePasswordAsync(User.GetUserId(), model, Ip, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Parola değiştirilemedi.");
            return View(model);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["ToastSuccess"] = "Parolanız güncellendi. Yeni parolanızla yeniden giriş yapınız.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task SignInUserAsync(User user, bool rememberMe)
    {
        var roleName = DomainConstants.GetRoleName(user.RoleId) ?? DomainConstants.RoleNames.Candidate;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, roleName),
            new(DomainConstants.ClaimTypesCustom.UserId, user.Id.ToString()),
            new(DomainConstants.ClaimTypesCustom.FullName, user.FullName),
            new(DomainConstants.ClaimTypesCustom.SecurityStamp, user.SecurityStamp.ToString("D")),
            new("MustChangePassword", user.MustChangePassword ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    private IActionResult RedirectToHome(Guid? roleId = null)
    {
        var role = roleId is null ? User.GetRole() : DomainConstants.GetRoleName(roleId.Value);
        if (role == DomainConstants.RoleNames.Candidate)
        {
            return RedirectToAction("Index", "CandidateDashboard");
        }

        if (role is DomainConstants.RoleNames.SuperAdmin or DomainConstants.RoleNames.ApplicationManager)
        {
            return RedirectToAction("Index", "AdminDashboard");
        }

        return RedirectToAction("Index", "Home");
    }

    private async Task PopulateYgsYearsAsync(CancellationToken cancellationToken)
    {
        var years = await _ygsYearRepository.GetActiveAsync(cancellationToken);
        ViewBag.YgsYears = years;
    }
}

using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private const int MaxFailedLoginAttempts = 5;
    private const string LoginFailureMessage = "E-posta/T.C. Kimlik Numarası veya parola hatalı.";
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _userRepository;
    private readonly IYgsYearRepository _ygsYearRepository;
    private readonly IPasswordService _passwordService;
    private readonly IIdentityDocumentValidator _identityDocumentValidator;
    private readonly IPhotoUploadService _photoUploadService;
    private readonly IAuditService _auditService;
    private readonly ISensitiveDataMaskingService _masking;
    private readonly IKeyedAsyncLock _keyedLock;
    private readonly TimeProvider _timeProvider;

    public AuthenticationService(
        IUserRepository userRepository,
        IYgsYearRepository ygsYearRepository,
        IPasswordService passwordService,
        IIdentityDocumentValidator identityDocumentValidator,
        IPhotoUploadService photoUploadService,
        IAuditService auditService,
        ISensitiveDataMaskingService masking,
        IKeyedAsyncLock keyedLock,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _ygsYearRepository = ygsYearRepository;
        _passwordService = passwordService;
        _identityDocumentValidator = identityDocumentValidator;
        _photoUploadService = photoUploadService;
        _auditService = auditService;
        _masking = masking;
        _keyedLock = keyedLock;
        _timeProvider = timeProvider;
    }

    public async Task<OperationResult<Guid>> RegisterCandidateAsync(
        RegisterViewModel model,
        PhotoUploadRequest? photo,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var identityRequest = ResolveIdentityRequest(model);
        IdentityDocumentValidationResult? identityValidation = null;
        if (identityRequest is null)
        {
            errors.Add("Kimlik belgesi bilgilerini giriniz.");
        }
        else
        {
            identityValidation = _identityDocumentValidator.Validate(identityRequest);
            if (!identityValidation.IsValid)
                errors.Add(identityValidation.ErrorMessage!);
        }

        if (!InputTextRules.IsValidHumanName(model.FirstName, out _))
            errors.Add(InputTextRules.FormatHumanNameMessage("Ad"));

        if (!InputTextRules.IsValidHumanName(model.LastName, out _))
            errors.Add(InputTextRules.FormatHumanNameMessage("Soyad"));

        if (!BirthDateRules.IsValid(model.BirthDate, DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime)))
        {
            errors.Add(model.BirthDate is null
                ? BirthDateRules.RequiredMessage
                : BirthDateRules.FutureMessage);
        }

        if (model.YgsYearId is null ||
            await _ygsYearRepository.GetByIdAsync(model.YgsYearId.Value, cancellationToken) is null)
        {
            errors.Add("Geçersiz YGS yılı.");
        }

        if (DisabilityDetailsNormalizer.IsMissingWhenRequired(model.HasDisability, model.DisabilityDetails))
            errors.Add(DisabilityDetailsNormalizer.RequiredMessage);

        if (!HasUploadedPhoto(photo))
            errors.Add(PhotoUploadMessages.Required);

        if (errors.Count > 0)
            return OperationResult<Guid>.Invalid(errors);

        var documentType = identityRequest!.DocumentType;
        var normalizedIdentity = identityValidation!.NormalizedIdentityNumber!;
        var normalizedEmail = model.Email.Trim();
        var issuingCountry = identityValidation.NormalizedIssuingCountryCode;

        var lockKey = PhotoUploadLockKeys.ForRegister(
            documentType,
            normalizedIdentity,
            issuingCountry,
            normalizedEmail);

        await using var lockHandle = await _keyedLock.AcquireAsync(lockKey, cancellationToken).ConfigureAwait(false);

        if (await _userRepository.IdentityExistsAsync(documentType, normalizedIdentity, issuingCountry, cancellationToken))
            return OperationResult<Guid>.Fail(GetDuplicateIdentityMessage(documentType));

        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
            return OperationResult<Guid>.Fail("Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır.");

        var photoResult = await _photoUploadService.SavePhotoAsync(photo!, cancellationToken);

        if (!photoResult.Success)
            return OperationResult<Guid>.Fail(photoResult.UserMessage ?? PhotoUploadMessages.GenericFailure);
        var savedPhotoPath = photoResult.StoredPath!;

        var user = BuildCandidateUser(model, identityRequest, identityValidation, savedPhotoPath);
        user.PasswordHash = _passwordService.Hash(model.Password);

        Guid userId;
        try
        {
            userId = await _userRepository.AddAsync(user, cancellationToken);
        }
        catch (DuplicateUserRegistrationException ex)
        {
            await _photoUploadService.DeletePhotoAsync(savedPhotoPath, cancellationToken).ConfigureAwait(false);
            return ex.Kind switch
            {
                DuplicateUserRegistrationKind.Email =>
                    OperationResult<Guid>.Fail("Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır."),
                _ => OperationResult<Guid>.Fail(GetDuplicateIdentityMessage(documentType))
            };
        }
        catch
        {
            await _photoUploadService.DeletePhotoAsync(savedPhotoPath, cancellationToken).ConfigureAwait(false);
            return OperationResult<Guid>.Fail("Kayıt işlemi tamamlanamadı. Daha sonra yeniden deneyiniz.");
        }

        await _auditService.LogAsync(
            "CandidateRegistered",
            $"Aday kaydı oluşturuldu. LoginIdentifierType=IdentityDocument",
            userId, ipAddress, null, correlationId, cancellationToken);

        return OperationResult<Guid>.Ok(userId);
    }

    public async Task<OperationResult<User>> ValidateCredentialsAsync(
        string tcNo,
        string password,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var trimmed = tcNo.Trim();
        User? user;
        string auditDescription;

        if (LoginIdentifierHelper.LooksLikeEmail(trimmed))
        {
            user = await _userRepository.GetByEmailOrTurkishIdentityAsync(trimmed, cancellationToken);
            auditDescription = $"Bilinmeyen kullanıcı. LoginIdentifierType=Email Email={_masking.MaskEmail(trimmed)}";
        }
        else
        {
            var normalizedTc = IdentityNumberNormalizer.NormalizeTurkishIdentityNumber(trimmed);
            if (normalizedTc is null || !TurkishIdentityNumber.IsValid(normalizedTc))
            {
                await _auditService.LogAsync(
                    "LoginFailed",
                    $"Geçersiz giriş tanımlayıcısı. LoginIdentifierType=TurkishIdentity",
                    null, ipAddress, null, correlationId, cancellationToken);
                return OperationResult<User>.Fail(LoginFailureMessage);
            }

            user = await _userRepository.GetByEmailOrTurkishIdentityAsync(normalizedTc, cancellationToken);
            auditDescription = $"Bilinmeyen kullanıcı. LoginIdentifierType=TurkishIdentity Identity={_masking.MaskIdentityNumber(IdentityDocumentType.TurkishIdentityNumber, normalizedTc, null)}";
        }

        if (user is null)
        {
            await _auditService.LogAsync("LoginFailed", auditDescription, null, ipAddress, null, correlationId, cancellationToken);
            return OperationResult<User>.Fail(LoginFailureMessage);
        }

        var loginAuditSuffix = LoginIdentifierHelper.LooksLikeEmail(trimmed)
            ? $"LoginIdentifierType=Email Email={_masking.MaskEmail(trimmed)}"
            : $"LoginIdentifierType=TurkishIdentity Identity={_masking.MaskIdentityNumber(IdentityDocumentType.TurkishIdentityNumber, trimmed, null)}";

        if (!user.IsActive)
        {
            await _auditService.LogAsync("LoginFailed", $"Pasif hesap. {loginAuditSuffix}", user.Id, ipAddress, null, correlationId, cancellationToken);
            return OperationResult<User>.Fail("Hesabınız aktif değil. Yönetici ile iletişime geçiniz.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            await _auditService.LogAsync("LoginLocked", $"Kilitli hesap giriş denemesi. {loginAuditSuffix}", user.Id, ipAddress, null, correlationId, cancellationToken);
            return OperationResult<User>.Fail("Hesabınız çok sayıda başarısız giriş nedeniyle geçici olarak kilitlendi. Bir süre sonra yeniden deneyiniz.");
        }

        var valid = _passwordService.Verify(user.PasswordHash, password, out var needsRehash);
        if (!valid)
        {
            var failed = user.FailedLoginCount + 1;
            DateTime? lockoutEnd = null;
            if (failed >= MaxFailedLoginAttempts)
            {
                lockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                failed = 0;
            }

            await _userRepository.UpdateLoginStateAsync(user.Id, failed, lockoutEnd, cancellationToken);
            await _auditService.LogAsync("LoginFailed", $"Hatalı parola. {loginAuditSuffix}", user.Id, ipAddress, null, correlationId, cancellationToken);
            return OperationResult<User>.Fail(LoginFailureMessage);
        }

        if (needsRehash)
            await _userRepository.UpdatePasswordAsync(user.Id, _passwordService.Hash(password), user.MustChangePassword, cancellationToken);

        if (user.FailedLoginCount != 0 || user.LockoutEnd is not null)
            await _userRepository.UpdateLoginStateAsync(user.Id, 0, null, cancellationToken);

        await _auditService.LogAsync("LoginSucceeded", $"Başarılı giriş. {loginAuditSuffix}", user.Id, ipAddress, null, correlationId, cancellationToken);
        return OperationResult<User>.Ok(user);
    }

    private static bool HasUploadedPhoto(PhotoUploadRequest? photo)
        => photo is not null
           && photo.Length > 0
           && !string.IsNullOrWhiteSpace(photo.FileName);

    private static IdentityDocumentValidationRequest? ResolveIdentityRequest(RegisterViewModel model)
    {
        if (model.IdentityDocumentType.HasValue && !string.IsNullOrWhiteSpace(model.IdentityNumber))
        {
            return new IdentityDocumentValidationRequest
            {
                DocumentType = model.IdentityDocumentType.Value,
                IdentityNumber = model.IdentityNumber,
                NationalityCountryCode = model.NationalityCountryCode,
                IssuingCountryCode = model.IssuingCountryCode,
                PassportExpiryDate = model.PassportExpiryDate
            };
        }

        if (!string.IsNullOrWhiteSpace(model.TcNo))
        {
            return new IdentityDocumentValidationRequest
            {
                DocumentType = IdentityDocumentType.TurkishIdentityNumber,
                IdentityNumber = model.TcNo,
                NationalityCountryCode = "TR"
            };
        }

        return null;
    }

    private static User BuildCandidateUser(
        RegisterViewModel model,
        IdentityDocumentValidationRequest identityRequest,
        IdentityDocumentValidationResult identityValidation,
        string savedPhotoPath)
    {
        var normalizedIdentity = identityValidation.NormalizedIdentityNumber!;
        var documentType = identityRequest.DocumentType;
        var rawIdentity = identityRequest.IdentityNumber!.Trim();

        var user = new User
        {
            RoleId = DomainConstants.RoleIds.Candidate,
            IdentityDocumentType = documentType,
            IdentityNumber = rawIdentity,
            NormalizedIdentityNumber = normalizedIdentity,
            Email = model.Email.Trim(),
            FirstName = InputTextRules.NormalizeHumanName(model.FirstName) ?? string.Empty,
            LastName = InputTextRules.NormalizeHumanName(model.LastName) ?? string.Empty,
            BirthDate = model.BirthDate,
            // Legacy BirthYear: geriye uyumluluk; UI'dan toplanmaz, BirthDate.Year türetilir.
            BirthYear = checked((short)model.BirthDate!.Value.Year),
            HighSchool = InputTextRules.IsValidOptionalMeaningfulText(model.HighSchool, multiline: false, out var highSchool)
                ? highSchool
                : null,
            DepartmentField = InputTextRules.IsValidOptionalMeaningfulText(model.DepartmentField, multiline: false, out var departmentField)
                ? departmentField
                : null,
            YgsScore = model.YgsScore,
            YgsYearId = model.YgsYearId,
            Address = InputTextRules.IsValidOptionalMeaningfulText(model.Address, multiline: true, out var address)
                ? address
                : null,
            Phone = NormalizeRegisterPhone(model),
            HasDisability = model.HasDisability,
            DisabilityDetails = DisabilityDetailsNormalizer.ForStorage(model.HasDisability, model.DisabilityDetails),
            PhotoPath = savedPhotoPath,
            IsActive = true,
            MustChangePassword = false,
            SecurityStamp = Guid.NewGuid(),
            PasswordHash = string.Empty
        };

        switch (documentType)
        {
            case IdentityDocumentType.TurkishIdentityNumber:
                user.TcNo = normalizedIdentity;
                user.NationalityCountryCode = "TR";
                user.Nationality = model.Nationality?.Trim() ?? "T.C.";
                break;
            case IdentityDocumentType.ForeignIdentityNumber:
                user.TcNo = null;
                user.NationalityCountryCode = identityValidation.NormalizedNationalityCountryCode;
                user.Nationality = identityValidation.NormalizedNationalityCountryCode;
                break;
            case IdentityDocumentType.Passport:
                user.TcNo = null;
                user.NationalityCountryCode = identityValidation.NormalizedNationalityCountryCode;
                user.IssuingCountryCode = identityValidation.NormalizedIssuingCountryCode;
                user.PassportExpiryDate = identityRequest.PassportExpiryDate;
                user.Nationality = identityValidation.NormalizedNationalityCountryCode;
                break;
        }

        return user;
    }

    private static string NormalizeRegisterPhone(RegisterViewModel model)
    {
        if (!MobilePhoneNumber.TryValidateAndNormalize(
                model.PhoneCountryCode,
                model.Phone,
                out var e164,
                out _))
        {
            // Model doğrulaması geçtiyse buraya düşülmemeli; yine de ham değeri yazmayız.
            throw new InvalidOperationException("Telefon numarası normalize edilemedi.");
        }

        return e164!;
    }

    private static string GetDuplicateIdentityMessage(IdentityDocumentType documentType) =>
        documentType switch
        {
            IdentityDocumentType.TurkishIdentityNumber =>
                "Bu T.C. kimlik numarasıyla kayıtlı bir hesap bulunmaktadır.",
            IdentityDocumentType.ForeignIdentityNumber =>
                "Bu yabancı kimlik numarasıyla kayıtlı bir hesap bulunmaktadır.",
            IdentityDocumentType.Passport =>
                "Bu pasaport numarasıyla kayıtlı bir hesap bulunmaktadır.",
            _ => "Bu kimlik bilgileriyle kayıtlı bir hesap bulunmaktadır."
        };
}

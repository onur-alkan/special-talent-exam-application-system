using Microsoft.Extensions.Logging;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IYgsYearRepository _ygsYearRepository;
    private readonly IPhotoUploadService _photoUploadService;
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;
    private readonly ICountryCatalog _countryCatalog;
    private readonly ILogger<UserService> _logger;
    private readonly IKeyedAsyncLock _keyedLock;

    public UserService(
        IUserRepository userRepository,
        IYgsYearRepository ygsYearRepository,
        IPhotoUploadService photoUploadService,
        IPasswordService passwordService,
        IAuditService auditService,
        ICountryCatalog countryCatalog,
        ILogger<UserService> logger,
        IKeyedAsyncLock keyedLock)
    {
        _userRepository = userRepository;
        _ygsYearRepository = ygsYearRepository;
        _photoUploadService = photoUploadService;
        _passwordService = passwordService;
        _auditService = auditService;
        _countryCatalog = countryCatalog;
        _logger = logger;
        _keyedLock = keyedLock;
    }

    public async Task<OperationResult<ProfileViewModel>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return OperationResult<ProfileViewModel>.Fail("Kullanıcı bulunamadı.");

        var vm = new ProfileViewModel
        {
            Id = user.Id,
            BirthDate = user.BirthDate,
            BirthYear = user.BirthYear,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            HighSchool = user.HighSchool,
            DepartmentField = user.DepartmentField,
            YgsScore = user.YgsScore,
            YgsYearId = user.YgsYearId,
            Address = user.Address,
            Phone = user.Phone,
            HasDisability = user.HasDisability,
            DisabilityDetails = user.DisabilityDetails,
            PhotoPath = user.PhotoPath,
            Identity = ProfileIdentityDisplayBuilder.Build(user, _countryCatalog)
        };

        return OperationResult<ProfileViewModel>.Ok(vm);
    }

    public async Task<OperationResult> UpdateProfileAsync(
        Guid userId,
        ProfileUpdateViewModel model,
        PhotoUploadRequest? photo,
        CancellationToken cancellationToken = default)
    {
        var lockKey = PhotoUploadLockKeys.ForProfile(userId);
        await using var lockHandle = await _keyedLock.AcquireAsync(lockKey, cancellationToken).ConfigureAwait(false);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return OperationResult.Fail("Kullanıcı bulunamadı.");

        if (model.YgsYearId is not null &&
            await _ygsYearRepository.GetByIdAsync(model.YgsYearId.Value, cancellationToken) is null)
        {
            return OperationResult.Fail("Geçerli bir YGS yılı seçiniz.");
        }

        if (!InputTextRules.IsValidHumanName(model.FirstName, out _))
            return OperationResult.Fail(InputTextRules.FormatHumanNameMessage("Ad"));

        if (!InputTextRules.IsValidHumanName(model.LastName, out _))
            return OperationResult.Fail(InputTextRules.FormatHumanNameMessage("Soyad"));

        if (DisabilityDetailsNormalizer.IsMissingWhenRequired(model.HasDisability, model.DisabilityDetails))
            return OperationResult.Fail(DisabilityDetailsNormalizer.RequiredMessage);

        if (!string.Equals(user.Email, model.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userRepository.GetByEmailAsync(model.Email.Trim(), cancellationToken);
            if (existing is not null && existing.Id != userId)
                return OperationResult.Fail("Bu e-posta adresi başka bir kullanıcı tarafından kullanılıyor.");
        }

        var previousPhotoPath = user.PhotoPath;
        string? newPhotoPath = null;

        if (photo is not null && photo.Length > 0)
        {
            var photoResult = await _photoUploadService.SavePhotoAsync(photo, cancellationToken);
            if (!photoResult.Success)
                return OperationResult.Fail(photoResult.UserMessage ?? PhotoUploadMessages.GenericFailure);

            newPhotoPath = photoResult.StoredPath;
            user.PhotoPath = newPhotoPath;
        }

        user.FirstName = InputTextRules.NormalizeHumanName(model.FirstName) ?? string.Empty;
        user.LastName = InputTextRules.NormalizeHumanName(model.LastName) ?? string.Empty;
        user.Email = model.Email.Trim();
        user.HighSchool = InputTextRules.IsValidOptionalMeaningfulText(model.HighSchool, multiline: false, out var highSchool)
            ? highSchool
            : null;
        user.DepartmentField = InputTextRules.IsValidOptionalMeaningfulText(model.DepartmentField, multiline: false, out var departmentField)
            ? departmentField
            : null;
        user.YgsScore = model.YgsScore;
        user.YgsYearId = model.YgsYearId;
        user.Address = InputTextRules.IsValidOptionalMeaningfulText(model.Address, multiline: true, out var address)
            ? address
            : null;
        user.Phone = model.Phone?.Trim();
        user.HasDisability = model.HasDisability;
        user.DisabilityDetails = DisabilityDetailsNormalizer.ForStorage(model.HasDisability, model.DisabilityDetails);

        bool updated;
        try
        {
            updated = await _userRepository.UpdateAsync(user, cancellationToken);
        }
        catch
        {
            if (newPhotoPath is not null)
                await _photoUploadService.DeletePhotoAsync(newPhotoPath, cancellationToken).ConfigureAwait(false);

            return OperationResult.Fail("Profil güncellenemedi.");
        }

        if (!updated)
        {
            if (newPhotoPath is not null)
                await _photoUploadService.DeletePhotoAsync(newPhotoPath, cancellationToken).ConfigureAwait(false);

            return OperationResult.Fail("Profil güncellenemedi.");
        }

        if (newPhotoPath is not null
            && !string.IsNullOrWhiteSpace(previousPhotoPath)
            && !string.Equals(previousPhotoPath, newPhotoPath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _photoUploadService.DeletePhotoAsync(previousPhotoPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Profil güncellemesi sonrası eski fotoğraf silinemedi. UserId={UserId}", userId);
            }
        }

        await _auditService.LogAsync("ProfileUpdated", "Profil güncellendi.", userId, null, null, null, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordViewModel model,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return OperationResult.Fail("Kullanıcı bulunamadı.");

        if (!_passwordService.Verify(user.PasswordHash, model.CurrentPassword, out _))
            return OperationResult.Fail("Mevcut parola doğru değil.");

        var newHash = _passwordService.Hash(model.NewPassword);
        var ok = await _userRepository.UpdatePasswordAsync(userId, newHash, mustChangePassword: false, cancellationToken);
        if (!ok)
            return OperationResult.Fail("Parola güncellenemedi.");

        await _auditService.LogAsync("PasswordChanged", "Kullanıcı parolasını değiştirdi.", userId, ipAddress, null, null, cancellationToken);
        return OperationResult.Ok();
    }
}

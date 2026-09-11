using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IUserService
{
    Task<OperationResult<ProfileViewModel>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateProfileAsync(
        Guid userId,
        ProfileUpdateViewModel model,
        PhotoUploadRequest? photo,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordViewModel model,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IAuthenticationService
{
    Task<OperationResult<Guid>> RegisterCandidateAsync(
        RegisterViewModel model,
        PhotoUploadRequest? photo,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// T.C. No + parola doğrular. Başarısız giriş sayacı ve hesap kilidini yönetir.
    /// Başarılıysa oturum açacak controller için User döner.
    /// </summary>
    Task<OperationResult<User>> ValidateCredentialsAsync(
        string tcNo,
        string password,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default);
}

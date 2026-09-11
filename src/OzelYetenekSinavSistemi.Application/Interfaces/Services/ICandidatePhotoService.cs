using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Aday fotoğraflarına yetki kontrollü erişim sağlar.
/// </summary>
public interface ICandidatePhotoService
{
    Task<OperationResult<PhotoFileContent>> GetCurrentUserPhotoAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<PhotoFileContent>> GetApplicationPhotoAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        CancellationToken cancellationToken = default);
}

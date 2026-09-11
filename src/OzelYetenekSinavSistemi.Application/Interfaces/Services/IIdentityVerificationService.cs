using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Gelecekteki MERNİS/KPS doğrulaması için sözleşme.
/// Bu değişiklik kapsamında production implementasyonu veya DI kaydı yoktur.
/// </summary>
public interface IIdentityVerificationService
{
    Task<IdentityVerificationResult> VerifyAsync(
        string tcNo,
        string firstName,
        string lastName,
        int birthYear,
        CancellationToken cancellationToken = default);
}

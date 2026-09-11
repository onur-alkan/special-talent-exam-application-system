using OzelYetenekSinavSistemi.Application.ViewModels.Documents;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IDocumentVerificationService
{
    Task<DocumentVerificationResultViewModel> VerifyAsync(string? verificationCode, CancellationToken cancellationToken = default);
}

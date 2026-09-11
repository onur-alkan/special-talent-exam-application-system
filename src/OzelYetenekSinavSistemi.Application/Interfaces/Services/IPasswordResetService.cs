using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IPasswordResetService
{
    /// <summary>
    /// Sıfırlama talebi oluşturur. Hesabın varlığını dışarıya belli etmemek için
    /// her durumda genel başarı sonucu döner.
    /// </summary>
    Task<OperationResult> RequestResetAsync(
        string tcNoOrEmail,
        string resetLinkBaseUrl,
        string? ipAddress,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ResetPasswordAsync(
        ResetPasswordViewModel model,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Parola sıfırlama token tüketimi ile parola güncellemesini tek SQL transaction içinde atomik yürütür.
/// </summary>
public interface IPasswordResetTransactionService
{
    /// <summary>
    /// TokenHash üzerinden tokenı kilitler; geçerliyse parolayı günceller ve tokenı tüketir.
    /// Token veya parola değerlerini loglamaz.
    /// </summary>
    Task<PasswordResetConsumeResult> ConsumeTokenAndUpdatePasswordAsync(
        string tokenHash,
        string newPasswordHash,
        CancellationToken cancellationToken = default);
}

using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// E-posta gönderim soyutlaması.
/// Development'ta File, Production'da Smtp implementasyonu kullanılır.
/// </summary>
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Loglarda ve belgelerde hassas verilerin maskelenmesi.
/// </summary>
public interface ISensitiveDataMaskingService
{
    /// <summary>T.C. Kimlik No maskesi, örn: 123****89.</summary>
    string MaskTcNo(string? tcNo);

    /// <summary>Kimlik belgesi türüne göre maskelenmiş kimlik numarası.</summary>
    string MaskIdentityNumber(
        IdentityDocumentType? type,
        string? identityNumber,
        string? legacyTcNo);

    string MaskEmail(string? email);

    /// <summary>Telefon maskesi, örn: +90******4567.</summary>
    string MaskPhone(string? phone);

    /// <summary>Log injection önlemi: CR/LF/tab/kontrol karakterlerini temizler ve uzunluğu sınırlar.</summary>
    string SanitizeForLog(string? input);

    string SanitizeForLog(string? input, int maxLength);

    string SanitizeCorrelationId(string? correlationId);

    string SanitizeRequestPath(string? requestPath);

    string SanitizeEventType(string? eventType);

    string? SanitizeDescription(string? description);

    string? SanitizeIpAddress(string? ipAddress);
}

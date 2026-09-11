namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Audit olaylarının veritabanına yazılması. Girdiler log injection'a karşı temizlenir.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string eventType,
        string? description,
        Guid? userId = null,
        string? ipAddress = null,
        string? requestPath = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// Yapılandırılmış saklama politikasına göre eski log/audit/token kayıtlarını temizler.
/// </summary>
public interface IDataRetentionCleanupService
{
    Task RunOnceAsync(CancellationToken cancellationToken = default);
}

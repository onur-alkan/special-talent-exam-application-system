using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    /// <summary>
    /// En yeni denetim kayıtlarını döner. take 1-200 aralığına sınırlandırılır.
    /// </summary>
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, int skip = 0, CancellationToken cancellationToken = default);
}

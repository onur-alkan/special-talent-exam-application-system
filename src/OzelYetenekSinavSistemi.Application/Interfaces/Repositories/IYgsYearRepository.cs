using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IYgsYearRepository : IGenericRepository<YgsYear>
{
    Task<IReadOnlyList<YgsYear>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<bool> YearExistsAsync(int year, CancellationToken cancellationToken = default);
}

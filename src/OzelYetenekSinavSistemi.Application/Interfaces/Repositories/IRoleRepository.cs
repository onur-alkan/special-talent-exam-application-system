using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IRoleRepository : IGenericRepository<Role>
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

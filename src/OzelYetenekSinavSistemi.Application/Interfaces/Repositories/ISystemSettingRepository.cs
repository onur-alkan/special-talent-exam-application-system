using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface ISystemSettingRepository : IGenericRepository<SystemSetting>
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> UpdateValueAsync(string key, string? value, Guid? updatedByUserId, CancellationToken cancellationToken = default);
}

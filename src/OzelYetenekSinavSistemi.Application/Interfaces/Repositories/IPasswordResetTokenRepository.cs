using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository : IGenericRepository<PasswordResetToken>
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> MarkAsUsedAsync(Guid id, CancellationToken cancellationToken = default);
    Task InvalidateActiveTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}

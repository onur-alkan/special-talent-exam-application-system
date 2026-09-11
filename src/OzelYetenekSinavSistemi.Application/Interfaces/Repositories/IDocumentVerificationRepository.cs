using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IDocumentVerificationRepository : IGenericRepository<DocumentVerification>
{
    Task<DocumentVerification?> GetByCodeAsync(string verificationCode, CancellationToken cancellationToken = default);
    Task<DocumentVerification?> GetByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
}

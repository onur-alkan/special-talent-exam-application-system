using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IExamPeriodRepository : IGenericRepository<ExamPeriod>
{
    Task<IReadOnlyList<ExamPeriod>> GetAllNotDeletedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExamPeriod>> GetActiveForCandidatesAsync(DateTime now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExamPeriod>> GetByManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetClosedAsync(Guid id, bool isClosed, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IExamPeriodManagerRepository : IGenericRepository<ExamPeriodManager>
{
    Task<IReadOnlyList<ExamPeriodManager>> GetByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<bool> IsManagerOfExamPeriodAsync(Guid managerUserId, Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<ManagerAssignmentWriteResult> AssignManagerAtomicAsync(ManagerAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<ManagerAssignmentWriteResult> RemoveManagerAtomicAsync(ManagerAssignmentRequest request, CancellationToken cancellationToken = default);
    Task<bool> RemoveAssignmentAsync(Guid examPeriodId, Guid managerUserId, CancellationToken cancellationToken = default);
}

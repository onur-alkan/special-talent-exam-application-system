using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IExamResultService
{
    Task<OperationResult<IReadOnlyList<CandidateApplicationDetail>>> GetCandidatesForExamAsync(
        Guid examPeriodId, Guid currentUserId, string role, CancellationToken cancellationToken = default);

    Task<OperationResult<CandidateApplicationDetail>> GetCandidateForEvaluationAsync(
        Guid applicationId, Guid currentUserId, string role, CancellationToken cancellationToken = default);

    Task<OperationResult> SaveResultAsync(
        ExamResultFormViewModel model,
        Guid evaluatorUserId,
        string role,
        string? ipAddress,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}

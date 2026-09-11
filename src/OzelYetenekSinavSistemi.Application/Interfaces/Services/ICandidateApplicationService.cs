using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.ViewModels.Applications;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface ICandidateApplicationService
{
    Task<IReadOnlyList<ExamPeriod>> GetActiveExamPeriodsAsync(CancellationToken cancellationToken = default);

    Task<OperationResult<ApplicationFormData>> GetApplicationFormAsync(Guid examPeriodId, Guid userId, CancellationToken cancellationToken = default);

    Task<OperationResult<CandidateApplicationCreationResult>> ApplyAsync(
        Guid userId,
        CreateApplicationViewModel model,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateApplicationAsync(
        Guid userId,
        Guid applicationId,
        CreateApplicationViewModel model,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandidateApplicationDetail>> GetMyApplicationsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<OperationResult<CandidateApplicationDetail>> GetMyApplicationDetailAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken = default);
}

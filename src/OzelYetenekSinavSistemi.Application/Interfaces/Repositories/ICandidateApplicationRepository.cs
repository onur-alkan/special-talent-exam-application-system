using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface ICandidateApplicationRepository : IGenericRepository<CandidateApplication>
{
    /// <summary>
    /// Sınav dönemi / tercih doğrulaması, CandidateNo, başvuru, tercihler,
    /// doğrulama kodu ve audit kaydını tek SQL transaction içinde atomik oluşturur.
    /// </summary>
    Task<ApplicationWriteResult> CreateApplicationAtomicAsync(
        CandidateApplicationCreationRequest request,
        CancellationToken cancellationToken = default);

    Task<CandidateApplication?> GetByUserAndExamAsync(Guid userId, Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<CandidateApplicationDetail?> GetDetailByIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateApplicationDetail>> GetDetailsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateApplicationDetail>> GetDetailsByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CandidateSelectedPreference>> GetSelectedPreferencesAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Başvuru sahipliği, sınav dönemi açıklığı ve tercih doğrulamasını
    /// tek SQL transaction içinde atomik günceller.
    /// </summary>
    Task<ApplicationWriteResult> UpdatePreferencesAtomicAsync(
        Guid applicationId,
        Guid userId,
        IReadOnlyList<Guid> orderedPreferenceOptionIds,
        CancellationToken cancellationToken = default);
}

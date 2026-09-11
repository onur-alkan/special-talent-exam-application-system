using OzelYetenekSinavSistemi.Application.DTOs.ExamResults;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface ICandidateExamResultRepository : IGenericRepository<CandidateExamResult>
{
    Task<CandidateExamResult?> GetByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yetki, eşzamanlılık ve audit kaydını tek SQL transaction içinde atomik olarak kaydeder.
    /// </summary>
    Task<ExamResultWriteResult> SaveResultAtomicAsync(
        ExamResultWriteRequest request,
        CancellationToken cancellationToken = default);
}

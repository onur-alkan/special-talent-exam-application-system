using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IExamPreferenceOptionRepository : IGenericRepository<ExamPreferenceOption>
{
    Task<IReadOnlyList<ExamPreferenceOption>> GetByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExamPreferenceOption>> GetActiveByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default);

    /// <summary>Yönetici DataTables için toplam/filtre/sayfa sorgusu (veritabanında sayfalanır).</summary>
    Task<PreferenceOptionPagedResult> SearchForDataTablesAsync(
        PreferenceOptionDataTablesQuery query,
        CancellationToken cancellationToken = default);

    Task<int> GetMaxDisplayOrderAsync(Guid examPeriodId, CancellationToken cancellationToken = default);

    Task<PreferenceOptionWriteResult> AddAtomicAsync(PreferenceOptionWriteRequest request, CancellationToken cancellationToken = default);
    Task<PreferenceOptionWriteResult> UpdateAtomicAsync(PreferenceOptionWriteRequest request, CancellationToken cancellationToken = default);
    Task<PreferenceOptionWriteResult> SetActiveAtomicAsync(PreferenceOptionWriteRequest request, CancellationToken cancellationToken = default);
    Task<PreferenceOptionWriteResult> DeleteAtomicAsync(PreferenceOptionWriteRequest request, CancellationToken cancellationToken = default);
}

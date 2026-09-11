using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IExamPeriodService
{
    Task<IReadOnlyList<ExamPeriod>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Rol ve atama kontrolüne göre kullanıcının görebileceği sınav dönemleri.</summary>
    Task<IReadOnlyList<ExamPeriod>> GetVisibleForUserAsync(Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Kaynak sahipliği/atama kontrolü ile tek sınav dönemi getirir.</summary>
    Task<OperationResult<ExamPeriod>> GetByIdForUserAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(ExamPeriodFormViewModel model, Guid createdByUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(ExamPeriodFormViewModel model, Guid currentUserId, string role, string? ipAddress, CancellationToken cancellationToken = default);
    Task<OperationResult> SetActiveAsync(Guid id, bool isActive, Guid currentUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<OperationResult> CloseAsync(Guid id, Guid currentUserId, string? ipAddress, CancellationToken cancellationToken = default);
    Task<OperationResult> SoftDeleteAsync(Guid id, Guid currentUserId, string? ipAddress, CancellationToken cancellationToken = default);

    // Tercih seçenekleri
    Task<IReadOnlyList<ExamPreferenceOption>> GetPreferenceOptionsAsync(Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<ExamPreferenceOption?> GetPreferenceOptionAsync(Guid optionId, CancellationToken cancellationToken = default);
    Task<PreferenceOptionDataTablesResponse> SearchPreferenceOptionsForDataTablesAsync(
        PreferenceOptionDataTablesQuery query,
        CancellationToken cancellationToken = default);
    Task<int> GetNextPreferenceDisplayOrderAsync(Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> AddPreferenceOptionAsync(
        PreferenceOptionFormViewModel model,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> UpdatePreferenceOptionAsync(
        PreferenceOptionFormViewModel model,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> SetPreferenceOptionActiveAsync(
        Guid optionId,
        bool isActive,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> DeletePreferenceOptionAsync(
        Guid optionId,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    // Yönetici atama
    Task<IReadOnlyList<ExamPeriodManager>> GetManagersAsync(Guid examPeriodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAssignableManagersAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AssignManagerAsync(Guid examPeriodId, Guid managerUserId, Guid assignedByUserId, string? ipAddress, string? correlationId = null, CancellationToken cancellationToken = default);
    Task<OperationResult> RemoveManagerAsync(Guid examPeriodId, Guid managerUserId, Guid actorUserId, string? ipAddress = null, string? correlationId = null, CancellationToken cancellationToken = default);
}

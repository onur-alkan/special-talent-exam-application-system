using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class ExamPeriodService : IExamPeriodService
{
    private readonly IExamPeriodRepository _examPeriodRepository;
    private readonly IExamPreferenceOptionRepository _optionRepository;
    private readonly IExamPeriodManagerRepository _managerRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditService _auditService;

    public ExamPeriodService(
        IExamPeriodRepository examPeriodRepository,
        IExamPreferenceOptionRepository optionRepository,
        IExamPeriodManagerRepository managerRepository,
        IUserRepository userRepository,
        IAuditService auditService)
    {
        _examPeriodRepository = examPeriodRepository;
        _optionRepository = optionRepository;
        _managerRepository = managerRepository;
        _userRepository = userRepository;
        _auditService = auditService;
    }

    public Task<IReadOnlyList<ExamPeriod>> GetAllAsync(CancellationToken cancellationToken = default)
        => _examPeriodRepository.GetAllNotDeletedAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamPeriod>> GetVisibleForUserAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (role == DomainConstants.RoleNames.SuperAdmin)
            return await _examPeriodRepository.GetAllNotDeletedAsync(cancellationToken);

        if (role == DomainConstants.RoleNames.ApplicationManager)
            return await _examPeriodRepository.GetByManagerAsync(userId, cancellationToken);

        return Array.Empty<ExamPeriod>();
    }

    public async Task<OperationResult<ExamPeriod>> GetByIdForUserAsync(Guid id, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var period = await _examPeriodRepository.GetByIdAsync(id, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult<ExamPeriod>.Fail("Sınav dönemi bulunamadı.");

        if (role == DomainConstants.RoleNames.SuperAdmin)
            return OperationResult<ExamPeriod>.Ok(period);

        if (role == DomainConstants.RoleNames.ApplicationManager)
        {
            var isManager = await _managerRepository.IsManagerOfExamPeriodAsync(userId, id, cancellationToken);
            if (!isManager)
                return OperationResult<ExamPeriod>.Fail("Bu sınav dönemine erişim yetkiniz yok.");
            return OperationResult<ExamPeriod>.Ok(period);
        }

        return OperationResult<ExamPeriod>.Fail("Bu sınav dönemine erişim yetkiniz yok.");
    }

    public async Task<OperationResult<Guid>> CreateAsync(ExamPeriodFormViewModel model, Guid createdByUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (!InputTextRules.IsValidMeaningfulTitle(model.Title, out var title))
            return OperationResult<Guid>.Fail(InputTextRules.FormatMeaningfulTitleMessage("Sınav / Başvuru Adı"));

        if (model.EndDate <= model.StartDate)
            return OperationResult<Guid>.Fail("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (model.MaxPreferences < 1)
            return OperationResult<Guid>.Fail("Maksimum tercih sayısını 1 veya daha büyük giriniz.");

        InputTextRules.IsValidOptionalMeaningfulText(model.Description, multiline: true, out var description);

        var period = new ExamPeriod
        {
            Title = title!,
            Description = description,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            MaxPreferences = model.MaxPreferences,
            IsActive = model.IsActive,
            IsClosed = false,
            IsDeleted = false,
            CreatedByUserId = createdByUserId
        };

        var id = await _examPeriodRepository.AddAsync(period, cancellationToken);
        await _auditService.LogAsync("ExamPeriodCreated", $"Sınav dönemi oluşturuldu: {period.Title}", createdByUserId, ipAddress, null, null, cancellationToken);
        return OperationResult<Guid>.Ok(id);
    }

    public async Task<OperationResult> UpdateAsync(ExamPeriodFormViewModel model, Guid currentUserId, string role, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (model.Id is null)
            return OperationResult.Fail("Geçersiz sınav dönemi.");

        var access = await GetByIdForUserAsync(model.Id.Value, currentUserId, role, cancellationToken);
        if (!access.Success)
            return OperationResult.Fail(access.ErrorMessage!);

        // Sınav dönemi ayarlarını yalnızca SuperAdmin değiştirebilir.
        if (role != DomainConstants.RoleNames.SuperAdmin)
            return OperationResult.Fail("Sınav dönemi bilgilerini yalnızca Süper Admin güncelleyebilir.");

        if (model.EndDate <= model.StartDate)
            return OperationResult.Fail("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");

        if (!InputTextRules.IsValidMeaningfulTitle(model.Title, out var title))
            return OperationResult.Fail(InputTextRules.FormatMeaningfulTitleMessage("Sınav / Başvuru Adı"));

        InputTextRules.IsValidOptionalMeaningfulText(model.Description, multiline: true, out var description);

        var period = access.Data!;
        period.Title = title!;
        period.Description = description;
        period.StartDate = model.StartDate;
        period.EndDate = model.EndDate;
        period.MaxPreferences = model.MaxPreferences;
        period.IsActive = model.IsActive;

        var ok = await _examPeriodRepository.UpdateAsync(period, cancellationToken);
        if (!ok)
            return OperationResult.Fail("Sınav dönemi güncellenemedi.");

        await _auditService.LogAsync("ExamPeriodUpdated", $"Sınav dönemi güncellendi: {period.Title}", currentUserId, ipAddress, null, null, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SetActiveAsync(Guid id, bool isActive, Guid currentUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var period = await _examPeriodRepository.GetByIdAsync(id, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult.Fail("Sınav dönemi bulunamadı.");

        await _examPeriodRepository.SetActiveAsync(id, isActive, cancellationToken);
        await _auditService.LogAsync("ExamPeriodActiveChanged", $"Sınav dönemi aktiflik={isActive}: {period.Title}", currentUserId, ipAddress, null, null, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> CloseAsync(Guid id, Guid currentUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var period = await _examPeriodRepository.GetByIdAsync(id, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult.Fail("Sınav dönemi bulunamadı.");

        await _examPeriodRepository.SetClosedAsync(id, true, cancellationToken);
        await _auditService.LogAsync("ExamPeriodClosed", $"Sınav dönemi kapatıldı: {period.Title}", currentUserId, ipAddress, null, null, cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SoftDeleteAsync(Guid id, Guid currentUserId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var period = await _examPeriodRepository.GetByIdAsync(id, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult.Fail("Sınav dönemi bulunamadı.");

        await _examPeriodRepository.SoftDeleteAsync(id, cancellationToken);
        await _auditService.LogAsync("ExamPeriodDeleted", $"Sınav dönemi soft-delete: {period.Title}", currentUserId, ipAddress, null, null, cancellationToken);
        return OperationResult.Ok();
    }

    public Task<IReadOnlyList<ExamPreferenceOption>> GetPreferenceOptionsAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
        => _optionRepository.GetByExamPeriodAsync(examPeriodId, cancellationToken);

    public Task<ExamPreferenceOption?> GetPreferenceOptionAsync(Guid optionId, CancellationToken cancellationToken = default)
        => _optionRepository.GetByIdAsync(optionId, cancellationToken);

    public async Task<PreferenceOptionDataTablesResponse> SearchPreferenceOptionsForDataTablesAsync(
        PreferenceOptionDataTablesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var page = await _optionRepository.SearchForDataTablesAsync(query, cancellationToken).ConfigureAwait(false);
            return new PreferenceOptionDataTablesResponse
            {
                Draw = query.Draw,
                RecordsTotal = page.RecordsTotal,
                RecordsFiltered = page.RecordsFiltered,
                Data = page.Rows
            };
        }
        catch
        {
            return new PreferenceOptionDataTablesResponse
            {
                Draw = query.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<PreferenceOptionTableRowDto>(),
                Error = "Tercih seçenekleri yüklenirken bir hata oluştu. Lütfen yeniden deneyiniz."
            };
        }
    }

    public async Task<int> GetNextPreferenceDisplayOrderAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        var max = await _optionRepository.GetMaxDisplayOrderAsync(examPeriodId, cancellationToken).ConfigureAwait(false);
        return max + 1;
    }

    public async Task<OperationResult<Guid>> AddPreferenceOptionAsync(
        PreferenceOptionFormViewModel model,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePreferenceOption(model.PreferenceName, model.DisplayOrder);
        if (validationError is not null)
            return OperationResult<Guid>.Fail(validationError);

        var write = await _optionRepository.AddAtomicAsync(new PreferenceOptionWriteRequest
        {
            ActorUserId = actorUserId,
            ExamPeriodId = model.ExamPeriodId,
            PreferenceName = InputTextRules.NormalizeSingleLineWhitespace(model.PreferenceName) ?? string.Empty,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        return MapPreferenceWrite(write);
    }

    public async Task<OperationResult<Guid>> UpdatePreferenceOptionAsync(
        PreferenceOptionFormViewModel model,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (model.Id is null)
            return OperationResult<Guid>.Fail("Geçersiz tercih seçeneği.");

        var validationError = ValidatePreferenceOption(model.PreferenceName, model.DisplayOrder);
        if (validationError is not null)
            return OperationResult<Guid>.Fail(validationError);

        var write = await _optionRepository.UpdateAtomicAsync(new PreferenceOptionWriteRequest
        {
            ActorUserId = actorUserId,
            OptionId = model.Id,
            PreferenceName = InputTextRules.NormalizeSingleLineWhitespace(model.PreferenceName) ?? string.Empty,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        return MapPreferenceWrite(write);
    }

    public async Task<OperationResult<Guid>> SetPreferenceOptionActiveAsync(
        Guid optionId,
        bool isActive,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var write = await _optionRepository.SetActiveAtomicAsync(new PreferenceOptionWriteRequest
        {
            ActorUserId = actorUserId,
            OptionId = optionId,
            IsActive = isActive,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        return MapPreferenceWrite(write);
    }

    public async Task<OperationResult<Guid>> DeletePreferenceOptionAsync(
        Guid optionId,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var write = await _optionRepository.DeleteAtomicAsync(new PreferenceOptionWriteRequest
        {
            ActorUserId = actorUserId,
            OptionId = optionId,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        return MapPreferenceWrite(write);
    }

    public Task<IReadOnlyList<ExamPeriodManager>> GetManagersAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
        => _managerRepository.GetByExamPeriodAsync(examPeriodId, cancellationToken);

    public Task<IReadOnlyList<User>> GetAssignableManagersAsync(CancellationToken cancellationToken = default)
        => _userRepository.GetActiveByRoleAsync(DomainConstants.RoleIds.ApplicationManager, cancellationToken);

    public async Task<OperationResult> AssignManagerAsync(Guid examPeriodId, Guid managerUserId, Guid assignedByUserId, string? ipAddress, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        var write = await _managerRepository.AssignManagerAtomicAsync(new ManagerAssignmentRequest
        {
            ActorUserId = assignedByUserId,
            ExamPeriodId = examPeriodId,
            ManagerUserId = managerUserId,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        if (!write.Success)
            return OperationResult.Fail(MapAssignError(write.Status));

        return OperationResult.Ok();
    }

    public async Task<OperationResult> RemoveManagerAsync(
        Guid examPeriodId,
        Guid managerUserId,
        Guid actorUserId,
        string? ipAddress = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var write = await _managerRepository.RemoveManagerAtomicAsync(new ManagerAssignmentRequest
        {
            ActorUserId = actorUserId,
            ExamPeriodId = examPeriodId,
            ManagerUserId = managerUserId,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        }, cancellationToken);

        if (!write.Success)
            return OperationResult.Fail(MapRemoveError(write.Status));

        return OperationResult.Ok();
    }

    private static string? ValidatePreferenceOption(string? preferenceName, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(preferenceName))
            return "Tercih adını giriniz.";

        if (!InputTextRules.IsValidMeaningfulTitle(preferenceName, out var normalized))
            return InputTextRules.FormatMeaningfulTitleMessage("Tercih adı");

        if (normalized!.Length > 100)
            return "Tercih adı en fazla 100 karakter olabilir.";
        if (displayOrder <= 0)
            return "Görünüm sırasını 1 veya daha büyük giriniz.";
        return null;
    }

    private static OperationResult<Guid> MapPreferenceWrite(PreferenceOptionWriteResult write)
    {
        if (write.Success && write.ExamPeriodId is { } examPeriodId)
            return OperationResult<Guid>.Ok(examPeriodId);

        var message = write.Status switch
        {
            PreferenceOptionWriteStatus.ActorNotAuthorized => "Bu işlem için yetkiniz yok.",
            PreferenceOptionWriteStatus.ExamPeriodNotFound => "Sınav dönemi bulunamadı.",
            PreferenceOptionWriteStatus.OptionNotFound => "Tercih seçeneği bulunamadı.",
            PreferenceOptionWriteStatus.DuplicateName =>
                "Bu tercih adı bu sınav döneminde zaten kullanılıyor.",
            PreferenceOptionWriteStatus.DuplicateDisplayOrder =>
                "Bu görünüm sırası bu sınav döneminde zaten kullanılıyor.",
            PreferenceOptionWriteStatus.InUse =>
                "Bu tercih daha önce başvurularda kullanıldığı için silinemez. Pasif hâle getirebilirsiniz.",
            _ => "Tercih seçeneği işlemi tamamlanamadı."
        };
        return OperationResult<Guid>.Fail(message);
    }

    private static string MapAssignError(ManagerAssignmentWriteStatus status) =>
        status switch
        {
            ManagerAssignmentWriteStatus.ActorNotAuthorized => "Bu işlem için yetkiniz yok.",
            ManagerAssignmentWriteStatus.ExamPeriodNotFound => "Sınav dönemi bulunamadı.",
            ManagerAssignmentWriteStatus.ManagerNotEligible => "Seçilen kullanıcı atanabilir aktif bir Başvuru Yöneticisi değil.",
            ManagerAssignmentWriteStatus.AlreadyAssigned => "Bu yönetici zaten atanmış.",
            ManagerAssignmentWriteStatus.Conflict => "İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.",
            _ => "Yönetici ataması tamamlanamadı."
        };

    private static string MapRemoveError(ManagerAssignmentWriteStatus status) =>
        status switch
        {
            ManagerAssignmentWriteStatus.ActorNotAuthorized => "Bu işlem için yetkiniz yok.",
            ManagerAssignmentWriteStatus.AssignmentNotFound => "Atama bulunamadı.",
            ManagerAssignmentWriteStatus.Conflict => "İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.",
            _ => "Atama kaldırılamadı."
        };
}

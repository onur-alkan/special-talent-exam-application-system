using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Applications;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class CandidateApplicationService : ICandidateApplicationService
{
    private readonly IExamPeriodRepository _examPeriodRepository;
    private readonly IExamPreferenceOptionRepository _optionRepository;
    private readonly ICandidateApplicationRepository _applicationRepository;
    private readonly IAuditService _auditService;

    public CandidateApplicationService(
        IExamPeriodRepository examPeriodRepository,
        IExamPreferenceOptionRepository optionRepository,
        ICandidateApplicationRepository applicationRepository,
        IAuditService auditService)
    {
        _examPeriodRepository = examPeriodRepository;
        _optionRepository = optionRepository;
        _applicationRepository = applicationRepository;
        _auditService = auditService;
    }

    public Task<IReadOnlyList<ExamPeriod>> GetActiveExamPeriodsAsync(CancellationToken cancellationToken = default)
        => _examPeriodRepository.GetActiveForCandidatesAsync(DateTime.Now, cancellationToken);

    public async Task<OperationResult<ApplicationFormData>> GetApplicationFormAsync(Guid examPeriodId, Guid userId, CancellationToken cancellationToken = default)
    {
        var period = await _examPeriodRepository.GetByIdAsync(examPeriodId, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult<ApplicationFormData>.Fail("Sınav dönemi bulunamadı.");

        if (!IsOpenForCandidates(period))
            return OperationResult<ApplicationFormData>.Fail("Bu sınav dönemi başvuruya açık değil.");

        var options = await _optionRepository.GetActiveByExamPeriodAsync(examPeriodId, cancellationToken);

        var existing = await _applicationRepository.GetByUserAndExamAsync(userId, examPeriodId, cancellationToken);
        IReadOnlyList<Guid> existingOptionIds = Array.Empty<Guid>();
        if (existing is not null)
        {
            var prefs = await _applicationRepository.GetSelectedPreferencesAsync(existing.Id, cancellationToken);
            existingOptionIds = prefs.OrderBy(p => p.PreferenceOrder).Select(p => p.PreferenceOptionId).ToList();
        }

        var data = new ApplicationFormData
        {
            ExamPeriod = period,
            Options = options,
            ExistingApplicationId = existing?.Id,
            ExistingSelectedOptionIds = existingOptionIds
        };

        return OperationResult<ApplicationFormData>.Ok(data);
    }

    public async Task<OperationResult<CandidateApplicationCreationResult>> ApplyAsync(
        Guid userId,
        CreateApplicationViewModel model,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        // Erken geri bildirim (nihai doğrulama transaction içindedir).
        var period = await _examPeriodRepository.GetByIdAsync(model.ExamPeriodId, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult<CandidateApplicationCreationResult>.Fail("Sınav dönemi bulunamadı.");

        if (!IsOpenForCandidates(period))
            return OperationResult<CandidateApplicationCreationResult>.Fail("Bu sınav dönemine şu anda başvuru yapılamaz.");

        var existing = await _applicationRepository.GetByUserAndExamAsync(userId, model.ExamPeriodId, cancellationToken);
        if (existing is not null)
            return OperationResult<CandidateApplicationCreationResult>.Fail("Bu sınav dönemine zaten başvurdunuz.");

        var validation = await ValidatePreferencesAsync(period, model.SelectedPreferenceOptionIds, cancellationToken);
        if (!validation.Success)
            return OperationResult<CandidateApplicationCreationResult>.Fail(validation.ErrorMessage!);

        var request = new CandidateApplicationCreationRequest
        {
            UserId = userId,
            ExamPeriodId = model.ExamPeriodId,
            OrderedPreferenceOptionIds = model.SelectedPreferenceOptionIds,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        };

        var write = await _applicationRepository.CreateApplicationAtomicAsync(request, cancellationToken);
        if (!write.Success)
            return OperationResult<CandidateApplicationCreationResult>.Fail(MapCreateError(write.Status));

        return OperationResult<CandidateApplicationCreationResult>.Ok(new CandidateApplicationCreationResult
        {
            ApplicationId = write.ApplicationId!.Value,
            CandidateNo = write.CandidateNo!.Value,
            VerificationCode = write.VerificationCode!
        });
    }

    public async Task<OperationResult> UpdateApplicationAsync(
        Guid userId,
        Guid applicationId,
        CreateApplicationViewModel model,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        // Erken geri bildirim (nihai doğrulama transaction içindedir).
        var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
        if (application is null || application.UserId != userId)
            return OperationResult.Fail("Başvuru bulunamadı veya bu işlem için yetkiniz yok.");

        var period = await _examPeriodRepository.GetByIdAsync(application.ExamPeriodId, cancellationToken);
        if (period is null || period.IsDeleted)
            return OperationResult.Fail("Başvuru güncelleme süresi dışındasınız.");

        if (!IsOpenForCandidates(period))
            return OperationResult.Fail("Başvuru güncelleme süresi dışındasınız.");

        var validation = await ValidatePreferencesAsync(period, model.SelectedPreferenceOptionIds, cancellationToken);
        if (!validation.Success)
            return OperationResult.Fail(validation.ErrorMessage!);

        var write = await _applicationRepository.UpdatePreferencesAtomicAsync(
            applicationId, userId, model.SelectedPreferenceOptionIds, cancellationToken);

        if (!write.Success)
            return OperationResult.Fail(MapUpdateError(write.Status));

        await _auditService.LogAsync(
            "ApplicationUpdated",
            $"Başvuru güncellendi. ApplicationId={applicationId}",
            userId, ipAddress, null, null, cancellationToken);

        return OperationResult.Ok();
    }

    public Task<IReadOnlyList<CandidateApplicationDetail>> GetMyApplicationsAsync(Guid userId, CancellationToken cancellationToken = default)
        => _applicationRepository.GetDetailsByUserAsync(userId, cancellationToken);

    public async Task<OperationResult<CandidateApplicationDetail>> GetMyApplicationDetailAsync(Guid applicationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var detail = await _applicationRepository.GetDetailByIdAsync(applicationId, cancellationToken);
        if (detail is null)
            return OperationResult<CandidateApplicationDetail>.Fail("Başvuru bulunamadı.");
        if (detail.UserId != userId)
            return OperationResult<CandidateApplicationDetail>.Fail("Bu başvuruya erişim yetkiniz yok.");
        return OperationResult<CandidateApplicationDetail>.Ok(detail);
    }

    private static bool IsOpenForCandidates(ExamPeriod period)
    {
        var now = DateTime.Now;
        return period.IsActive
            && !period.IsClosed
            && !period.IsDeleted
            && period.StartDate <= now
            && period.EndDate >= now;
    }

    private async Task<OperationResult> ValidatePreferencesAsync(ExamPeriod period, IReadOnlyList<Guid> selectedIds, CancellationToken cancellationToken)
    {
        if (selectedIds is null || selectedIds.Count == 0)
            return OperationResult.Fail("En az bir tercih seçiniz.");

        if (selectedIds.Any(id => id == Guid.Empty))
            return OperationResult.Fail("Seçtiğiniz tercihlerden biri artık kullanılamıyor. Tercihlerinizi kontrol ediniz.");

        if (selectedIds.Count > period.MaxPreferences)
            return OperationResult.Fail($"En fazla {period.MaxPreferences} tercih seçebilirsiniz.");

        if (selectedIds.Distinct().Count() != selectedIds.Count)
            return OperationResult.Fail("Aynı tercih birden fazla kez seçilemez.");

        var validOptions = await _optionRepository.GetActiveByExamPeriodAsync(period.Id, cancellationToken);
        var validIds = validOptions.Select(o => o.Id).ToHashSet();

        if (selectedIds.Any(id => !validIds.Contains(id)))
            return OperationResult.Fail("Seçtiğiniz tercihlerden biri artık kullanılamıyor. Tercihlerinizi kontrol ediniz.");

        return OperationResult.Ok();
    }

    private static string MapCreateError(ApplicationWriteStatus status) =>
        status switch
        {
            ApplicationWriteStatus.ExamPeriodNotFound => "Sınav dönemi bulunamadı.",
            ApplicationWriteStatus.ExamNotOpen => "Bu sınav dönemine şu anda başvuru yapılamaz.",
            ApplicationWriteStatus.AlreadyApplied => "Bu sınav dönemine zaten başvurdunuz.",
            ApplicationWriteStatus.NoPreferenceSelected => "En az bir tercih seçiniz.",
            ApplicationWriteStatus.PreferenceLimitExceeded => "En fazla izin verilen sayıda tercih seçebilirsiniz.",
            ApplicationWriteStatus.DuplicatePreference => "Aynı tercih birden fazla kez seçilemez.",
            ApplicationWriteStatus.InvalidOrInactivePreference =>
                "Seçtiğiniz tercihlerden biri artık kullanılamıyor. Tercihlerinizi kontrol ediniz.",
            ApplicationWriteStatus.Conflict => "İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.",
            _ => "Başvuru tamamlanamadı. Yeniden deneyiniz."
        };

    private static string MapUpdateError(ApplicationWriteStatus status) =>
        status switch
        {
            ApplicationWriteStatus.ApplicationNotFoundOrUnauthorized =>
                "Başvuru bulunamadı veya bu işlem için yetkiniz yok.",
            ApplicationWriteStatus.ExamNotOpen => "Başvuru güncelleme süresi dışındasınız.",
            ApplicationWriteStatus.NoPreferenceSelected => "En az bir tercih seçiniz.",
            ApplicationWriteStatus.PreferenceLimitExceeded => "En fazla izin verilen sayıda tercih seçebilirsiniz.",
            ApplicationWriteStatus.DuplicatePreference => "Aynı tercih birden fazla kez seçilemez.",
            ApplicationWriteStatus.InvalidOrInactivePreference =>
                "Seçtiğiniz tercihlerden biri artık kullanılamıyor. Tercihlerinizi kontrol ediniz.",
            ApplicationWriteStatus.Conflict => "İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.",
            _ => "Başvuru güncellenemedi."
        };
}

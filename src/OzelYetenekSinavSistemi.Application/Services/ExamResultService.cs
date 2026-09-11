using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.DTOs.ExamResults;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class ExamResultService : IExamResultService
{
    private readonly ICandidateApplicationRepository _applicationRepository;
    private readonly ICandidateExamResultRepository _resultRepository;
    private readonly IExamPeriodManagerRepository _managerRepository;

    public ExamResultService(
        ICandidateApplicationRepository applicationRepository,
        ICandidateExamResultRepository resultRepository,
        IExamPeriodManagerRepository managerRepository)
    {
        _applicationRepository = applicationRepository;
        _resultRepository = resultRepository;
        _managerRepository = managerRepository;
    }

    public async Task<OperationResult<IReadOnlyList<CandidateApplicationDetail>>> GetCandidatesForExamAsync(
        Guid examPeriodId, Guid currentUserId, string role, CancellationToken cancellationToken = default)
    {
        if (!await HasExamAccessAsync(examPeriodId, currentUserId, role, cancellationToken))
            return OperationResult<IReadOnlyList<CandidateApplicationDetail>>.Fail("Bu sınav dönemine erişim yetkiniz yok.");

        var rows = await _applicationRepository.GetDetailsByExamPeriodAsync(examPeriodId, cancellationToken);
        return OperationResult<IReadOnlyList<CandidateApplicationDetail>>.Ok(
            CandidateResultOrdering.OrderByExamScoreDescending(rows));
    }

    public async Task<OperationResult<CandidateApplicationDetail>> GetCandidateForEvaluationAsync(
        Guid applicationId, Guid currentUserId, string role, CancellationToken cancellationToken = default)
    {
        var detail = await _applicationRepository.GetDetailByIdAsync(applicationId, cancellationToken);
        if (detail is null)
            return OperationResult<CandidateApplicationDetail>.Fail("Başvuru bulunamadı.");

        if (!await HasExamAccessAsync(detail.ExamPeriodId, currentUserId, role, cancellationToken))
            return OperationResult<CandidateApplicationDetail>.Fail("Bu adaya erişim yetkiniz yok.");

        return OperationResult<CandidateApplicationDetail>.Ok(detail);
    }

    public async Task<OperationResult> SaveResultAsync(
        ExamResultFormViewModel model,
        Guid evaluatorUserId,
        string role,
        string? ipAddress,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (model.ApplicationId == Guid.Empty)
            return OperationResult.Fail("Başvuru bulunamadı veya bu işlem için yetkiniz yok.");

        if (!Enum.IsDefined(typeof(AttendanceStatus), model.AttendanceStatus))
            return OperationResult.Fail(ExamResultValidationMessages.InvalidAttendanceStatus);

        decimal? score;
        if (model.AttendanceStatus == AttendanceStatus.Attended)
        {
            if (model.ExamScore is null)
                return OperationResult.Fail("Sınava giren aday için puanı giriniz.");
            if (model.ExamScore < DomainConstants.MinExamScore || model.ExamScore > DomainConstants.MaxExamScore)
                return OperationResult.Fail("Sınav puanı 0-100 aralığında olmalıdır.");
            score = model.ExamScore;
        }
        else
        {
            // NotAttended / Cancelled / Disqualified: istemciden gelse bile puan kaydedilmez.
            score = null;
        }

        if (!InputTextRules.IsValidOptionalMeaningfulText(model.AdminDescription, multiline: true, out var description))
            return OperationResult.Fail(InputTextRules.FormatMeaningfulTextMessage("Yönetici Açıklaması"));

        if (description is not null && description.Length > 2000)
            return OperationResult.Fail("Yönetici açıklaması en fazla 2000 karakter olabilir.");

        var isDescriptionVisible = description is not null && model.IsDescriptionVisibleToCandidate;

        // Erken geri bildirim (nihai yetki transaction içindedir).
        var detail = await _applicationRepository.GetDetailByIdAsync(model.ApplicationId, cancellationToken);
        if (detail is null || !await HasExamAccessAsync(detail.ExamPeriodId, evaluatorUserId, role, cancellationToken))
            return OperationResult.Fail("Başvuru bulunamadı veya bu işlem için yetkiniz yok.");

        var request = new ExamResultWriteRequest
        {
            ApplicationId = model.ApplicationId,
            EvaluatorUserId = evaluatorUserId,
            IsSuperAdmin = role == DomainConstants.RoleNames.SuperAdmin,
            AttendanceStatus = model.AttendanceStatus,
            ExamScore = score,
            AdminDescription = description,
            IsDescriptionVisibleToCandidate = isDescriptionVisible,
            ExpectedUpdatedDate = model.ExpectedUpdatedDate,
            IpAddress = ipAddress,
            CorrelationId = correlationId
        };

        var write = await _resultRepository.SaveResultAtomicAsync(request, cancellationToken);
        if (!write.Success)
            return OperationResult.Fail(MapWriteError(write.Status));

        return OperationResult.Ok();
    }

    private static string MapWriteError(ExamResultWriteStatus status) =>
        status switch
        {
            ExamResultWriteStatus.ApplicationNotFoundOrUnauthorized =>
                "Başvuru bulunamadı veya bu işlem için yetkiniz yok.",
            ExamResultWriteStatus.InvalidAttendanceStatus => ExamResultValidationMessages.InvalidAttendanceStatus,
            ExamResultWriteStatus.ScoreRequired => "Sınava giren aday için puanı giriniz.",
            ExamResultWriteStatus.ScoreNotAllowed => ExamResultValidationMessages.ScoreNotAllowed,
            ExamResultWriteStatus.ScoreOutOfRange => "Sınav puanı 0-100 aralığında olmalıdır.",
            ExamResultWriteStatus.DescriptionTooLong => "Yönetici açıklaması en fazla 2000 karakter olabilir.",
            ExamResultWriteStatus.ConcurrencyConflict =>
                "Bu sonuç başka bir kullanıcı tarafından değiştirildi. Güncel verileri kontrol edip tekrar deneyin.",
            ExamResultWriteStatus.Conflict =>
                "İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.",
            _ => "Sonuç kaydedilemedi."
        };

    private async Task<bool> HasExamAccessAsync(Guid examPeriodId, Guid userId, string role, CancellationToken cancellationToken)
    {
        if (role == DomainConstants.RoleNames.SuperAdmin)
            return true;
        if (role == DomainConstants.RoleNames.ApplicationManager)
            return await _managerRepository.IsManagerOfExamPeriodAsync(userId, examPeriodId, cancellationToken);
        return false;
    }
}

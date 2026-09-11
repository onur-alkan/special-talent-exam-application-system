namespace OzelYetenekSinavSistemi.Application.DTOs.Applications;

/// <summary>
/// Aday başvurusu oluşturma / tercih güncelleme atomik yazma sonuç durumu.
/// </summary>
public enum ApplicationWriteStatus
{
    Success = 0,
    ExamPeriodNotFound = 1,
    ExamNotOpen = 2,
    AlreadyApplied = 3,
    ApplicationNotFoundOrUnauthorized = 4,
    NoPreferenceSelected = 5,
    PreferenceLimitExceeded = 6,
    DuplicatePreference = 7,
    InvalidOrInactivePreference = 8,
    Conflict = 9,
    Failed = 10
}

/// <summary>
/// Transaction içi başvuru yazma sonucu. Beklenen iş kuralı hataları exception değildir.
/// </summary>
public sealed class ApplicationWriteResult
{
    public ApplicationWriteStatus Status { get; private init; }
    public Guid? ApplicationId { get; private init; }
    public int? CandidateNo { get; private init; }
    public string? VerificationCode { get; private init; }

    public bool Success => Status == ApplicationWriteStatus.Success;

    public static ApplicationWriteResult Ok(Guid applicationId, int candidateNo, string verificationCode) =>
        new()
        {
            Status = ApplicationWriteStatus.Success,
            ApplicationId = applicationId,
            CandidateNo = candidateNo,
            VerificationCode = verificationCode
        };

    public static ApplicationWriteResult OkUpdated(Guid applicationId) =>
        new()
        {
            Status = ApplicationWriteStatus.Success,
            ApplicationId = applicationId
        };

    public static ApplicationWriteResult Fail(ApplicationWriteStatus status) =>
        new() { Status = status };
}

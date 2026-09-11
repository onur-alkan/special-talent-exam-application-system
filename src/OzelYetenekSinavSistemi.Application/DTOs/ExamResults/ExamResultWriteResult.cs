using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.DTOs.ExamResults;

/// <summary>
/// Sınav sonucu atomik yazma durumları.
/// </summary>
public enum ExamResultWriteStatus
{
    Success = 0,
    ApplicationNotFoundOrUnauthorized = 1,
    InvalidAttendanceStatus = 2,
    ScoreRequired = 3,
    ScoreNotAllowed = 4,
    ScoreOutOfRange = 5,
    DescriptionTooLong = 6,
    ConcurrencyConflict = 7,
    Conflict = 8,
    Failed = 9
}

/// <summary>
/// Transaction içi sınav sonucu yazma sonucu.
/// Beklenen iş kuralı ve eşzamanlılık hataları exception değildir.
/// </summary>
public sealed class ExamResultWriteResult
{
    public ExamResultWriteStatus Status { get; private init; }
    public Guid? ResultId { get; private init; }
    public DateTime? UpdatedDate { get; private init; }

    public bool Success => Status == ExamResultWriteStatus.Success;

    public static ExamResultWriteResult Ok(Guid resultId, DateTime updatedDate) =>
        new()
        {
            Status = ExamResultWriteStatus.Success,
            ResultId = resultId,
            UpdatedDate = updatedDate
        };

    public static ExamResultWriteResult Fail(ExamResultWriteStatus status) =>
        new() { Status = status };
}

/// <summary>
/// Atomik sonuç kaydı için transaction içi istek modeli.
/// </summary>
public sealed class ExamResultWriteRequest
{
    public Guid ApplicationId { get; init; }
    public Guid EvaluatorUserId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public AttendanceStatus AttendanceStatus { get; init; }
    public decimal? ExamScore { get; init; }
    public string? AdminDescription { get; init; }
    public bool IsDescriptionVisibleToCandidate { get; init; }
    public DateTime? ExpectedUpdatedDate { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}

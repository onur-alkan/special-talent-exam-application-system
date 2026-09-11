namespace OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;

public enum PreferenceOptionWriteStatus
{
    Success = 0,
    ActorNotAuthorized = 1,
    ExamPeriodNotFound = 2,
    OptionNotFound = 3,
    DuplicateName = 4,
    DuplicateDisplayOrder = 5,
    InUse = 6,
    Failed = 7
}

public sealed class PreferenceOptionWriteResult
{
    public PreferenceOptionWriteStatus Status { get; private init; }
    public Guid? OptionId { get; private init; }
    public Guid? ExamPeriodId { get; private init; }

    public bool Success => Status == PreferenceOptionWriteStatus.Success;

    public static PreferenceOptionWriteResult Ok(Guid optionId, Guid examPeriodId) =>
        new()
        {
            Status = PreferenceOptionWriteStatus.Success,
            OptionId = optionId,
            ExamPeriodId = examPeriodId
        };

    public static PreferenceOptionWriteResult Fail(
        PreferenceOptionWriteStatus status,
        Guid? examPeriodId = null) =>
        new() { Status = status, ExamPeriodId = examPeriodId };
}

public sealed class PreferenceOptionWriteRequest
{
    public Guid ActorUserId { get; init; }
    public Guid? OptionId { get; init; }
    public Guid? ExamPeriodId { get; init; }
    public string PreferenceName { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}

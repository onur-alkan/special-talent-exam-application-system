namespace OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;

public enum ManagerAssignmentWriteStatus
{
    Success = 0,
    ActorNotAuthorized = 1,
    ExamPeriodNotFound = 2,
    ManagerNotEligible = 3,
    AlreadyAssigned = 4,
    AssignmentNotFound = 5,
    Conflict = 6,
    Failed = 7
}

public sealed class ManagerAssignmentWriteResult
{
    public ManagerAssignmentWriteStatus Status { get; private init; }

    public bool Success => Status == ManagerAssignmentWriteStatus.Success;

    public static ManagerAssignmentWriteResult Ok() =>
        new() { Status = ManagerAssignmentWriteStatus.Success };

    public static ManagerAssignmentWriteResult Fail(ManagerAssignmentWriteStatus status) =>
        new() { Status = status };
}

public sealed class ManagerAssignmentRequest
{
    public Guid ActorUserId { get; init; }
    public Guid ExamPeriodId { get; init; }
    public Guid ManagerUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}

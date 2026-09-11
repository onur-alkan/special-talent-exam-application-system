namespace OzelYetenekSinavSistemi.Application.DTOs.Users;

public enum UserManagementWriteStatus
{
    Success = 0,
    UserNotFound = 1,
    ActorNotAuthorized = 2,
    InvalidRole = 3,
    CandidateAccountCannotBePromoted = 4,
    DuplicateTcNo = 5,
    DuplicateEmail = 6,
    CannotModifyOwnAccount = 7,
    LastActiveSuperAdmin = 8,
    Conflict = 9,
    Failed = 10
}

public sealed class UserManagementWriteResult
{
    public UserManagementWriteStatus Status { get; private init; }
    public Guid? UserId { get; private init; }

    public bool Success => Status == UserManagementWriteStatus.Success;

    public static UserManagementWriteResult Ok(Guid userId) =>
        new() { Status = UserManagementWriteStatus.Success, UserId = userId };

    public static UserManagementWriteResult Fail(UserManagementWriteStatus status) =>
        new() { Status = status };
}

public sealed class CreateStaffUserRequest
{
    public Guid ActorUserId { get; init; }
    public string TcNo { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public Guid RoleId { get; init; }
    public string PasswordHash { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public Guid SecurityStamp { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed class UpdateStaffUserRequest
{
    public Guid ActorUserId { get; init; }
    public Guid TargetUserId { get; init; }
    public Guid NewRoleId { get; init; }
    public bool IsActive { get; init; }
    public string? IpAddress { get; init; }
    public string? CorrelationId { get; init; }
}

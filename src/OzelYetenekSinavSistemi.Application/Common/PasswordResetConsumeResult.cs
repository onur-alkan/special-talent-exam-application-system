namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Parola sıfırlama token tüketimi + parola güncellemesinin atomik sonucu.
/// </summary>
public enum PasswordResetConsumeStatus
{
    Succeeded = 0,
    TokenNotFound = 1,
    TokenAlreadyUsed = 2,
    TokenExpired = 3,
    UserUpdateFailed = 4,
    TokenUpdateFailed = 5
}

public sealed class PasswordResetConsumeResult
{
    public PasswordResetConsumeStatus Status { get; private init; }
    public Guid? UserId { get; private init; }

    public bool Success => Status == PasswordResetConsumeStatus.Succeeded;

    public static PasswordResetConsumeResult Succeeded(Guid userId) =>
        new() { Status = PasswordResetConsumeStatus.Succeeded, UserId = userId };

    public static PasswordResetConsumeResult Failed(PasswordResetConsumeStatus status) =>
        new() { Status = status };
}

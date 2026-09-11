namespace OzelYetenekSinavSistemi.Application.DTOs.Users;

/// <summary>
/// Cookie oturum doğrulaması için hafif kullanıcı durumu.
/// </summary>
public sealed class UserAuthenticationState
{
    public Guid UserId { get; init; }
    public Guid RoleId { get; init; }
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public Guid SecurityStamp { get; init; }
}

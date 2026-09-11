using OzelYetenekSinavSistemi.Application.DTOs.Users;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailOrTurkishIdentityAsync(string value, CancellationToken cancellationToken = default);

    Task<bool> IdentityExistsAsync(
        IdentityDocumentType type,
        string normalizedIdentityNumber,
        string? issuingCountryCode,
        CancellationToken cancellationToken = default);

    /// <summary>Geçiş dönemi uyumluluğu — yeni <see cref="GetByEmailOrTurkishIdentityAsync"/> metodunu kullanın.</summary>
    Task<User?> GetByTcNoAsync(string tcNo, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Geçiş dönemi uyumluluğu — yeni <see cref="GetByEmailOrTurkishIdentityAsync"/> metodunu kullanın.</summary>
    Task<User?> GetByTcNoOrEmailAsync(string tcNoOrEmail, CancellationToken cancellationToken = default);

    /// <summary>Geçiş dönemi uyumluluğu — yeni <see cref="IdentityExistsAsync"/> metodunu kullanın.</summary>
    Task<bool> TcNoExistsAsync(string tcNo, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<UserAuthenticationState?> GetAuthenticationStateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default);
    Task<bool> UpdateLoginStateAsync(Guid userId, int failedLoginCount, DateTime? lockoutEnd, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetStaffUsersAsync(int take, int skip, CancellationToken cancellationToken = default);
    Task<UserManagementWriteResult> CreateStaffUserAtomicAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default);
    Task<UserManagementWriteResult> UpdateStaffUserAtomicAsync(UpdateStaffUserRequest request, CancellationToken cancellationToken = default);
}

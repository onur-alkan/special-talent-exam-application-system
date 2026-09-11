using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Users;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : GenericRepository<User>, IUserRepository
{
    protected override string TableName => "dbo.Users";

    private const string SelectColumns = @"Id, RoleId, TcNo, IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber,
        NationalityCountryCode, IssuingCountryCode, PassportExpiryDate, PasswordHash, Email, FirstName, LastName, BirthYear,
        BirthDate, Nationality, HighSchool, DepartmentField, YgsScore, YgsYearId, Address, Phone, HasDisability, DisabilityDetails,
        PhotoPath, IsActive, FailedLoginCount, LockoutEnd, MustChangePassword, SecurityStamp, CreatedDate, UpdatedDate";

    private const string TcUniqueConstraint = "UQ_Users_TcNo";
    private const string TcFilteredUniqueIndex = "UQ_Users_TcNo_NotNull";
    private const string EmailUniqueConstraint = "UQ_Users_Email";
    private const string TurkishIdentityUniqueIndex = "UQ_Users_Identity_Turkish";
    private const string ForeignIdentityUniqueIndex = "UQ_Users_Identity_Foreign";
    private const string PassportIdentityUniqueIndex = "UQ_Users_Identity_Passport";

    private const byte TurkishIdentityType = (byte)IdentityDocumentType.TurkishIdentityNumber;
    private const byte ForeignIdentityType = (byte)IdentityDocumentType.ForeignIdentityNumber;
    private const byte PassportIdentityType = (byte)IdentityDocumentType.Passport;

    public UserRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public override async Task<Guid> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (IsUniqueViolation(ex, EmailUniqueConstraint))
        {
            throw new DuplicateUserRegistrationException(DuplicateUserRegistrationKind.Email);
        }
        catch (SqlException ex) when (IsIdentityUniqueViolation(ex))
        {
            throw new DuplicateUserRegistrationException(DuplicateUserRegistrationKind.Identity);
        }
    }

    public async Task<User?> GetByEmailOrTurkishIdentityAsync(string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (LoginIdentifierHelper.LooksLikeEmail(trimmed))
            return await GetByEmailAsync(trimmed, cancellationToken).ConfigureAwait(false);

        return await GetByTurkishIdentityNumberAsync(trimmed, cancellationToken).ConfigureAwait(false);
    }

    public Task<User?> GetByTcNoAsync(string tcNo, CancellationToken cancellationToken = default)
        => GetByTurkishIdentityNumberAsync(tcNo.Trim(), cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM dbo.Users
WHERE Email = @Email;";

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<User>(
            new CommandDefinition(sql, new { Email = email.Trim() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public Task<User?> GetByTcNoOrEmailAsync(string tcNoOrEmail, CancellationToken cancellationToken = default)
        => GetByEmailOrTurkishIdentityAsync(tcNoOrEmail, cancellationToken);

    public Task<bool> TcNoExistsAsync(string tcNo, CancellationToken cancellationToken = default)
        => IdentityExistsAsync(IdentityDocumentType.TurkishIdentityNumber, tcNo.Trim(), null, cancellationToken);

    public async Task<bool> IdentityExistsAsync(
        IdentityDocumentType type,
        string normalizedIdentityNumber,
        string? issuingCountryCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (type == IdentityDocumentType.Passport)
        {
            const string passportSql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Users
    WHERE IdentityDocumentType = @Type
      AND NormalizedIdentityNumber = @Normalized
      AND IssuingCountryCode = @Issuing
) THEN 1 ELSE 0 END;";

            return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(passportSql, new
                {
                    Type = PassportIdentityType,
                    Normalized = normalizedIdentityNumber,
                    Issuing = issuingCountryCode
                }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        if (type == IdentityDocumentType.TurkishIdentityNumber)
        {
            const string turkishSql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Users
    WHERE (IdentityDocumentType = @Type AND NormalizedIdentityNumber = @Normalized)
       OR TcNo = @Normalized
) THEN 1 ELSE 0 END;";

            return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(turkishSql, new
                {
                    Type = TurkishIdentityType,
                    Normalized = normalizedIdentityNumber
                }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        const string foreignSql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Users
    WHERE IdentityDocumentType = @Type
      AND NormalizedIdentityNumber = @Normalized
) THEN 1 ELSE 0 END;";

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(foreignSql, new
            {
                Type = ForeignIdentityType,
                Normalized = normalizedIdentityNumber
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email) THEN 1 ELSE 0 END;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Email = email.Trim() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<User>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.Users WHERE RoleId = @RoleId ORDER BY FirstName, LastName;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<User>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<User>> GetActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.Users WHERE RoleId = @RoleId AND IsActive = 1 ORDER BY FirstName, LastName;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<User>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<UserAuthenticationState?> GetAuthenticationStateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT Id AS UserId, RoleId, IsActive, MustChangePassword, SecurityStamp
FROM dbo.Users
WHERE Id = @UserId;";

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<UserAuthenticationState>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdatePasswordAsync(Guid userId, string passwordHash, bool mustChangePassword, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE dbo.Users
            SET PasswordHash = @PasswordHash,
                MustChangePassword = @MustChangePassword,
                SecurityStamp = NEWID(),
                UpdatedDate = SYSDATETIME()
            WHERE Id = @UserId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, PasswordHash = passwordHash, MustChangePassword = mustChangePassword }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> UpdateLoginStateAsync(Guid userId, int failedLoginCount, DateTime? lockoutEnd, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE dbo.Users
            SET FailedLoginCount = @FailedLoginCount, LockoutEnd = @LockoutEnd
            WHERE Id = @UserId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, FailedLoginCount = failedLoginCount, LockoutEnd = lockoutEnd }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<IReadOnlyList<User>> GetStaffUsersAsync(int take, int skip, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var sql = $@"
SELECT {SelectColumns}
FROM dbo.Users
WHERE RoleId IN @RoleIds
ORDER BY CreatedDate DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<User>(
            new CommandDefinition(sql, new
            {
                RoleIds = new[] { DomainConstants.RoleIds.SuperAdmin, DomainConstants.RoleIds.ApplicationManager },
                Skip = skip,
                Take = take
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<UserManagementWriteResult> CreateStaffUserAtomicAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!DomainConstants.IsStaffRoleId(request.RoleId) || request.RoleId == Guid.Empty)
            return UserManagementWriteResult.Fail(UserManagementWriteStatus.InvalidRole);

        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await IsActiveSuperAdminAsync(connection, transaction, request.ActorUserId, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.ActorNotAuthorized);
            }

            const string insertSql = @"
INSERT INTO dbo.Users
    (RoleId, TcNo, IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber, NationalityCountryCode,
     PasswordHash, Email, FirstName, LastName, Nationality, IsActive, MustChangePassword, SecurityStamp, CreatedDate)
OUTPUT INSERTED.Id
VALUES
    (@RoleId, @TcNo, @TurkishIdentityType, @TcNo, @TcNo, 'TR',
     @PasswordHash, @Email, @FirstName, @LastName, N'T.C.', @IsActive, 1, @SecurityStamp, SYSDATETIME());";

            Guid newId;
            try
            {
                newId = await connection.ExecuteScalarAsync<Guid>(
                    new CommandDefinition(insertSql, new
                    {
                        request.RoleId,
                        request.TcNo,
                        TurkishIdentityType,
                        request.PasswordHash,
                        request.Email,
                        request.FirstName,
                        request.LastName,
                        request.IsActive,
                        request.SecurityStamp
                    }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (IsUniqueViolation(ex, TcUniqueConstraint) || IsUniqueViolation(ex, TcFilteredUniqueIndex) || IsUniqueViolation(ex, TurkishIdentityUniqueIndex))
            {
                await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.DuplicateTcNo);
            }
            catch (SqlException ex) when (IsUniqueViolation(ex, EmailUniqueConstraint))
            {
                await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.DuplicateEmail);
            }

            if (newId == Guid.Empty)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.Failed);
            }

            var roleName = DomainConstants.GetRoleName(request.RoleId) ?? "Unknown";
            var auditOk = await InsertAuditAsync(
                connection, transaction, request.ActorUserId, "StaffUserCreated",
                $"UserId={newId}; Role={roleName}", request.IpAddress, request.CorrelationId, cancellationToken)
                .ConfigureAwait(false);

            if (!auditOk)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return UserManagementWriteResult.Ok(newId);
        }
        catch (SqlException ex) when (IsUniqueViolation(ex, TcUniqueConstraint) || IsUniqueViolation(ex, TcFilteredUniqueIndex) || IsUniqueViolation(ex, TurkishIdentityUniqueIndex))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return UserManagementWriteResult.Fail(UserManagementWriteStatus.DuplicateTcNo);
        }
        catch (SqlException ex) when (IsUniqueViolation(ex, EmailUniqueConstraint))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return UserManagementWriteResult.Fail(UserManagementWriteStatus.DuplicateEmail);
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<UserManagementWriteResult> UpdateStaffUserAtomicAsync(
        UpdateStaffUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!DomainConstants.IsStaffRoleId(request.NewRoleId) || request.NewRoleId == Guid.Empty)
            return UserManagementWriteResult.Fail(UserManagementWriteStatus.InvalidRole);

        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            const string lockActor = @"
SELECT Id, RoleId, IsActive
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ActorUserId;";

            var actor = await connection.QueryFirstOrDefaultAsync<UserLockRow>(
                new CommandDefinition(lockActor, new { request.ActorUserId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (actor is null || !actor.IsActive || actor.RoleId != DomainConstants.RoleIds.SuperAdmin)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.ActorNotAuthorized);
            }

            const string lockTarget = @"
SELECT Id, RoleId, IsActive
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @TargetUserId;";

            var target = await connection.QueryFirstOrDefaultAsync<UserLockRow>(
                new CommandDefinition(lockTarget, new { request.TargetUserId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (target is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.UserNotFound);
            }

            if (target.RoleId == DomainConstants.RoleIds.Candidate)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.CandidateAccountCannotBePromoted);
            }

            if (request.TargetUserId == request.ActorUserId)
            {
                if (!request.IsActive || request.NewRoleId != DomainConstants.RoleIds.SuperAdmin)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return UserManagementWriteResult.Fail(UserManagementWriteStatus.CannotModifyOwnAccount);
                }
            }

            var demotingOrDeactivatingSuperAdmin =
                target.RoleId == DomainConstants.RoleIds.SuperAdmin
                && target.IsActive
                && (!request.IsActive || request.NewRoleId != DomainConstants.RoleIds.SuperAdmin);

            if (demotingOrDeactivatingSuperAdmin)
            {
                const string countSql = @"
SELECT COUNT(1)
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE RoleId = @SuperAdminRoleId
  AND IsActive = 1;";

                var activeSuperAdmins = await connection.ExecuteScalarAsync<int>(
                    new CommandDefinition(countSql, new { SuperAdminRoleId = DomainConstants.RoleIds.SuperAdmin },
                        transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (activeSuperAdmins <= 1)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return UserManagementWriteResult.Fail(UserManagementWriteStatus.LastActiveSuperAdmin);
                }
            }

            var roleChanged = target.RoleId != request.NewRoleId;
            var activeChanged = target.IsActive != request.IsActive;

            const string updateSql = @"
UPDATE dbo.Users
SET RoleId = @NewRoleId,
    IsActive = @IsActive,
    SecurityStamp = NEWID(),
    UpdatedDate = SYSDATETIME()
WHERE Id = @TargetUserId;";

            var updated = await connection.ExecuteAsync(
                new CommandDefinition(updateSql, new
                {
                    request.TargetUserId,
                    request.NewRoleId,
                    request.IsActive
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return UserManagementWriteResult.Fail(UserManagementWriteStatus.Failed);
            }

            if (target.RoleId == DomainConstants.RoleIds.ApplicationManager
                && (request.NewRoleId != DomainConstants.RoleIds.ApplicationManager || !request.IsActive))
            {
                const string deleteAssignments = @"
DELETE FROM dbo.ExamPeriodManagers
WHERE ManagerUserId = @TargetUserId;";

                await connection.ExecuteAsync(
                    new CommandDefinition(deleteAssignments, new { request.TargetUserId }, transaction, cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }

            if (roleChanged)
            {
                var roleName = DomainConstants.GetRoleName(request.NewRoleId) ?? "Unknown";
                if (!await InsertAuditAsync(connection, transaction, request.ActorUserId, "StaffUserRoleChanged",
                        $"UserId={request.TargetUserId}; Role={roleName}", request.IpAddress, request.CorrelationId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return UserManagementWriteResult.Fail(UserManagementWriteStatus.Failed);
                }
            }

            if (activeChanged)
            {
                var eventType = request.IsActive ? "StaffUserActivated" : "StaffUserDeactivated";
                if (!await InsertAuditAsync(connection, transaction, request.ActorUserId, eventType,
                        $"UserId={request.TargetUserId}", request.IpAddress, request.CorrelationId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return UserManagementWriteResult.Fail(UserManagementWriteStatus.Failed);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return UserManagementWriteResult.Ok(request.TargetUserId);
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<User?> GetByTurkishIdentityNumberAsync(string tcNo, CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT TOP (1) {SelectColumns}
FROM dbo.Users
WHERE TcNo = @TcNo
   OR (IdentityDocumentType = @TurkishType AND NormalizedIdentityNumber = @TcNo)
ORDER BY CASE WHEN TcNo = @TcNo THEN 0 ELSE 1 END, Id;";

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<User>(
            new CommandDefinition(sql, new { TcNo = tcNo, TurkishType = TurkishIdentityType }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static bool IsIdentityUniqueViolation(SqlException ex) =>
        IsUniqueViolation(ex, TcUniqueConstraint)
        || IsUniqueViolation(ex, TcFilteredUniqueIndex)
        || IsUniqueViolation(ex, TurkishIdentityUniqueIndex)
        || IsUniqueViolation(ex, ForeignIdentityUniqueIndex)
        || IsUniqueViolation(ex, PassportIdentityUniqueIndex);

    private static async Task<bool> IsActiveSuperAdminAsync(
        SqlConnection connection, SqlTransaction transaction, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT 1
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @UserId AND IsActive = 1 AND RoleId = @RoleId;";

        var result = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { UserId = userId, RoleId = DomainConstants.RoleIds.SuperAdmin },
                transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result is not null;
    }

    private static async Task<bool> InsertAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid? userId,
        string eventType,
        string description,
        string? ipAddress,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.AuditLogs (UserId, EventType, [Description], IpAddress, CorrelationId)
VALUES (@UserId, @EventType, @Description, @IpAddress, @CorrelationId);";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                UserId = userId,
                EventType = eventType,
                Description = description,
                IpAddress = ipAddress,
                CorrelationId = correlationId
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

        return affected == 1;
    }

    internal static bool IsUniqueViolation(SqlException ex, string constraintName) =>
        (ex.Number is 2627 or 2601)
        && ex.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase);

    private static async Task SafeRollbackAsync(SqlTransaction transaction, CancellationToken cancellationToken)
    {
        try { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); }
        catch { /* ignore */ }
    }

    private sealed class UserLockRow
    {
        public Guid Id { get; init; }
        public Guid RoleId { get; init; }
        public bool IsActive { get; init; }
    }
}

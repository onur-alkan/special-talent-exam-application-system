using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class ExamPeriodManagerRepository : GenericRepository<ExamPeriodManager>, IExamPeriodManagerRepository
{
    protected override string TableName => "dbo.ExamPeriodManagers";

    private const string SelectColumns = "Id, ExamPeriodId, ManagerUserId, AssignedByUserId, AssignedDate";
    private const string UniqueConstraint = "UQ_ExamPeriodManagers";

    public ExamPeriodManagerRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<IReadOnlyList<ExamPeriodManager>> GetByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.ExamPeriodManagers WHERE ExamPeriodId = @ExamPeriodId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExamPeriodManager>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> IsManagerOfExamPeriodAsync(Guid managerUserId, Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM dbo.ExamPeriodManagers m
    INNER JOIN dbo.Users u ON u.Id = m.ManagerUserId
    INNER JOIN dbo.ExamPeriods ep ON ep.Id = m.ExamPeriodId
    WHERE m.ManagerUserId = @ManagerUserId
      AND m.ExamPeriodId = @ExamPeriodId
      AND u.IsActive = 1
      AND u.RoleId = @ApplicationManagerRoleId
      AND ep.IsDeleted = 0
) THEN 1 ELSE 0 END;";

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new
            {
                ManagerUserId = managerUserId,
                ExamPeriodId = examPeriodId,
                ApplicationManagerRoleId = DomainConstants.RoleIds.ApplicationManager
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ManagerAssignmentWriteResult> AssignManagerAtomicAsync(
        ManagerAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            const string lockActor = @"
SELECT 1
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ActorUserId AND IsActive = 1 AND RoleId = @SuperAdminRoleId;";

            var actorOk = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(lockActor, new
                {
                    request.ActorUserId,
                    SuperAdminRoleId = DomainConstants.RoleIds.SuperAdmin
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (actorOk is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.ActorNotAuthorized);
            }

            const string lockExam = @"
SELECT Id
FROM dbo.ExamPeriods WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ExamPeriodId AND IsDeleted = 0;";

            var examId = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(lockExam, new { request.ExamPeriodId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (examId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.ExamPeriodNotFound);
            }

            const string lockManager = @"
SELECT Id
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ManagerUserId
  AND IsActive = 1
  AND RoleId = @ApplicationManagerRoleId;";

            var managerId = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(lockManager, new
                {
                    request.ManagerUserId,
                    ApplicationManagerRoleId = DomainConstants.RoleIds.ApplicationManager
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (managerId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.ManagerNotEligible);
            }

            const string lockExisting = @"
SELECT Id
FROM dbo.ExamPeriodManagers WITH (UPDLOCK, HOLDLOCK)
WHERE ExamPeriodId = @ExamPeriodId
  AND ManagerUserId = @ManagerUserId;";

            var existing = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(lockExisting, new { request.ExamPeriodId, request.ManagerUserId },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (existing is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.AlreadyAssigned);
            }

            const string insertSql = @"
INSERT INTO dbo.ExamPeriodManagers (ExamPeriodId, ManagerUserId, AssignedByUserId, AssignedDate)
VALUES (@ExamPeriodId, @ManagerUserId, @ActorUserId, SYSDATETIME());";

            int inserted;
            try
            {
                inserted = await connection.ExecuteAsync(
                    new CommandDefinition(insertSql, new
                    {
                        request.ExamPeriodId,
                        request.ManagerUserId,
                        request.ActorUserId
                    }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (IsAssignmentUniqueViolation(ex))
            {
                await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.AlreadyAssigned);
            }

            if (inserted != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.Failed);
            }

            const string auditSql = @"
INSERT INTO dbo.AuditLogs (UserId, EventType, [Description], IpAddress, CorrelationId)
VALUES (@UserId, @EventType, @Description, @IpAddress, @CorrelationId);";

            var auditAffected = await connection.ExecuteAsync(
                new CommandDefinition(auditSql, new
                {
                    UserId = request.ActorUserId,
                    EventType = "ManagerAssigned",
                    Description = $"ExamPeriodId={request.ExamPeriodId}; ManagerUserId={request.ManagerUserId}",
                    request.IpAddress,
                    request.CorrelationId
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (auditAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ManagerAssignmentWriteResult.Ok();
        }
        catch (SqlException ex) when (IsAssignmentUniqueViolation(ex))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.AlreadyAssigned);
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ManagerAssignmentWriteResult> RemoveManagerAtomicAsync(
        ManagerAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            const string lockActor = @"
SELECT 1
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ActorUserId AND IsActive = 1 AND RoleId = @SuperAdminRoleId;";

            var actorOk = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(lockActor, new
                {
                    request.ActorUserId,
                    SuperAdminRoleId = DomainConstants.RoleIds.SuperAdmin
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (actorOk is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.ActorNotAuthorized);
            }

            const string lockAssignment = @"
SELECT Id
FROM dbo.ExamPeriodManagers WITH (UPDLOCK, HOLDLOCK)
WHERE ExamPeriodId = @ExamPeriodId
  AND ManagerUserId = @ManagerUserId;";

            var assignmentId = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(lockAssignment, new { request.ExamPeriodId, request.ManagerUserId },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (assignmentId is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.AssignmentNotFound);
            }

            const string deleteSql = @"
DELETE FROM dbo.ExamPeriodManagers
WHERE ExamPeriodId = @ExamPeriodId AND ManagerUserId = @ManagerUserId;";

            var deleted = await connection.ExecuteAsync(
                new CommandDefinition(deleteSql, new { request.ExamPeriodId, request.ManagerUserId },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (deleted != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.Failed);
            }

            const string auditSql = @"
INSERT INTO dbo.AuditLogs (UserId, EventType, [Description], IpAddress, CorrelationId)
VALUES (@UserId, @EventType, @Description, @IpAddress, @CorrelationId);";

            var auditAffected = await connection.ExecuteAsync(
                new CommandDefinition(auditSql, new
                {
                    UserId = request.ActorUserId,
                    EventType = "ManagerAssignmentRemoved",
                    Description = $"ExamPeriodId={request.ExamPeriodId}; ManagerUserId={request.ManagerUserId}",
                    request.IpAddress,
                    request.CorrelationId
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (auditAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ManagerAssignmentWriteResult.Fail(ManagerAssignmentWriteStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ManagerAssignmentWriteResult.Ok();
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<bool> RemoveAssignmentAsync(Guid examPeriodId, Guid managerUserId, CancellationToken cancellationToken = default)
    {
        // Geriye dönük; yeni kod RemoveManagerAtomicAsync kullanır.
        const string sql = "DELETE FROM dbo.ExamPeriodManagers WHERE ExamPeriodId = @ExamPeriodId AND ManagerUserId = @ManagerUserId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId, ManagerUserId = managerUserId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    internal static bool IsAssignmentUniqueViolation(SqlException ex) =>
        (ex.Number is 2627 or 2601)
        && ex.Message.Contains(UniqueConstraint, StringComparison.OrdinalIgnoreCase);

    private static async Task SafeRollbackAsync(SqlTransaction transaction, CancellationToken cancellationToken)
    {
        try { await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false); }
        catch { /* ignore */ }
    }
}

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.DTOs.ExamResults;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class CandidateExamResultRepository : GenericRepository<CandidateExamResult>, ICandidateExamResultRepository
{
    protected override string TableName => "dbo.CandidateExamResults";

    private const string SelectColumns = @"Id, ApplicationId, AttendanceStatus, ExamScore, AdminDescription,
        IsDescriptionVisibleToCandidate, EvaluatedByUserId, UpdatedDate";

    private const string ApplicationIdUniqueConstraint = "UQ_CandidateExamResults_ApplicationId";

    public CandidateExamResultRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<CandidateExamResult?> GetByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.CandidateExamResults WHERE ApplicationId = @ApplicationId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<CandidateExamResult>(
            new CommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ExamResultWriteResult> SaveResultAtomicAsync(
        ExamResultWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidatePayload(request);
        if (validation != ExamResultWriteStatus.Success)
            return ExamResultWriteResult.Fail(validation);

        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            const string lockApplication = @"
SELECT Id, ExamPeriodId
FROM dbo.CandidateApplications WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ApplicationId;";

            var application = await connection.QueryFirstOrDefaultAsync<ApplicationLockRow>(
                new CommandDefinition(lockApplication, new { request.ApplicationId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (application is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ExamResultWriteResult.Fail(ExamResultWriteStatus.ApplicationNotFoundOrUnauthorized);
            }

            if (!request.IsSuperAdmin)
            {
                const string lockAssignment = @"
SELECT 1
FROM dbo.ExamPeriodManagers m WITH (UPDLOCK, HOLDLOCK)
INNER JOIN dbo.Users u WITH (UPDLOCK, HOLDLOCK) ON u.Id = m.ManagerUserId
INNER JOIN dbo.ExamPeriods ep WITH (UPDLOCK, HOLDLOCK) ON ep.Id = m.ExamPeriodId
WHERE m.ExamPeriodId = @ExamPeriodId
  AND m.ManagerUserId = @EvaluatorUserId
  AND u.IsActive = 1
  AND u.RoleId = @ApplicationManagerRoleId
  AND ep.IsDeleted = 0;";

                var assigned = await connection.ExecuteScalarAsync<int?>(
                    new CommandDefinition(lockAssignment, new
                    {
                        application.ExamPeriodId,
                        request.EvaluatorUserId,
                        ApplicationManagerRoleId = DomainConstants.RoleIds.ApplicationManager
                    }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

                if (assigned is null)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ExamResultWriteResult.Fail(ExamResultWriteStatus.ApplicationNotFoundOrUnauthorized);
                }
            }

            const string lockResult = @"
SELECT Id, UpdatedDate
FROM dbo.CandidateExamResults WITH (UPDLOCK, HOLDLOCK)
WHERE ApplicationId = @ApplicationId;";

            var existing = await connection.QueryFirstOrDefaultAsync<ResultLockRow>(
                new CommandDefinition(lockResult, new { request.ApplicationId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var concurrency = EvaluateConcurrency(existing, request.ExpectedUpdatedDate);
            if (concurrency != ExamResultWriteStatus.Success)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ExamResultWriteResult.Fail(concurrency);
            }

            ResultOutputRow? written;
            var isInsert = existing is null;

            if (isInsert)
            {
                written = await InsertResultAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                written = await UpdateResultAsync(connection, transaction, existing!, request, cancellationToken).ConfigureAwait(false);
            }

            if (written is null || written.Id == Guid.Empty || written.UpdatedDate is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ExamResultWriteResult.Fail(ExamResultWriteStatus.ConcurrencyConflict);
            }

            const string insertAudit = @"
INSERT INTO dbo.AuditLogs (UserId, EventType, [Description], IpAddress, CorrelationId)
VALUES (@UserId, @EventType, @Description, @IpAddress, @CorrelationId);";

            var operation = isInsert ? "Create" : "Update";
            var auditAffected = await connection.ExecuteAsync(
                new CommandDefinition(insertAudit, new
                {
                    UserId = request.EvaluatorUserId,
                    EventType = "ExamResultSaved",
                    Description = $"ApplicationId={request.ApplicationId}; AttendanceStatus={request.AttendanceStatus}; Operation={operation}",
                    request.IpAddress,
                    request.CorrelationId
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (auditAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ExamResultWriteResult.Fail(ExamResultWriteStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ExamResultWriteResult.Ok(written.Id, written.UpdatedDate.Value);
        }
        catch (SqlException ex) when (IsApplicationIdUniqueViolation(ex))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return ExamResultWriteResult.Fail(ExamResultWriteStatus.ConcurrencyConflict);
        }
        catch (SqlException ex) when (IsUniqueConstraintViolation(ex.Number))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return ExamResultWriteResult.Fail(ExamResultWriteStatus.Conflict);
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static ExamResultWriteStatus ValidatePayload(ExamResultWriteRequest request)
    {
        if (request.ApplicationId == Guid.Empty)
            return ExamResultWriteStatus.ApplicationNotFoundOrUnauthorized;

        if (!Enum.IsDefined(typeof(AttendanceStatus), request.AttendanceStatus))
            return ExamResultWriteStatus.InvalidAttendanceStatus;

        if (request.AttendanceStatus == AttendanceStatus.Attended)
        {
            if (request.ExamScore is null)
                return ExamResultWriteStatus.ScoreRequired;
            if (request.ExamScore < DomainConstants.MinExamScore || request.ExamScore > DomainConstants.MaxExamScore)
                return ExamResultWriteStatus.ScoreOutOfRange;
        }
        else if (request.ExamScore is not null)
        {
            return ExamResultWriteStatus.ScoreNotAllowed;
        }

        if (request.AdminDescription is not null && request.AdminDescription.Length > 2000)
            return ExamResultWriteStatus.DescriptionTooLong;

        return ExamResultWriteStatus.Success;
    }

    private static ExamResultWriteStatus EvaluateConcurrency(ResultLockRow? existing, DateTime? expectedUpdatedDate)
    {
        if (existing is null && expectedUpdatedDate is null)
            return ExamResultWriteStatus.Success;

        if (existing is null || expectedUpdatedDate is null)
            return ExamResultWriteStatus.ConcurrencyConflict;

        if (!UpdatedDatesEqual(existing.UpdatedDate, expectedUpdatedDate))
            return ExamResultWriteStatus.ConcurrencyConflict;

        return ExamResultWriteStatus.Success;
    }

    private static bool UpdatedDatesEqual(DateTime? left, DateTime? right)
    {
        if (left is null || right is null)
            return false;

        // DATETIME2(3) hassasiyetine hizala
        var a = TruncateToMilliseconds(left.Value);
        var b = TruncateToMilliseconds(right.Value);
        return a == b;
    }

    private static DateTime TruncateToMilliseconds(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Unspecified);

    private static async Task<ResultOutputRow?> InsertResultAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ExamResultWriteRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.CandidateExamResults
    (ApplicationId, AttendanceStatus, ExamScore, AdminDescription, IsDescriptionVisibleToCandidate, EvaluatedByUserId, UpdatedDate)
OUTPUT INSERTED.Id, INSERTED.UpdatedDate
VALUES
    (@ApplicationId, @AttendanceStatus, @ExamScore, @AdminDescription, @IsDescriptionVisibleToCandidate, @EvaluatorUserId, SYSDATETIME());";

        try
        {
            return await connection.QuerySingleOrDefaultAsync<ResultOutputRow>(
                new CommandDefinition(sql, new
                {
                    request.ApplicationId,
                    AttendanceStatus = (int)request.AttendanceStatus,
                    request.ExamScore,
                    request.AdminDescription,
                    request.IsDescriptionVisibleToCandidate,
                    request.EvaluatorUserId
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (SqlException ex) when (IsApplicationIdUniqueViolation(ex))
        {
            throw;
        }
    }

    private static async Task<ResultOutputRow?> UpdateResultAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ResultLockRow existing,
        ExamResultWriteRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE dbo.CandidateExamResults
SET AttendanceStatus = @AttendanceStatus,
    ExamScore = @ExamScore,
    AdminDescription = @AdminDescription,
    IsDescriptionVisibleToCandidate = @IsDescriptionVisibleToCandidate,
    EvaluatedByUserId = @EvaluatorUserId,
    UpdatedDate = SYSDATETIME()
OUTPUT INSERTED.Id, INSERTED.UpdatedDate
WHERE Id = @Id
  AND UpdatedDate = @ExpectedUpdatedDate;";

        return await connection.QuerySingleOrDefaultAsync<ResultOutputRow>(
            new CommandDefinition(sql, new
            {
                existing.Id,
                AttendanceStatus = (int)request.AttendanceStatus,
                request.ExamScore,
                request.AdminDescription,
                request.IsDescriptionVisibleToCandidate,
                request.EvaluatorUserId,
                ExpectedUpdatedDate = TruncateToMilliseconds(request.ExpectedUpdatedDate!.Value)
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    internal static bool IsApplicationIdUniqueViolation(SqlException ex) =>
        IsApplicationIdUniqueViolation(ex.Number, ex.Message);

    internal static bool IsApplicationIdUniqueViolation(int number, string message) =>
        IsUniqueConstraintViolation(number)
        && !string.IsNullOrEmpty(message)
        && message.Contains(ApplicationIdUniqueConstraint, StringComparison.OrdinalIgnoreCase);

    internal static bool IsUniqueConstraintViolation(int number) =>
        number is 2627 or 2601;

    private static async Task SafeRollbackAsync(SqlTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Transaction zaten tamamlanmış olabilir.
        }
    }

    private sealed class ApplicationLockRow
    {
        public Guid Id { get; init; }
        public Guid ExamPeriodId { get; init; }
    }

    private sealed class ResultLockRow
    {
        public Guid Id { get; init; }
        public DateTime? UpdatedDate { get; init; }
    }

    private sealed class ResultOutputRow
    {
        public Guid Id { get; init; }
        public DateTime? UpdatedDate { get; init; }
    }
}

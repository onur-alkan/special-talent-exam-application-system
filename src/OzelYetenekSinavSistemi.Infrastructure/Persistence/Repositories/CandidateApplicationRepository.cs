using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class CandidateApplicationRepository : GenericRepository<CandidateApplication>, ICandidateApplicationRepository
{
    protected override string TableName => "dbo.CandidateApplications";

    private const string SelectColumns = "Id, UserId, ExamPeriodId, CandidateNo, RegistrationDate, UpdatedDate, VerificationCode, [Status]";
    private const string UserExamUniqueConstraint = "UQ_CandidateApplications_User_Exam";

    private readonly ICandidateNumberService _candidateNumberService;

    public CandidateApplicationRepository(
        IDbConnectionFactory connectionFactory,
        ICandidateNumberService candidateNumberService)
        : base(connectionFactory)
    {
        _candidateNumberService = candidateNumberService;
    }

    public async Task<ApplicationWriteResult> CreateApplicationAtomicAsync(
        CandidateApplicationCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            var exam = await LockExamPeriodAsync(connection, transaction, request.ExamPeriodId, cancellationToken)
                .ConfigureAwait(false);
            if (exam is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.ExamPeriodNotFound);
            }

            if (!IsExamOpen(exam))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.ExamNotOpen);
            }

            var existingId = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    @"SELECT Id
                      FROM dbo.CandidateApplications WITH (UPDLOCK, HOLDLOCK)
                      WHERE UserId = @UserId AND ExamPeriodId = @ExamPeriodId;",
                    new { request.UserId, request.ExamPeriodId },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (existingId is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.AlreadyApplied);
            }

            var preferenceStatus = await ValidateAndLockPreferencesAsync(
                connection, transaction, request.ExamPeriodId, exam.MaxPreferences, request.OrderedPreferenceOptionIds, cancellationToken)
                .ConfigureAwait(false);
            if (preferenceStatus != ApplicationWriteStatus.Success)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(preferenceStatus);
            }

            var candidateNo = await _candidateNumberService
                .GenerateNextAsync(connection, transaction, request.ExamPeriodId, cancellationToken)
                .ConfigureAwait(false);

            var verificationCode = Guid.NewGuid().ToString("N").ToUpperInvariant();

            const string insertApplication = @"
INSERT INTO dbo.CandidateApplications (UserId, ExamPeriodId, CandidateNo, VerificationCode, [Status])
OUTPUT INSERTED.Id
VALUES (@UserId, @ExamPeriodId, @CandidateNo, @VerificationCode, @Status);";

            Guid applicationId;
            try
            {
                applicationId = await connection.ExecuteScalarAsync<Guid>(
                    new CommandDefinition(insertApplication, new
                    {
                        request.UserId,
                        request.ExamPeriodId,
                        CandidateNo = candidateNo,
                        VerificationCode = verificationCode,
                        Status = (int)ApplicationStatus.Submitted
                    }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (IsUserExamUniqueViolation(ex))
            {
                await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.AlreadyApplied);
            }
            catch (SqlException ex) when (IsUniqueConstraintViolation(ex))
            {
                await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Conflict);
            }

            if (applicationId == Guid.Empty)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Failed);
            }

            var prefsInserted = await InsertPreferencesAsync(
                connection, transaction, applicationId, request.OrderedPreferenceOptionIds, cancellationToken)
                .ConfigureAwait(false);
            if (!prefsInserted)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Failed);
            }

            const string insertVerification = @"
INSERT INTO dbo.DocumentVerifications (ApplicationId, VerificationCode, IsValid)
VALUES (@ApplicationId, @VerificationCode, 1);";

            var verificationAffected = await connection.ExecuteAsync(
                new CommandDefinition(insertVerification, new
                {
                    ApplicationId = applicationId,
                    VerificationCode = verificationCode
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (verificationAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Failed);
            }

            const string insertAudit = @"
INSERT INTO dbo.AuditLogs (UserId, EventType, [Description], IpAddress, CorrelationId)
VALUES (@UserId, @EventType, @Description, @IpAddress, @CorrelationId);";

            var auditAffected = await connection.ExecuteAsync(
                new CommandDefinition(insertAudit, new
                {
                    request.UserId,
                    EventType = "ApplicationCreated",
                    Description = $"Başvuru oluşturuldu. ExamPeriodId={request.ExamPeriodId}, CandidateNo={candidateNo}",
                    IpAddress = request.IpAddress,
                    CorrelationId = request.CorrelationId
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (auditAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ApplicationWriteResult.Ok(applicationId, candidateNo, verificationCode);
        }
        catch (SqlException ex) when (IsUserExamUniqueViolation(ex))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return ApplicationWriteResult.Fail(ApplicationWriteStatus.AlreadyApplied);
        }
        catch (SqlException ex) when (IsUniqueConstraintViolation(ex))
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return ApplicationWriteResult.Fail(ApplicationWriteStatus.Conflict);
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ApplicationWriteResult> UpdatePreferencesAtomicAsync(
        Guid applicationId,
        Guid userId,
        IReadOnlyList<Guid> orderedPreferenceOptionIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqlConnection)await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            const string lockApplication = @"
SELECT Id, UserId, ExamPeriodId
FROM dbo.CandidateApplications WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ApplicationId;";

            var application = await connection.QueryFirstOrDefaultAsync<ApplicationLockRow>(
                new CommandDefinition(lockApplication, new { ApplicationId = applicationId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            if (application is null || application.UserId != userId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.ApplicationNotFoundOrUnauthorized);
            }

            var exam = await LockExamPeriodAsync(connection, transaction, application.ExamPeriodId, cancellationToken)
                .ConfigureAwait(false);
            if (exam is null || !IsExamOpen(exam))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.ExamNotOpen);
            }

            var preferenceStatus = await ValidateAndLockPreferencesAsync(
                connection, transaction, application.ExamPeriodId, exam.MaxPreferences, orderedPreferenceOptionIds, cancellationToken)
                .ConfigureAwait(false);
            if (preferenceStatus != ApplicationWriteStatus.Success)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(preferenceStatus);
            }

            const string deleteExisting = "DELETE FROM dbo.CandidateSelectedPreferences WHERE ApplicationId = @ApplicationId;";
            await connection.ExecuteAsync(
                new CommandDefinition(deleteExisting, new { ApplicationId = applicationId }, transaction, cancellationToken: cancellationToken))
                .ConfigureAwait(false);

            var prefsInserted = await InsertPreferencesAsync(
                connection, transaction, applicationId, orderedPreferenceOptionIds, cancellationToken)
                .ConfigureAwait(false);
            if (!prefsInserted)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Failed);
            }

            const string updateApp = @"
UPDATE dbo.CandidateApplications
SET UpdatedDate = SYSDATETIME(), [Status] = @Status
WHERE Id = @ApplicationId AND UserId = @UserId;";

            var updated = await connection.ExecuteAsync(
                new CommandDefinition(updateApp, new
                {
                    ApplicationId = applicationId,
                    UserId = userId,
                    Status = (int)ApplicationStatus.Updated
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (updated != 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ApplicationWriteResult.Fail(ApplicationWriteStatus.Failed);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ApplicationWriteResult.OkUpdated(applicationId);
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<CandidateApplication?> GetByUserAndExamAsync(Guid userId, Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.CandidateApplications WHERE UserId = @UserId AND ExamPeriodId = @ExamPeriodId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<CandidateApplication>(
            new CommandDefinition(sql, new { UserId = userId, ExamPeriodId = examPeriodId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<CandidateApplicationDetail?> GetDetailByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var sql = DetailSelect + " WHERE ca.Id = @ApplicationId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var detail = await connection.QueryFirstOrDefaultAsync<CandidateApplicationDetail>(
            new CommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (detail is not null)
            detail.Preferences = await GetPreferenceNamesAsync(connection, applicationId, cancellationToken).ConfigureAwait(false);

        return detail;
    }

    public async Task<IReadOnlyList<CandidateApplicationDetail>> GetDetailsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sql = DetailSelect + " WHERE ca.UserId = @UserId ORDER BY ca.RegistrationDate DESC;";
        return await LoadDetailsAsync(sql, new { UserId = userId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CandidateApplicationDetail>> GetDetailsByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        var sql = DetailSelect + " WHERE ca.ExamPeriodId = @ExamPeriodId ORDER BY ca.CandidateNo ASC;";
        return await LoadDetailsAsync(sql, new { ExamPeriodId = examPeriodId }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CandidateSelectedPreference>> GetSelectedPreferencesAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT Id, ApplicationId, PreferenceOptionId, PreferenceOrder
            FROM dbo.CandidateSelectedPreferences WHERE ApplicationId = @ApplicationId ORDER BY PreferenceOrder;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CandidateSelectedPreference>(
            new CommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static async Task<ExamPeriodLockRow?> LockExamPeriodAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid examPeriodId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT Id, MaxPreferences, IsActive, IsClosed, IsDeleted, StartDate, EndDate,
       SYSDATETIME() AS ServerNow
FROM dbo.ExamPeriods WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ExamPeriodId;";

        return await connection.QueryFirstOrDefaultAsync<ExamPeriodLockRow>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId }, transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static bool IsExamOpen(ExamPeriodLockRow exam) =>
        !exam.IsDeleted
        && exam.IsActive
        && !exam.IsClosed
        && exam.MaxPreferences >= 1
        && exam.StartDate <= exam.ServerNow
        && exam.EndDate >= exam.ServerNow;

    private static async Task<ApplicationWriteStatus> ValidateAndLockPreferencesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid examPeriodId,
        int maxPreferences,
        IReadOnlyList<Guid> orderedPreferenceOptionIds,
        CancellationToken cancellationToken)
    {
        if (orderedPreferenceOptionIds is null || orderedPreferenceOptionIds.Count == 0)
            return ApplicationWriteStatus.NoPreferenceSelected;

        if (orderedPreferenceOptionIds.Any(id => id == Guid.Empty))
            return ApplicationWriteStatus.InvalidOrInactivePreference;

        if (orderedPreferenceOptionIds.Count > maxPreferences)
            return ApplicationWriteStatus.PreferenceLimitExceeded;

        if (orderedPreferenceOptionIds.Distinct().Count() != orderedPreferenceOptionIds.Count)
            return ApplicationWriteStatus.DuplicatePreference;

        const string sql = @"
SELECT Id
FROM dbo.ExamPreferenceOptions WITH (UPDLOCK, HOLDLOCK)
WHERE ExamPeriodId = @ExamPeriodId
  AND IsActive = 1
  AND Id IN @PreferenceIds;";

        var found = (await connection.QueryAsync<Guid>(
            new CommandDefinition(sql, new
            {
                ExamPeriodId = examPeriodId,
                PreferenceIds = orderedPreferenceOptionIds.Distinct().ToArray()
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (found.Count != orderedPreferenceOptionIds.Distinct().Count())
            return ApplicationWriteStatus.InvalidOrInactivePreference;

        return ApplicationWriteStatus.Success;
    }

    private static async Task<bool> InsertPreferencesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid applicationId,
        IReadOnlyList<Guid> orderedPreferenceOptionIds,
        CancellationToken cancellationToken)
    {
        const string insertPreference = @"
INSERT INTO dbo.CandidateSelectedPreferences (ApplicationId, PreferenceOptionId, PreferenceOrder)
VALUES (@ApplicationId, @PreferenceOptionId, @PreferenceOrder);";

        var order = 1;
        foreach (var optionId in orderedPreferenceOptionIds)
        {
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(insertPreference, new
                {
                    ApplicationId = applicationId,
                    PreferenceOptionId = optionId,
                    PreferenceOrder = order
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (affected != 1)
                return false;

            order++;
        }

        return true;
    }

    internal static bool IsUserExamUniqueViolation(SqlException ex) =>
        IsUserExamUniqueViolation(ex.Number, ex.Message);

    internal static bool IsUniqueConstraintViolation(SqlException ex) =>
        IsUniqueConstraintViolation(ex.Number);

    /// <summary>
    /// UQ_CandidateApplications_User_Exam ihlali (SQL 2627/2601 + constraint adı).
    /// </summary>
    internal static bool IsUserExamUniqueViolation(int number, string message) =>
        IsUniqueConstraintViolation(number)
        && !string.IsNullOrEmpty(message)
        && message.Contains(UserExamUniqueConstraint, StringComparison.OrdinalIgnoreCase);

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

    private async Task<IReadOnlyList<CandidateApplicationDetail>> LoadDetailsAsync(string sql, object param, CancellationToken cancellationToken)
    {
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var details = (await connection.QueryAsync<CandidateApplicationDetail>(
            new CommandDefinition(sql, param, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (details.Count == 0)
            return details;

        var ids = details.Select(d => d.ApplicationId).ToArray();
        const string prefSql = @"SELECT csp.ApplicationId, epo.PreferenceName, csp.PreferenceOrder
            FROM dbo.CandidateSelectedPreferences csp
            INNER JOIN dbo.ExamPreferenceOptions epo ON epo.Id = csp.PreferenceOptionId
            WHERE csp.ApplicationId IN @Ids
            ORDER BY csp.PreferenceOrder;";

        var prefRows = await connection.QueryAsync(
            new CommandDefinition(prefSql, new { Ids = ids }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        var lookup = prefRows
            .GroupBy(r => (Guid)r.ApplicationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => (string)x.PreferenceName).ToList());

        foreach (var detail in details)
        {
            if (lookup.TryGetValue(detail.ApplicationId, out var prefs))
                detail.Preferences = prefs;
        }

        return details;
    }

    private static async Task<IReadOnlyList<string>> GetPreferenceNamesAsync(IDbConnection connection, Guid applicationId, CancellationToken cancellationToken)
    {
        const string prefSql = @"SELECT epo.PreferenceName
            FROM dbo.CandidateSelectedPreferences csp
            INNER JOIN dbo.ExamPreferenceOptions epo ON epo.Id = csp.PreferenceOptionId
            WHERE csp.ApplicationId = @ApplicationId
            ORDER BY csp.PreferenceOrder;";
        var names = await connection.QueryAsync<string>(
            new CommandDefinition(prefSql, new { ApplicationId = applicationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return names.ToList();
    }

    private sealed class ExamPeriodLockRow
    {
        public Guid Id { get; init; }
        public int MaxPreferences { get; init; }
        public bool IsActive { get; init; }
        public bool IsClosed { get; init; }
        public bool IsDeleted { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public DateTime ServerNow { get; init; }
    }

    private sealed class ApplicationLockRow
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public Guid ExamPeriodId { get; init; }
    }

    private const string DetailSelect = @"
SELECT ca.Id AS ApplicationId, ca.UserId, ca.ExamPeriodId, ca.CandidateNo, ca.VerificationCode,
       ca.RegistrationDate, ca.[Status],
       u.TcNo, u.IdentityDocumentType, u.IdentityNumber, u.NationalityCountryCode,
       u.IssuingCountryCode, u.PassportExpiryDate,
       u.FirstName, u.LastName, u.BirthYear, u.BirthDate, u.Email, u.Phone, u.PhotoPath,
       ep.Title AS ExamTitle,
       ep.EndDate AS ExamPeriodEndDate,
       CASE WHEN ep.IsActive = 1 AND ep.IsClosed = 0 AND ep.IsDeleted = 0
                 AND ep.StartDate <= SYSDATETIME() AND ep.EndDate >= SYSDATETIME()
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS CanEdit,
       CASE WHEN SYSDATETIME() >= ep.EndDate
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS CanAccessExamEntranceDocument,
       r.AttendanceStatus, r.ExamScore, r.AdminDescription, r.IsDescriptionVisibleToCandidate,
       r.UpdatedDate AS EvaluatedDate
FROM dbo.CandidateApplications ca
INNER JOIN dbo.Users u ON u.Id = ca.UserId
INNER JOIN dbo.ExamPeriods ep ON ep.Id = ca.ExamPeriodId
LEFT JOIN dbo.CandidateExamResults r ON r.ApplicationId = ca.Id";
}

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class ExamPreferenceOptionRepository : GenericRepository<ExamPreferenceOption>, IExamPreferenceOptionRepository
{
    protected override string TableName => "dbo.ExamPreferenceOptions";

    private const string SelectColumns = "Id, ExamPeriodId, PreferenceName, DisplayOrder, IsActive";
    private const string NameConstraint = "UQ_ExamPreferenceOptions_Period_Name";
    private const string OrderConstraint = "UQ_ExamPreferenceOptions_Period_Order";

    public ExamPreferenceOptionRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<IReadOnlyList<ExamPreferenceOption>> GetByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.ExamPreferenceOptions WHERE ExamPeriodId = @ExamPeriodId ORDER BY DisplayOrder, PreferenceName;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExamPreferenceOption>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ExamPreferenceOption>> GetActiveByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.ExamPreferenceOptions WHERE ExamPeriodId = @ExamPeriodId AND IsActive = 1 ORDER BY DisplayOrder;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExamPreferenceOption>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<int> GetMaxDisplayOrderAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT ISNULL(MAX(DisplayOrder), 0)
FROM dbo.ExamPreferenceOptions
WHERE ExamPeriodId = @ExamPeriodId;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<PreferenceOptionPagedResult> SearchForDataTablesAsync(
        PreferenceOptionDataTablesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var start = Math.Max(0, query.Start);
        var length = NormalizePageLength(query.Length);
        var orderBy = ResolveOrderByClause(query.OrderColumnIndex, query.OrderDirection);
        var search = string.IsNullOrWhiteSpace(query.SearchValue)
            ? null
            : query.SearchValue.Trim();
        var searchPattern = search is null ? null : $"%{EscapeLikePattern(search)}%";

        const string totalSql = @"SELECT COUNT_BIG(1)
FROM dbo.ExamPreferenceOptions
WHERE ExamPeriodId = @ExamPeriodId;";

        const string filteredSql = @"SELECT COUNT_BIG(1)
FROM dbo.ExamPreferenceOptions
WHERE ExamPeriodId = @ExamPeriodId
  AND (@SearchPattern IS NULL OR PreferenceName LIKE @SearchPattern ESCAPE '\');";

        // ORDER BY whitelist ResolveOrderByClause ile sabitlenir; parametre değildir.
        var pageSql = $@"SELECT Id, PreferenceName, DisplayOrder, IsActive
FROM dbo.ExamPreferenceOptions
WHERE ExamPeriodId = @ExamPeriodId
  AND (@SearchPattern IS NULL OR PreferenceName LIKE @SearchPattern ESCAPE '\')
ORDER BY {orderBy}
OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;";

        var args = new
        {
            ExamPeriodId = query.ExamPeriodId,
            SearchPattern = searchPattern,
            Start = start,
            Length = length
        };

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var recordsTotal = (int)await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(totalSql, new { query.ExamPeriodId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var recordsFiltered = (int)await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(filteredSql, args, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var rows = (await connection.QueryAsync<PreferenceOptionTableRowDto>(
            new CommandDefinition(pageSql, args, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToList();

        return new PreferenceOptionPagedResult
        {
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsFiltered,
            Rows = rows
        };
    }

    private static int NormalizePageLength(int length)
    {
        return length switch
        {
            10 or 25 or 50 or 100 => length,
            _ => 10
        };
    }

    private static string ResolveOrderByClause(int columnIndex, string? direction)
    {
        var dir = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        var primary = columnIndex switch
        {
            1 => "PreferenceName",
            2 => "IsActive",
            _ => "DisplayOrder"
        };
        return $"{primary} {dir}, Id ASC";
    }

    private static string EscapeLikePattern(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);

    public async Task<PreferenceOptionWriteResult> AddAtomicAsync(
        PreferenceOptionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExamPeriodId is not { } examPeriodId)
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.ExamPeriodNotFound);

        using var connection = (SqlConnection)await ConnectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await LockAuthorizedActorAsync(connection, transaction, request.ActorUserId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.ActorNotAuthorized, null, cancellationToken);

            if (!await LockExamPeriodAsync(connection, transaction, examPeriodId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.ExamPeriodNotFound, null, cancellationToken);

            var duplicate = await FindDuplicateAsync(
                connection, transaction, examPeriodId, null,
                request.PreferenceName, request.DisplayOrder, cancellationToken);
            if (duplicate is not null)
                return await RollbackAndFailAsync(transaction, duplicate.Value, examPeriodId, cancellationToken);

            const string insertSql = @"
INSERT INTO dbo.ExamPreferenceOptions (ExamPeriodId, PreferenceName, DisplayOrder, IsActive)
OUTPUT INSERTED.Id
VALUES (@ExamPeriodId, @PreferenceName, @DisplayOrder, @IsActive);";

            Guid optionId;
            try
            {
                optionId = await connection.ExecuteScalarAsync<Guid>(
                    new CommandDefinition(insertSql, new
                    {
                        ExamPeriodId = examPeriodId,
                        request.PreferenceName,
                        request.DisplayOrder,
                        request.IsActive
                    }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (MapUniqueViolation(ex) is { } status)
            {
                return await RollbackAndFailAsync(transaction, status, examPeriodId, cancellationToken);
            }

            if (!await InsertAuditAsync(
                    connection, transaction, request, "PreferenceOptionCreated",
                    optionId, examPeriodId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, examPeriodId, cancellationToken);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Ok(optionId, examPeriodId);
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.Failed, examPeriodId);
        }
    }

    public async Task<PreferenceOptionWriteResult> UpdateAtomicAsync(
        PreferenceOptionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OptionId is not { } optionId)
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.OptionNotFound);

        using var connection = (SqlConnection)await ConnectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await LockAuthorizedActorAsync(connection, transaction, request.ActorUserId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.ActorNotAuthorized, null, cancellationToken);

            var option = await LockOptionAsync(connection, transaction, optionId, cancellationToken);
            if (option is null)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.OptionNotFound, null, cancellationToken);

            var duplicate = await FindDuplicateAsync(
                connection, transaction, option.ExamPeriodId, optionId,
                request.PreferenceName, request.DisplayOrder, cancellationToken);
            if (duplicate is not null)
                return await RollbackAndFailAsync(transaction, duplicate.Value, option.ExamPeriodId, cancellationToken);

            const string updateSql = @"
UPDATE dbo.ExamPreferenceOptions
SET PreferenceName = @PreferenceName,
    DisplayOrder = @DisplayOrder,
    IsActive = @IsActive
WHERE Id = @OptionId;";

            int affected;
            try
            {
                affected = await connection.ExecuteAsync(
                    new CommandDefinition(updateSql, new
                    {
                        OptionId = optionId,
                        request.PreferenceName,
                        request.DisplayOrder,
                        request.IsActive
                    }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (MapUniqueViolation(ex) is { } status)
            {
                return await RollbackAndFailAsync(transaction, status, option.ExamPeriodId, cancellationToken);
            }

            if (affected != 1)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, option.ExamPeriodId, cancellationToken);

            if (!await InsertAuditAsync(
                    connection, transaction, request, "PreferenceOptionUpdated",
                    optionId, option.ExamPeriodId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, option.ExamPeriodId, cancellationToken);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Ok(optionId, option.ExamPeriodId);
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.Failed);
        }
    }

    public async Task<PreferenceOptionWriteResult> SetActiveAtomicAsync(
        PreferenceOptionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OptionId is not { } optionId)
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.OptionNotFound);

        using var connection = (SqlConnection)await ConnectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await LockAuthorizedActorAsync(connection, transaction, request.ActorUserId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.ActorNotAuthorized, null, cancellationToken);

            var option = await LockOptionAsync(connection, transaction, optionId, cancellationToken);
            if (option is null)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.OptionNotFound, null, cancellationToken);

            const string updateSql = @"
UPDATE dbo.ExamPreferenceOptions
SET IsActive = @IsActive
WHERE Id = @OptionId;";
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(updateSql, new
                {
                    OptionId = optionId,
                    request.IsActive
                }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (affected != 1)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, option.ExamPeriodId, cancellationToken);

            var eventType = request.IsActive
                ? "PreferenceOptionActivated"
                : "PreferenceOptionDeactivated";
            if (!await InsertAuditAsync(
                    connection, transaction, request, eventType,
                    optionId, option.ExamPeriodId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, option.ExamPeriodId, cancellationToken);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Ok(optionId, option.ExamPeriodId);
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.Failed);
        }
    }

    public async Task<PreferenceOptionWriteResult> DeleteAtomicAsync(
        PreferenceOptionWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OptionId is not { } optionId)
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.OptionNotFound);

        using var connection = (SqlConnection)await ConnectionFactory
            .CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        try
        {
            if (!await LockAuthorizedActorAsync(connection, transaction, request.ActorUserId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.ActorNotAuthorized, null, cancellationToken);

            var option = await LockOptionAsync(connection, transaction, optionId, cancellationToken);
            if (option is null)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.OptionNotFound, null, cancellationToken);

            const string usageSql = @"
SELECT TOP (1) 1
FROM dbo.CandidateSelectedPreferences WITH (UPDLOCK, HOLDLOCK)
WHERE PreferenceOptionId = @OptionId;";
            var isUsed = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(usageSql, new { OptionId = optionId },
                    transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (isUsed is not null)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.InUse, option.ExamPeriodId, cancellationToken);

            int affected;
            try
            {
                const string deleteSql = "DELETE FROM dbo.ExamPreferenceOptions WHERE Id = @OptionId;";
                affected = await connection.ExecuteAsync(
                    new CommandDefinition(deleteSql, new { OptionId = optionId },
                        transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.InUse, option.ExamPeriodId, cancellationToken);
            }

            if (affected != 1)
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, option.ExamPeriodId, cancellationToken);

            if (!await InsertAuditAsync(
                    connection, transaction, request, "PreferenceOptionDeleted",
                    optionId, option.ExamPeriodId, cancellationToken))
                return await RollbackAndFailAsync(transaction, PreferenceOptionWriteStatus.Failed, option.ExamPeriodId, cancellationToken);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Ok(optionId, option.ExamPeriodId);
        }
        catch (OperationCanceledException)
        {
            await SafeRollbackAsync(transaction, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
            return PreferenceOptionWriteResult.Fail(PreferenceOptionWriteStatus.Failed);
        }
    }

    private static async Task<bool> LockAuthorizedActorAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT 1
FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ActorUserId
  AND IsActive = 1
  AND RoleId = @SuperAdminRoleId;";
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new
            {
                ActorUserId = actorUserId,
                SuperAdminRoleId = DomainConstants.RoleIds.SuperAdmin
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false) is not null;
    }

    private static async Task<bool> LockExamPeriodAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid examPeriodId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT 1
FROM dbo.ExamPeriods WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @ExamPeriodId AND IsDeleted = 0;";
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { ExamPeriodId = examPeriodId },
                transaction, cancellationToken: cancellationToken)).ConfigureAwait(false) is not null;
    }

    private static Task<LockedOption?> LockOptionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid optionId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT Id, ExamPeriodId
FROM dbo.ExamPreferenceOptions WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @OptionId;";
        return connection.QuerySingleOrDefaultAsync<LockedOption>(
            new CommandDefinition(sql, new { OptionId = optionId },
                transaction, cancellationToken: cancellationToken));
    }

    private static async Task<PreferenceOptionWriteStatus?> FindDuplicateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid examPeriodId,
        Guid? excludedOptionId,
        string preferenceName,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        const string nameSql = @"
SELECT TOP (1) 1
FROM dbo.ExamPreferenceOptions WITH (UPDLOCK, HOLDLOCK)
WHERE ExamPeriodId = @ExamPeriodId
  AND PreferenceName = @PreferenceName
  AND (@ExcludedOptionId IS NULL OR Id <> @ExcludedOptionId);";
        var duplicateName = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(nameSql, new
            {
                ExamPeriodId = examPeriodId,
                PreferenceName = preferenceName,
                ExcludedOptionId = excludedOptionId
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (duplicateName is not null)
            return PreferenceOptionWriteStatus.DuplicateName;

        const string orderSql = @"
SELECT TOP (1) 1
FROM dbo.ExamPreferenceOptions WITH (UPDLOCK, HOLDLOCK)
WHERE ExamPeriodId = @ExamPeriodId
  AND DisplayOrder = @DisplayOrder
  AND (@ExcludedOptionId IS NULL OR Id <> @ExcludedOptionId);";
        var duplicateOrder = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(orderSql, new
            {
                ExamPeriodId = examPeriodId,
                DisplayOrder = displayOrder,
                ExcludedOptionId = excludedOptionId
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return duplicateOrder is null
            ? null
            : PreferenceOptionWriteStatus.DuplicateDisplayOrder;
    }

    private static async Task<bool> InsertAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        PreferenceOptionWriteRequest request,
        string eventType,
        Guid optionId,
        Guid examPeriodId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO dbo.AuditLogs (UserId, EventType, [Description], IpAddress, CorrelationId)
VALUES (@UserId, @EventType, @Description, @IpAddress, @CorrelationId);";
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                UserId = request.ActorUserId,
                EventType = eventType,
                Description = $"ExamPeriodId={examPeriodId}; PreferenceOptionId={optionId}",
                request.IpAddress,
                request.CorrelationId
            }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected == 1;
    }

    private static PreferenceOptionWriteStatus? MapUniqueViolation(SqlException exception)
    {
        if (exception.Number is not (2601 or 2627))
            return null;
        if (exception.Message.Contains(NameConstraint, StringComparison.OrdinalIgnoreCase))
            return PreferenceOptionWriteStatus.DuplicateName;
        if (exception.Message.Contains(OrderConstraint, StringComparison.OrdinalIgnoreCase))
            return PreferenceOptionWriteStatus.DuplicateDisplayOrder;
        return PreferenceOptionWriteStatus.Failed;
    }

    private static async Task<PreferenceOptionWriteResult> RollbackAndFailAsync(
        SqlTransaction transaction,
        PreferenceOptionWriteStatus status,
        Guid? examPeriodId,
        CancellationToken cancellationToken)
    {
        await SafeRollbackAsync(transaction, cancellationToken).ConfigureAwait(false);
        return PreferenceOptionWriteResult.Fail(status, examPeriodId);
    }

    private static async Task SafeRollbackAsync(
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // İlk hata korunur; rollback hatası dışarı sızdırılmaz.
        }
    }

    private sealed class LockedOption
    {
        public Guid Id { get; init; }
        public Guid ExamPeriodId { get; init; }
    }
}

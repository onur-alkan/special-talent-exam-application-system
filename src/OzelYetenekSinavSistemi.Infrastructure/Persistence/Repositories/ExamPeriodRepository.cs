using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class ExamPeriodRepository : GenericRepository<ExamPeriod>, IExamPeriodRepository
{
    protected override string TableName => "dbo.ExamPeriods";

    private const string SelectColumns = @"Id, Title, [Description], StartDate, EndDate, MaxPreferences,
        IsActive, IsClosed, IsDeleted, CreatedByUserId, CreatedDate, UpdatedDate";

    public ExamPeriodRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<IReadOnlyList<ExamPeriod>> GetAllNotDeletedAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.ExamPeriods WHERE IsDeleted = 0 ORDER BY CreatedDate DESC;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExamPeriod>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ExamPeriod>> GetActiveForCandidatesAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var sql = $@"SELECT {SelectColumns} FROM dbo.ExamPeriods
            WHERE IsActive = 1 AND IsClosed = 0 AND IsDeleted = 0
              AND StartDate <= @Now AND EndDate >= @Now
            ORDER BY EndDate ASC;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExamPeriod>(
            new CommandDefinition(sql, new { Now = now }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ExamPeriod>> GetByManagerAsync(Guid managerUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT ep.Id, ep.Title, ep.[Description], ep.StartDate, ep.EndDate, ep.MaxPreferences,
                       ep.IsActive, ep.IsClosed, ep.IsDeleted, ep.CreatedByUserId, ep.CreatedDate, ep.UpdatedDate
                FROM dbo.ExamPeriods ep
                INNER JOIN dbo.ExamPeriodManagers m ON m.ExamPeriodId = ep.Id
                INNER JOIN dbo.Users u ON u.Id = m.ManagerUserId
                WHERE m.ManagerUserId = @ManagerUserId
                  AND ep.IsDeleted = 0
                  AND u.IsActive = 1
                  AND u.RoleId = @ApplicationManagerRoleId
                ORDER BY ep.CreatedDate DESC;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ExamPeriod>(
            new CommandDefinition(sql, new
            {
                ManagerUserId = managerUserId,
                ApplicationManagerRoleId = DomainConstants.RoleIds.ApplicationManager
            }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.ExamPeriods SET IsDeleted = 1, IsActive = 0, UpdatedDate = SYSDATETIME() WHERE Id = @Id;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> SetClosedAsync(Guid id, bool isClosed, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.ExamPeriods SET IsClosed = @IsClosed, UpdatedDate = SYSDATETIME() WHERE Id = @Id;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, IsClosed = isClosed }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.ExamPeriods SET IsActive = @IsActive, UpdatedDate = SYSDATETIME() WHERE Id = @Id;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, IsActive = isActive }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }
}

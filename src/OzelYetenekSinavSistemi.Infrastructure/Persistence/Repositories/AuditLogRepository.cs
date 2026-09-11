using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
{
    public const int MinTake = 1;
    public const int MaxTake = 200;

    protected override string TableName => "dbo.AuditLogs";

    public AuditLogRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, int skip = 0, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, MinTake, MaxTake);
        skip = Math.Max(0, skip);

        const string sql = @"
SELECT Id, UserId, EventType, [Description], IpAddress, RequestPath, CorrelationId, CreatedDate
FROM dbo.AuditLogs
ORDER BY CreatedDate DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<AuditLog>(
            new CommandDefinition(sql, new { Take = take, Skip = skip }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }
}

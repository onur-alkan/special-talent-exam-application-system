using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class YgsYearRepository : GenericRepository<YgsYear>, IYgsYearRepository
{
    protected override string TableName => "dbo.YgsYears";

    public YgsYearRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<IReadOnlyList<YgsYear>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id, [Year], IsActive, CreatedDate FROM dbo.YgsYears WHERE IsActive = 1 ORDER BY [Year] DESC;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<YgsYear>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<bool> YearExistsAsync(int year, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.YgsYears WHERE [Year] = @Year) THEN 1 ELSE 0 END;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Year = year }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

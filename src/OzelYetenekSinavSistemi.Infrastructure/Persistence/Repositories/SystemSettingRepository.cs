using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class SystemSettingRepository : GenericRepository<SystemSetting>, ISystemSettingRepository
{
    protected override string TableName => "dbo.SystemSettings";

    private const string SelectColumns = "Id, SettingKey, SettingValue, [Description], UpdatedDate, UpdatedByUserId";

    public SystemSettingRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.SystemSettings WHERE SettingKey = @Key;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<SystemSetting>(
            new CommandDefinition(sql, new { Key = key }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> UpdateValueAsync(string key, string? value, Guid? updatedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE dbo.SystemSettings
SET SettingValue = @Value, UpdatedDate = SYSDATETIME(), UpdatedByUserId = @UpdatedByUserId
WHERE SettingKey = @Key;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Key = key, Value = value, UpdatedByUserId = updatedByUserId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }
}

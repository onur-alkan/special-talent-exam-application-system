using Dapper;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository : GenericRepository<PasswordResetToken>, IPasswordResetTokenRepository
{
    protected override string TableName => "dbo.PasswordResetTokens";

    private const string SelectColumns = "Id, UserId, TokenHash, ExpiresAt, UsedAt, CreatedDate, CreatedIpAddress";

    public PasswordResetTokenRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.PasswordResetTokens WHERE TokenHash = @TokenHash;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<PasswordResetToken>(
            new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<bool> MarkAsUsedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.PasswordResetTokens SET UsedAt = SYSDATETIME() WHERE Id = @Id AND UsedAt IS NULL;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return affected > 0;
    }

    public async Task InvalidateActiveTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.PasswordResetTokens SET UsedAt = SYSDATETIME() WHERE UserId = @UserId AND UsedAt IS NULL;";
        using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

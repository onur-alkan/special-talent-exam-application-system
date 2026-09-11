using System.Data;
using Dapper;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Parola sıfırlama token tüketimi ve parola güncellemesini tek transaction içinde yürütür.
/// UPDLOCK/HOLDLOCK ile aynı tokenın eşzamanlı kullanımını engeller.
/// </summary>
public sealed class PasswordResetTransactionService : IPasswordResetTransactionService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PasswordResetTransactionService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PasswordResetConsumeResult> ConsumeTokenAndUpdatePasswordAsync(
        string tokenHash,
        string newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            const string lockSql = @"
SELECT Id, UserId, TokenHash, ExpiresAt, UsedAt, CreatedDate, CreatedIpAddress
FROM dbo.PasswordResetTokens WITH (UPDLOCK, HOLDLOCK)
WHERE TokenHash = @TokenHash;";

            var token = await connection.QueryFirstOrDefaultAsync<PasswordResetToken>(
                new CommandDefinition(
                    lockSql,
                    new { TokenHash = tokenHash },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (token is null)
            {
                transaction.Rollback();
                return PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenNotFound);
            }

            if (token.UsedAt is not null)
            {
                transaction.Rollback();
                return PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenAlreadyUsed);
            }

            if (token.ExpiresAt <= DateTime.UtcNow)
            {
                transaction.Rollback();
                return PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenExpired);
            }

            const string updateUserSql = @"
UPDATE dbo.Users
SET PasswordHash = @PasswordHash,
    MustChangePassword = 0,
    SecurityStamp = NEWID(),
    UpdatedDate = SYSDATETIME()
WHERE Id = @UserId;";

            var userAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    updateUserSql,
                    new { UserId = token.UserId, PasswordHash = newPasswordHash },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (userAffected != 1)
            {
                transaction.Rollback();
                return PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.UserUpdateFailed);
            }

            const string updateTokenSql = @"
UPDATE dbo.PasswordResetTokens
SET UsedAt = SYSDATETIME()
WHERE Id = @Id AND UsedAt IS NULL;";

            var tokenAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    updateTokenSql,
                    new { Id = token.Id },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (tokenAffected != 1)
            {
                transaction.Rollback();
                return PasswordResetConsumeResult.Failed(PasswordResetConsumeStatus.TokenUpdateFailed);
            }

            transaction.Commit();
            return PasswordResetConsumeResult.Succeeded(token.UserId);
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Rollback zaten yapılmış veya bağlantı kapanmış olabilir.
            }

            throw;
        }
    }
}

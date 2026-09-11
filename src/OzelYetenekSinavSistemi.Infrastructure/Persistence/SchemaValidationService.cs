using Dapper;
using Microsoft.Extensions.Logging;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence;

/// <summary>
/// Kritik şema sütunlarının varlığını doğrular. ALTER TABLE çalıştırmaz.
/// </summary>
public sealed class SchemaValidationService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SchemaValidationService> _logger;

    public SchemaValidationService(
        IDbConnectionFactory connectionFactory,
        ILogger<SchemaValidationService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COL_LENGTH(N'dbo.Users', N'SecurityStamp');";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var length = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (length is null)
        {
            throw new InvalidOperationException(
                "Veritabanı şeması eksik: dbo.Users.SecurityStamp sütunu bulunamadı. " +
                "database/migrations/001_AddUsersSecurityStamp.sql betiğini uygulayın. " +
                "Bağlantı bilgisi veya gizli değerler bu mesajda yer almaz.");
        }

        _logger.LogInformation("Şema doğrulaması başarılı (Users.SecurityStamp mevcut).");
    }
}

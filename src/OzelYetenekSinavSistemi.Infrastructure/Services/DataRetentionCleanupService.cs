using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// Serilog SQL, AuditLogs ve PasswordResetTokens için batch temizlik.
/// Tablo adları sabittir; parametreli SQL kullanılır; satır içeriği loglanmaz.
/// </summary>
public sealed class DataRetentionCleanupService : IDataRetentionCleanupService
{
    private const string AppLockResource = "OYS_DataRetentionCleanup";
    private const int MaxBatchesPerTable = 100;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DataRetentionOptions _options;
    private readonly ILogger<DataRetentionCleanupService> _logger;

    public DataRetentionCleanupService(
        IDbConnectionFactory connectionFactory,
        IOptions<DataRetentionOptions> options,
        ILogger<DataRetentionCleanupService> logger)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnableCleanup)
        {
            _logger.LogInformation("Veri saklama temizliği kapalı (DataRetention:EnableCleanup=false).");
            return;
        }

        var batchSize = Math.Clamp(_options.BatchSize, DataRetentionOptions.MinBatchSize, DataRetentionOptions.MaxBatchSize);

        using var connection = (SqlConnection)await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        if (!await TryAcquireLockAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Veri saklama temizliği atlandı; başka bir instance çalışıyor.");
            return;
        }

        try
        {
            var serilogDeleted = await SafeDeleteBatchesAsync(
                "Serilog",
                () => DeleteOldSerilogLogsAsync(connection, DateTime.UtcNow.AddDays(-_options.SerilogSqlDays), batchSize, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            var auditDeleted = await SafeDeleteBatchesAsync(
                "AuditLogs",
                () => DeleteOldAuditLogsAsync(connection, DateTime.UtcNow.AddDays(-_options.AuditLogDays), batchSize, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            var tokenDeleted = await SafeDeleteBatchesAsync(
                "PasswordResetTokens",
                () => DeleteExpiredPasswordResetTokensAsync(connection, DateTime.UtcNow.AddDays(-_options.PasswordResetTokenDays), batchSize, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Veri saklama temizliği tamamlandı. SerilogDeleted={SerilogDeleted}, AuditDeleted={AuditDeleted}, TokenDeleted={TokenDeleted}",
                serilogDeleted, auditDeleted, tokenDeleted);
        }
        finally
        {
            await ReleaseLockAsync(connection, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> SafeDeleteBatchesAsync(
        string tableLabel,
        Func<Task<int>> deleteBatchAsync,
        CancellationToken cancellationToken)
    {
        var total = 0;
        try
        {
            for (var i = 0; i < MaxBatchesPerTable; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var deleted = await deleteBatchAsync().ConfigureAwait(false);
                if (deleted <= 0)
                    break;
                total += deleted;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Tek tablo hatası diğerlerini engellemez; satır içeriği / hassas veri loglanmaz.
            _logger.LogError(ex, "Veri saklama temizliği başarısız. Table={Table}", tableLabel);
        }

        return total;
    }

    private static async Task<int> DeleteOldSerilogLogsAsync(
        SqlConnection connection, DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        const string sql = @"
DELETE TOP (@BatchSize)
FROM dbo.Logs
WHERE TimeStamp < @Cutoff;";

        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Cutoff = cutoffUtc, BatchSize = batchSize }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<int> DeleteOldAuditLogsAsync(
        SqlConnection connection, DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        const string sql = @"
DELETE TOP (@BatchSize)
FROM dbo.AuditLogs
WHERE CreatedDate < @Cutoff;";

        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Cutoff = cutoffUtc, BatchSize = batchSize }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<int> DeleteExpiredPasswordResetTokensAsync(
        SqlConnection connection, DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        // Aktif ve süresi dolmamış tokenlar silinmez.
        const string sql = @"
DELETE TOP (@BatchSize)
FROM dbo.PasswordResetTokens
WHERE
    (UsedAt IS NOT NULL AND UsedAt < @Cutoff)
    OR (ExpiresAt < SYSDATETIME() AND ExpiresAt < @Cutoff);";

        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Cutoff = cutoffUtc, BatchSize = batchSize }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private static async Task<bool> TryAcquireLockAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @result INT;
EXEC @result = sp_getapplock
    @Resource = @Resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Session',
    @LockTimeout = 0;
SELECT @result;";

        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { Resource = AppLockResource }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return result >= 0;
    }

    private static async Task ReleaseLockAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = 'Session';",
                    new { Resource = AppLockResource },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch
        {
            // Bağlantı kapanırken session kilidi düşer.
        }
    }

    /// <summary>Unit testler için silme kurallarını doğrulamak üzere kullanılır.</summary>
    internal static bool ShouldDeletePasswordResetToken(DateTime? usedAt, DateTime expiresAt, DateTime now, DateTime cutoff) =>
        (usedAt is not null && usedAt < cutoff)
        || (expiresAt < now && expiresAt < cutoff);
}

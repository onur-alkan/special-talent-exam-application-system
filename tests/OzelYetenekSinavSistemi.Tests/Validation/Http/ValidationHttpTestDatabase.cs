using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Tests.TestSupport;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

/// <summary>
/// İzole geçici SQL Server veritabanı oluşturur ve test sonunda drop eder.
/// Register ve admin HTTP testleri tarafından paylaşılır.
/// </summary>
public sealed class ValidationHttpTestDatabase : IAsyncDisposable
{
    private readonly SqlConnectionStringBuilder _masterBuilder;
    private bool _created;

    public ValidationHttpTestDatabase()
    {
        _masterBuilder = ResolveMasterConnectionBuilder();

        var suffix = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        DatabaseName = $"{TestDatabaseSafetyGuard.AllowedPrefix}{suffix}"[
            ..Math.Min(128, $"{TestDatabaseSafetyGuard.AllowedPrefix}{suffix}".Length)];

        var appBuilder = new SqlConnectionStringBuilder(_masterBuilder.ConnectionString)
        {
            InitialCatalog = DatabaseName
        };
        ConnectionString = appBuilder.ConnectionString;
        TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(ConnectionString);
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(ConnectionString);
        await CleanupStaleOrphanedDatabasesAsync(cancellationToken).ConfigureAwait(false);

        await using (var master = new SqlConnection(_masterBuilder.ConnectionString))
        {
            await master.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{DatabaseName}]";
            create.CommandTimeout = 120;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _created = true;

        var scriptPath = LocateSqlScript();
        var preparedScriptPath = await PrepareScriptForDatabaseAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        try
        {
            await RunSqlCmdScriptAsync(preparedScriptPath, cancellationToken).ConfigureAwait(false);
            await VerifySchemaAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(preparedScriptPath))
                File.Delete(preparedScriptPath);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_created)
            return;

        SqlConnection.ClearAllPools();

        try
        {
            await using var master = new SqlConnection(_masterBuilder.ConnectionString);
            await master.OpenAsync().ConfigureAwait(false);
            await using var drop = master.CreateCommand();
            drop.CommandText = $@"
IF DB_ID(N'{DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END";
            drop.CommandTimeout = 120;
            await drop.ExecuteNonQueryAsync().ConfigureAwait(false);

            await using var verify = master.CreateCommand();
            verify.CommandText = $"SELECT DB_ID(N'{DatabaseName}')";
            var remaining = await verify.ExecuteScalarAsync().ConfigureAwait(false);
            if (remaining is not null and not DBNull)
            {
                throw new InvalidOperationException(
                    $"Geçici test veritabanı drop sonrası hâlâ mevcut: [{DatabaseName}].");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Geçici test veritabanı drop edilemedi: [{DatabaseName}].", ex);
        }
        finally
        {
            _created = false;
        }
    }

    internal static SqlConnectionStringBuilder ResolveMasterConnectionBuilder() =>
        TestSqlServerConnection.CreateMasterBuilder();

    internal static async Task DropDatabaseByNameAsync(string databaseName, CancellationToken cancellationToken = default)
    {
        var masterBuilder = ResolveMasterConnectionBuilder();
        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var drop = master.CreateCommand();
        drop.CommandText = $@"
IF DB_ID(N'{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END";
        drop.CommandTimeout = 120;
        await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task CleanupLegacyValAdminDatabasesAsync(CancellationToken cancellationToken = default)
    {
        var leftovers = await ListLeftoverTestDatabasesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var name in leftovers.Where(n => n.StartsWith("OYS_ValAdmin_", StringComparison.OrdinalIgnoreCase)))
            await DropDatabaseByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task CleanupStaleOrphanedDatabasesAsync(CancellationToken cancellationToken = default)
    {
        await CleanupLegacyValAdminDatabasesAsync(cancellationToken).ConfigureAwait(false);

        var leftovers = await ListLeftoverTestDatabasesAsync(cancellationToken).ConfigureAwait(false);
        foreach (var name in leftovers.Where(IsStaleTestDatabaseName))
            await DropDatabaseByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsStaleTestDatabaseName(string databaseName)
    {
        if (!databaseName.StartsWith(TestDatabaseSafetyGuard.AllowedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = databaseName[TestDatabaseSafetyGuard.AllowedPrefix.Length..];
        var underscore = suffix.IndexOf('_', StringComparison.Ordinal);
        if (underscore <= 0)
            return false;

        var timestamp = suffix[..underscore];
        if (!DateTime.TryParseExact(
                timestamp,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var createdUtc))
        {
            return false;
        }

        return createdUtc < DateTime.UtcNow.AddMinutes(-10);
    }

    internal static async Task<IReadOnlyList<string>> ListLeftoverTestDatabasesAsync(
        CancellationToken cancellationToken = default)
    {
        var masterBuilder = ResolveMasterConnectionBuilder();
        await using var master = new SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = master.CreateCommand();
        command.CommandText = @"
SELECT name
FROM sys.databases
WHERE name LIKE 'OYS_ValidationHttp[_]%' ESCAPE '\'
   OR name LIKE 'OYS_ValAdmin[_]%' ESCAPE '\'";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            names.Add(reader.GetString(0));
        return names;
    }

    private async Task<string> PrepareScriptForDatabaseAsync(string scriptPath, CancellationToken cancellationToken)
    {
        // Schema scripti USE/CREATE DATABASE içermez; hedef DB sqlcmd -d ile seçilir.
        // Eski script kopyalarına karşı savunmacı temizleme bırakılır.
        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        script = Regex.Replace(
            script,
            @"IF DB_ID\(N'OzelYetenekSinavSistemi'\)[\s\S]*?^GO\s*$",
            string.Empty,
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        script = Regex.Replace(
            script,
            @"^\s*USE\s+\[[^\]]+\]\s*;?\s*$",
            string.Empty,
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        var preparedPath = Path.Combine(Path.GetTempPath(), $"{DatabaseName}_{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(preparedPath, script, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        return preparedPath;
    }

    private async Task RunSqlCmdScriptAsync(string scriptPath, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        var startInfo = TestSqlServerConnection.CreateSqlCmdStartInfo(builder, DatabaseName, scriptPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("sqlcmd başlatılamadı.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Geçici veritabanı şeması uygulanamadı (sqlcmd exit {process.ExitCode}). {stderr} {stdout}");
        }
    }

    private async Task VerifySchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COL_LENGTH(N'dbo.Users', N'SecurityStamp');";
        var length = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (length is null or DBNull)
        {
            throw new InvalidOperationException(
                "Geçici veritabanı şeması doğrulanamadı: dbo.Users.SecurityStamp eksik.");
        }
    }

    private static string LocateSqlScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "database", "OzelYetenekSinavSistemi.sql");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("database/OzelYetenekSinavSistemi.sql bulunamadı.");
    }
}

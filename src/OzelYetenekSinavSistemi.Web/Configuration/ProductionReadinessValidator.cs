using System.Data;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Web.Configuration;

/// <summary>
/// Production startup fail-fast: connection string ve private storage.
/// Secret/connection string/fiziksel yol loglamaz.
/// </summary>
public static class ProductionReadinessValidator
{
    public static void ValidateConnectionString(IHostEnvironment environment, string? connectionString)
    {
        if (!environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection zorunludur.");
            return;
        }

        var errors = new List<string>();
        ValidateConnectionStringCore(connectionString, errors);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }

    public static void ValidateConnectionStringCore(string? connectionString, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("Production ortamında ConnectionStrings:DefaultConnection zorunludur.");
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                errors.Add("Production ortamında ConnectionStrings:DefaultConnection sunucu adresi eksik.");
                return;
            }

            if (IsDevelopmentSqlDataSource(builder.DataSource))
            {
                errors.Add(
                    "Production ortamında ConnectionStrings:DefaultConnection localhost/geliştirme sunucusu olamaz.");
            }

            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
                errors.Add("Production ortamında ConnectionStrings:DefaultConnection veritabanı adı eksik.");
        }
        catch (ArgumentException)
        {
            errors.Add("Production ortamında ConnectionStrings:DefaultConnection geçersiz.");
        }
        catch (FormatException)
        {
            errors.Add("Production ortamında ConnectionStrings:DefaultConnection geçersiz.");
        }
    }

    public static bool IsDevelopmentSqlDataSource(string dataSource)
    {
        var raw = dataSource.Trim();
        if (raw.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            raw = raw[4..].Trim();

        if (raw is "." or ".\\" || raw.StartsWith(".\\", StringComparison.Ordinal))
            return true;

        if (raw.StartsWith("(local)", StringComparison.OrdinalIgnoreCase))
            return true;

        var host = raw.Split(['\\', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
            ?? string.Empty;

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
               || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsurePrivatePhotoStorageAccessible(
        PhotoUploadOptions options,
        IHostEnvironment environment,
        List<string>? errors = null)
    {
        var localErrors = errors ?? new List<string>();

        if (string.IsNullOrWhiteSpace(options.StorageRootPath))
        {
            localErrors.Add("PhotoUpload storage dizini yapılandırılmamış.");
        }
        else
        {
            try
            {
                var storage = Path.GetFullPath(options.StorageRootPath);
                var webRoot = string.IsNullOrWhiteSpace(options.WebRootPath)
                    ? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"))
                    : Path.GetFullPath(options.WebRootPath);

                var webPrefix = AppendDirectorySeparator(webRoot);
                var storageFull = Path.GetFullPath(storage);
                if (AppendDirectorySeparator(storageFull).StartsWith(webPrefix, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(storageFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        webRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                {
                    localErrors.Add("PhotoUpload storage dizini web kökünün (wwwroot) altında olamaz.");
                }
                else
                {
                    Directory.CreateDirectory(storageFull);
                    var probe = Path.Combine(storageFull, $".oys-health-{Guid.NewGuid():N}.tmp");
                    try
                    {
                        File.WriteAllText(probe, "ok");
                        _ = File.ReadAllText(probe);
                    }
                    finally
                    {
                        if (File.Exists(probe))
                            File.Delete(probe);
                    }
                }
            }
            catch (Exception)
            {
                localErrors.Add("PhotoUpload storage dizinine yazma/okuma erişimi doğrulanamadı.");
            }
        }

        if (errors is null && localErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", localErrors));
    }

    public static void EnsureDataProtectionKeysAccessible(string keysDirectory, List<string>? errors = null)
    {
        var localErrors = errors ?? new List<string>();
        try
        {
            if (string.IsNullOrWhiteSpace(keysDirectory))
            {
                localErrors.Add("DataProtection anahtar dizini yapılandırılmamış.");
            }
            else
            {
                var full = Path.GetFullPath(keysDirectory);
                Directory.CreateDirectory(full);
                var probe = Path.Combine(full, $".oys-dp-health-{Guid.NewGuid():N}.tmp");
                try
                {
                    File.WriteAllText(probe, "ok");
                    _ = File.ReadAllText(probe);
                }
                finally
                {
                    if (File.Exists(probe))
                        File.Delete(probe);
                }
            }
        }
        catch (Exception)
        {
            localErrors.Add("DataProtection anahtar dizinine erişim doğrulanamadı.");
        }

        if (errors is null && localErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", localErrors));
    }

    public static async Task<bool> CanOpenSqlAsync(string connectionString, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = Math.Max(1, (int)timeout.TotalSeconds);
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static string AppendDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}


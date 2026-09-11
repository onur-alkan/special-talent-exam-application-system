using Microsoft.Data.SqlClient;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

/// <summary>
/// HTTP/integration testlerinin gerçek geliştirme veya production veritabanına bağlanmasını engeller.
/// </summary>
internal static class TestDatabaseSafetyGuard
{
    internal const string AllowedPrefix = "OYS_ValidationHttp_";

    private static readonly HashSet<string> ForbiddenCatalogs = new(StringComparer.OrdinalIgnoreCase)
    {
        "OzelYetenekSinavSistemi",
        "OYS",
        "master",
        "tempdb",
        "model",
        "msdb"
    };

    internal static void EnsureIsolatedTestDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Test connection string boş olamaz.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        var catalog = builder.InitialCatalog;

        if (string.IsNullOrWhiteSpace(catalog))
        {
            throw new InvalidOperationException(
                "Test connection string geçici bir veritabanı adı (Initial Catalog) içermelidir.");
        }

        if (ForbiddenCatalogs.Contains(catalog))
        {
            throw new InvalidOperationException(
                $"Yasak veritabanı adı kullanılamaz: '{catalog}'. Testler izole geçici veritabanı kullanmalıdır.");
        }

        if (!catalog.StartsWith(AllowedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Test veritabanı adı '{AllowedPrefix}' ile başlamalıdır; bulunan: '{catalog}'.");
        }
    }

    internal static bool IsAllowedTestDatabaseName(string? databaseName) =>
        !string.IsNullOrWhiteSpace(databaseName)
        && databaseName.StartsWith(AllowedPrefix, StringComparison.OrdinalIgnoreCase);

    internal static bool IsForbiddenDatabaseName(string? databaseName) =>
        string.IsNullOrWhiteSpace(databaseName)
        || ForbiddenCatalogs.Contains(databaseName);
}

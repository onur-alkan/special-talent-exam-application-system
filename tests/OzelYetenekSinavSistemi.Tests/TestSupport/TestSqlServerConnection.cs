using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace OzelYetenekSinavSistemi.Tests.TestSupport;

/// <summary>
/// Resolves the SQL Server endpoint used by isolated test databases.
/// CI supplies a disposable instance through <see cref="EnvironmentVariableName"/>.
/// Local Windows development keeps the historical SQLEXPRESS Integrated Security fallback.
/// Production application configuration is never read.
/// </summary>
internal static class TestSqlServerConnection
{
    internal const string EnvironmentVariableName = "OYS_TEST_SQLSERVER_CONNECTION";

    internal const string LegacyEnvironmentVariableName = "OYS_TEST_CONNECTION";

    internal const string LocalDevelopmentFallback =
        @"Server=.\SQLEXPRESS;Database=OzelYetenekSinavSistemi;Trusted_Connection=True;TrustServerCertificate=True;";

    internal static string ResolveBaseConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var legacy = Environment.GetEnvironmentVariable(LegacyEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(legacy))
            return legacy;

        return LocalDevelopmentFallback;
    }

    internal static SqlConnectionStringBuilder CreateBuilder(string initialCatalog)
    {
        return new SqlConnectionStringBuilder(ResolveBaseConnectionString())
        {
            InitialCatalog = initialCatalog
        };
    }

    internal static string BuildConnectionString(string initialCatalog) =>
        CreateBuilder(initialCatalog).ConnectionString;

    internal static SqlConnectionStringBuilder CreateMasterBuilder() => CreateBuilder("master");

    /// <summary>
    /// Builds a sqlcmd invocation. Local Integrated Security keeps the historical -E flags.
    /// SQL authentication (CI) adds -C so sqlcmd 18 can use TrustServerCertificate.
    /// Passwords are passed through ArgumentList so they are not shell-parsed.
    /// </summary>
    internal static ProcessStartInfo CreateSqlCmdStartInfo(
        SqlConnectionStringBuilder builder,
        string databaseName,
        string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sqlcmd",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        startInfo.ArgumentList.Add("-S");
        startInfo.ArgumentList.Add(builder.DataSource);
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(databaseName);
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-b");
        startInfo.ArgumentList.Add("-I");

        if (builder.IntegratedSecurity)
        {
            startInfo.ArgumentList.Add("-E");
        }
        else
        {
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add("-U");
            startInfo.ArgumentList.Add(builder.UserID);
            startInfo.ArgumentList.Add("-P");
            startInfo.ArgumentList.Add(builder.Password);
        }

        return startInfo;
    }
}

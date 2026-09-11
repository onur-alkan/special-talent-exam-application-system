using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Tests.TestSupport;

namespace OzelYetenekSinavSistemi.Tests.Persistence;

/// <summary>
/// SQL kurulum/migration scriptlerinin hedef DB'den bağımsız ve güvenli olduğunu doğrular.
/// </summary>
public sealed class SqlScriptHardeningTests
{
    private static readonly string[] ForbiddenSystemDatabases =
    [
        "master", "tempdb", "model", "msdb"
    ];

    private static readonly Regex UseStatementRegex = new(
        @"^\s*USE\s+(\[[^\]]+\]|[A-Za-z0-9_]+)\s*;?\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DropDatabaseRegex = new(
        @"\bDROP\s+DATABASE\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TruncateRegex = new(
        @"\bTRUNCATE\s+TABLE\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UncontrolledDeleteRegex = new(
        @"^\s*DELETE\s+FROM\s+\S+\s*;?\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void MigrationScripts_DoNotContainHardcodedUseOzelYetenekSinavSistemi()
    {
        foreach (var path in EnumerateMigrationScripts())
        {
            var sql = File.ReadAllText(path);
            Assert.DoesNotContain("USE [OzelYetenekSinavSistemi]", sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("USE OzelYetenekSinavSistemi", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SchemaAndMigrationScripts_DoNotContainHardcodedProjectDatabaseUse()
    {
        foreach (var path in EnumerateDeployableSqlScripts())
        {
            var sql = File.ReadAllText(path);
            foreach (Match match in UseStatementRegex.Matches(sql))
            {
                var target = match.Groups[1].Value.Trim('[', ']');
                Assert.False(
                    string.Equals(target, "OzelYetenekSinavSistemi", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(path)} contains USE {target}");
                Assert.False(
                    target.StartsWith("OYS_", StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(path)} contains hardcoded USE {target}");
            }
        }
    }

    [Fact]
    public void MigrationScriptOrder_IsUniqueAndContiguousFrom001()
    {
        var numbers = EnumerateMigrationScripts()
            .Select(path => Path.GetFileName(path)!)
            .Select(name =>
            {
                var prefix = name.Split('_', 2)[0];
                Assert.True(int.TryParse(prefix, NumberStyles.None, CultureInfo.InvariantCulture, out var n), name);
                return n;
            })
            .OrderBy(n => n)
            .ToArray();

        Assert.NotEmpty(numbers);
        Assert.Equal(numbers.Length, numbers.Distinct().Count());
        Assert.Equal(1, numbers[0]);
        for (var i = 0; i < numbers.Length; i++)
            Assert.Equal(i + 1, numbers[i]);
    }

    [Fact]
    public void Migration006_BirthDate_ExistsInDelivery()
    {
        var path = Path.Combine(LocateDatabaseRoot(), "migrations", "006_AddApplicantBirthDate.sql");
        Assert.True(File.Exists(path));
        var sql = File.ReadAllText(path);
        Assert.Contains("BirthDate", sql, StringComparison.Ordinal);
        Assert.Contains("DATE NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchemaScript_DoesNotCreateOrUseHardcodedDatabase()
    {
        var schema = File.ReadAllText(Path.Combine(LocateDatabaseRoot(), "OzelYetenekSinavSistemi.sql"));
        Assert.DoesNotContain("CREATE DATABASE [", schema, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USE [OzelYetenekSinavSistemi]", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DB_NAME() IN (N'master'", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapCreateDatabase_IsOptionalAndSeparate()
    {
        var bootstrap = Path.Combine(
            LocateDatabaseRoot(),
            "bootstrap",
            "CreateDatabase.OzelYetenekSinavSistemi.sql");
        Assert.True(File.Exists(bootstrap));
        var sql = File.ReadAllText(bootstrap);
        Assert.Contains("CREATE DATABASE [OzelYetenekSinavSistemi]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeployableScripts_HaveNoDangerousDropTruncateOrBlindDelete()
    {
        foreach (var path in EnumerateDeployableSqlScripts())
        {
            var sql = File.ReadAllText(path);
            Assert.False(DropDatabaseRegex.IsMatch(sql), path);
            Assert.False(TruncateRegex.IsMatch(sql), path);
            Assert.False(UncontrolledDeleteRegex.IsMatch(sql), $"{path} has uncontrolled DELETE");
        }
    }

    [Fact]
    public void Migrations_GuardAgainstSystemDatabasesAndRequirePrerequisites()
    {
        foreach (var path in EnumerateMigrationScripts())
        {
            var sql = File.ReadAllText(path);
            Assert.Contains("DB_NAME() IN (N'master'", sql, StringComparison.Ordinal);
            Assert.True(
                sql.Contains("dbo.Users", StringComparison.Ordinal)
                || sql.Contains("dbo.ExamPreferenceOptions", StringComparison.Ordinal),
                Path.GetFileName(path));
        }
    }

    [Fact]
    public void SchemaAndRepository_MapCoreIdentityAndBirthFields()
    {
        var schema = File.ReadAllText(Path.Combine(LocateDatabaseRoot(), "OzelYetenekSinavSistemi.sql"));
        Assert.Contains("BirthDate         DATE          NULL", schema, StringComparison.Ordinal);
        Assert.Contains("BirthYear         SMALLINT      NULL", schema, StringComparison.Ordinal);
        Assert.Contains("UQ_CandidateApplications_User_Exam UNIQUE (UserId, ExamPeriodId)", schema, StringComparison.Ordinal);
        Assert.Contains("UQ_CandidateApplications_Exam_No UNIQUE (ExamPeriodId, CandidateNo)", schema, StringComparison.Ordinal);
        Assert.Contains("PreferenceOrder", schema, StringComparison.Ordinal);
        Assert.Contains("UQ_CandidateExamResults_ApplicationId UNIQUE (ApplicationId)", schema, StringComparison.Ordinal);

        var userEntity = LocateUnder("src", "OzelYetenekSinavSistemi.Domain", "Entities", "User.cs");
        var entity = File.ReadAllText(userEntity);
        Assert.Contains("BirthDate", entity, StringComparison.Ordinal);
        Assert.Contains("BirthYear", entity, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlDeploymentDocumentation_ListsOrderedSqlcmdWithDashDAndDashB()
    {
        var doc = File.ReadAllText(LocateUnder("docs", "SQL-DEPLOYMENT.md"));
        Assert.Contains("sqlcmd -S \"<SERVER>\" -d \"<DATABASE>\" -E -b -I -i", doc, StringComparison.Ordinal);
        Assert.Contains("001_AddUsersSecurityStamp.sql", doc, StringComparison.Ordinal);
        Assert.Contains("006_AddApplicantBirthDate.sql", doc, StringComparison.Ordinal);
        Assert.Contains("Backup", doc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveAudit_FreshInstallSecondPassAndDevDbUnchanged_OnDistinctNames()
    {
        if (!await CanConnectToSqlServerAsync())
        {
            // CI veya sqlcmd yoksa statik testler yeterli; canlı audit atlanır.
            return;
        }

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var dbA = $"OYS_FinalSqlAudit_A_{stamp}";
        var dbB = $"OYS_FinalSqlAudit_B_{stamp}";
        Assert.False(string.Equals(dbA, "OzelYetenekSinavSistemi", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals(dbB, "OzelYetenekSinavSistemi", StringComparison.OrdinalIgnoreCase));

        await CleanupLeftoverAuditDatabasesAsync();
        var baseline = await CaptureDevelopmentSnapshotAsync();

        try
        {
            await CreateEmptyDatabaseAsync(dbA);
            await CreateEmptyDatabaseAsync(dbB);

            await ApplyAllScriptsInOrderAsync(dbA);
            await AssertSchemaExpectationsAsync(dbA);
            var seedAfterFirst = await CaptureSeedCountsAsync(dbA);

            await ApplyAllScriptsInOrderAsync(dbA);
            await AssertSchemaExpectationsAsync(dbA);
            var seedAfterSecond = await CaptureSeedCountsAsync(dbA);
            Assert.Equal(seedAfterFirst.Roles, seedAfterSecond.Roles);
            Assert.Equal(seedAfterFirst.YgsYears, seedAfterSecond.YgsYears);
            Assert.Equal(seedAfterFirst.SystemSettings, seedAfterSecond.SystemSettings);

            await ApplyAllScriptsInOrderAsync(dbB);
            await AssertSchemaExpectationsAsync(dbB);

            if (baseline is not null)
            {
                var after = await CaptureDevelopmentSnapshotAsync();
                Assert.NotNull(after);
                baseline.AssertUnchanged(after!);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbA);
            await DropDatabaseAsync(dbB);
            await CleanupLeftoverAuditDatabasesAsync();
        }
    }

    private static async Task CleanupLeftoverAuditDatabasesAsync()
    {
        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync().ConfigureAwait(false);
        var names = (await master.QueryAsync<string>(
                "SELECT name FROM sys.databases WHERE name LIKE 'OYS_FinalSqlAudit[_]%' ESCAPE '\\'")
            .ConfigureAwait(false)).ToList();

        foreach (var name in names)
            await DropDatabaseAsync(name).ConfigureAwait(false);
    }

    private static IEnumerable<string> EnumerateMigrationScripts()
    {
        var migrationsDir = Path.Combine(LocateDatabaseRoot(), "migrations");
        return Directory.GetFiles(migrationsDir, "*.sql")
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateDeployableSqlScripts()
    {
        yield return Path.Combine(LocateDatabaseRoot(), "OzelYetenekSinavSistemi.sql");
        foreach (var migration in EnumerateMigrationScripts())
            yield return migration;
    }

    private static async Task ApplyAllScriptsInOrderAsync(string databaseName)
    {
        var root = LocateDatabaseRoot();
        var scripts = new List<string>
        {
            Path.Combine(root, "OzelYetenekSinavSistemi.sql")
        };
        scripts.AddRange(EnumerateMigrationScripts());

        foreach (var script in scripts)
            await RunSqlCmdAsync(databaseName, script).ConfigureAwait(false);
    }

    private static async Task AssertSchemaExpectationsAsync(string databaseName)
    {
        await using var connection = new SqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync().ConfigureAwait(false);

        string[] tables =
        [
            "Users", "Roles", "ExamPeriods", "ExamPreferenceOptions", "CandidateApplications",
            "CandidateSelectedPreferences", "CandidateExamResults", "PasswordResetTokens",
            "AuditLogs", "DocumentVerifications", "Logs"
        ];

        foreach (var table in tables)
        {
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN OBJECT_ID(@name, 'U') IS NULL THEN 0 ELSE 1 END",
                new { name = $"dbo.{table}" }).ConfigureAwait(false);
            Assert.Equal(1, exists);
        }

        var birthDate = await connection.ExecuteScalarAsync<string?>(@"
SELECT t.name
FROM sys.columns c
INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.Users') AND c.name = N'BirthDate';").ConfigureAwait(false);
        Assert.Equal("date", birthDate, ignoreCase: true);

        var birthDateNullable = await connection.ExecuteScalarAsync<bool>(@"
SELECT c.is_nullable
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(N'dbo.Users') AND c.name = N'BirthDate';").ConfigureAwait(false);
        Assert.True(birthDateNullable);

        var birthYear = await connection.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN COL_LENGTH(N'dbo.Users', N'BirthYear') IS NULL THEN 0 ELSE 1 END")
            .ConfigureAwait(false);
        Assert.Equal(1, birthYear);

        async Task AssertConstraint(string name)
        {
            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM sys.key_constraints WHERE name = @name",
                new { name }).ConfigureAwait(false);
            Assert.True(count >= 1, $"Missing constraint {name}");
        }

        await AssertConstraint("UQ_CandidateApplications_User_Exam").ConfigureAwait(false);
        await AssertConstraint("UQ_CandidateApplications_Exam_No").ConfigureAwait(false);
        await AssertConstraint("UQ_CandidateSelectedPreferences_App_Order").ConfigureAwait(false);
        await AssertConstraint("UQ_CandidateExamResults_ApplicationId").ConfigureAwait(false);

        var fkCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.CandidateApplications')")
            .ConfigureAwait(false);
        Assert.True(fkCount >= 2);
    }

    private static async Task<(int Roles, int YgsYears, int SystemSettings)> CaptureSeedCountsAsync(string databaseName)
    {
        await using var connection = new SqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync().ConfigureAwait(false);
        var roles = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.Roles").ConfigureAwait(false);
        var years = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.YgsYears").ConfigureAwait(false);
        var settings = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.SystemSettings").ConfigureAwait(false);
        return (roles, years, settings);
    }

    private static async Task CreateEmptyDatabaseAsync(string databaseName)
    {
        Assert.StartsWith("OYS_FinalSqlAudit_", databaseName, StringComparison.OrdinalIgnoreCase);
        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync().ConfigureAwait(false);
        await master.ExecuteAsync($"CREATE DATABASE [{databaseName}]").ConfigureAwait(false);
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        if (!databaseName.StartsWith("OYS_FinalSqlAudit_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to drop non-audit database: {databaseName}");

        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync().ConfigureAwait(false);
        await master.ExecuteAsync($@"
IF DB_ID(N'{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END").ConfigureAwait(false);
    }

    private static async Task RunSqlCmdAsync(string databaseName, string scriptPath)
    {
        var builder = new SqlConnectionStringBuilder(BuildConnectionString(databaseName));
        var startInfo = TestSqlServerConnection.CreateSqlCmdStartInfo(builder, databaseName, scriptPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("sqlcmd başlatılamadı.");
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        Assert.True(
            process.ExitCode == 0,
            $"sqlcmd failed for {Path.GetFileName(scriptPath)} on {databaseName} (exit {process.ExitCode}). {stderr} {stdout}");
    }

    private static async Task<bool> CanConnectToSqlServerAsync()
    {
        try
        {
            await using var connection = new SqlConnection(BuildConnectionString("master"));
            await connection.OpenAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string BuildConnectionString(string initialCatalog) =>
        TestSqlServerConnection.BuildConnectionString(initialCatalog);

    private static async Task<DevSnapshot?> CaptureDevelopmentSnapshotAsync()
    {
        try
        {
            await using var connection = new SqlConnection(BuildConnectionString("OzelYetenekSinavSistemi"));
            await connection.OpenAsync().ConfigureAwait(false);
            return await connection.QuerySingleAsync<DevSnapshot>(@"
SELECT
    (SELECT COUNT(1) FROM sys.tables) AS TableCount,
    (SELECT COUNT(1) FROM dbo.Users) AS Users,
    (SELECT COUNT(1) FROM dbo.Roles) AS Roles,
    (SELECT COUNT(1) FROM dbo.ExamPeriods) AS ExamPeriods,
    (SELECT COUNT(1) FROM dbo.ExamPreferenceOptions) AS ExamPreferenceOptions,
    (SELECT ISNULL(MAX(CAST(CreatedDate AS BIGINT)), 0) FROM dbo.Users) AS UsersMaxTicks,
    (SELECT ISNULL(MAX(CAST(CreatedDate AS BIGINT)), 0) FROM dbo.ExamPeriods) AS ExamPeriodsMaxTicks;").ConfigureAwait(false);
        }
        catch (SqlException)
        {
            return null;
        }
    }

    private sealed class DevSnapshot
    {
        public int TableCount { get; init; }
        public int Users { get; init; }
        public int Roles { get; init; }
        public int ExamPeriods { get; init; }
        public int ExamPreferenceOptions { get; init; }
        public long UsersMaxTicks { get; init; }
        public long ExamPeriodsMaxTicks { get; init; }

        public void AssertUnchanged(DevSnapshot after)
        {
            Assert.Equal(TableCount, after.TableCount);
            Assert.Equal(Users, after.Users);
            Assert.Equal(Roles, after.Roles);
            Assert.Equal(ExamPeriods, after.ExamPeriods);
            Assert.Equal(ExamPreferenceOptions, after.ExamPreferenceOptions);
            Assert.Equal(UsersMaxTicks, after.UsersMaxTicks);
            Assert.Equal(ExamPeriodsMaxTicks, after.ExamPeriodsMaxTicks);
        }
    }

    private static string LocateDatabaseRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "database");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "OzelYetenekSinavSistemi.sql")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("database klasörü bulunamadı.");
    }

    private static string LocateUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

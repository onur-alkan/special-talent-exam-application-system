using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Moq;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Applications;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Infrastructure.Persistence;
using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Concurrency;

/// <summary>
/// CandidateNo atomik üretimini izole SQL Server audit DB üzerinde doğrular.
/// Geliştirme DB'sine yazmaz; test sonunda OYS_CandidateNoAudit_* drop edilir.
/// </summary>
[Collection(nameof(CandidateNoConcurrencyCollection))]
public sealed class CandidateNoConcurrencyLiveTests
{
    private const string AuditPrefix = "OYS_CandidateNoAudit_";
    private const string ForceRollbackCorrelation = "FORCE_ROLLBACK_AUDIT";
    private static readonly Guid SuperAdminId = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid Exam1Id = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBB01");
    private static readonly Guid Exam2Id = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBB02");
    private static readonly Guid Option1Id = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCC01");
    private static readonly Guid Option2Id = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCC02");

    [Fact]
    public void Schema_HasExamPeriodCandidateNoAndUserExamUniqueConstraints()
    {
        var schema = File.ReadAllText(Path.Combine(LocateDatabaseRoot(), "OzelYetenekSinavSistemi.sql"));
        Assert.Contains("UQ_CandidateApplications_Exam_No UNIQUE (ExamPeriodId, CandidateNo)", schema, StringComparison.Ordinal);
        Assert.Contains("UQ_CandidateApplications_User_Exam UNIQUE (UserId, ExamPeriodId)", schema, StringComparison.Ordinal);
        Assert.Contains("ExamPeriodCounters", schema, StringComparison.Ordinal);
        Assert.Contains("LastCandidateNo", schema, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateApplicationViewModel_DoesNotBindCandidateNoFromClient()
    {
        var props = typeof(CreateApplicationViewModel).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("CandidateNo", props);
        Assert.Contains(nameof(CreateApplicationViewModel.ExamPeriodId), props);
        Assert.Contains(nameof(CreateApplicationViewModel.SelectedPreferenceOptionIds), props);
    }

    [Fact]
    public void CandidateNumberService_UsesCounterRowWithUpdlockHoldlockInSameTransaction()
    {
        var source = File.ReadAllText(LocateUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "CandidateNumberService.cs"));
        Assert.Contains("ExamPeriodCounters WITH (UPDLOCK, HOLDLOCK)", source, StringComparison.Ordinal);
        Assert.Contains("LastCandidateNo = LastCandidateNo + 1", source, StringComparison.Ordinal);
        Assert.Contains("IDbTransaction transaction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MAX(CandidateNo)", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SemaphoreSlim", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateApplicationAtomic_CoversPreferencesInSameTransaction_AndMapsUniqueViolations()
    {
        var source = File.ReadAllText(LocateUnder(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories",
            "CandidateApplicationRepository.cs"));
        Assert.Contains("BeginTransactionAsync(IsolationLevel.ReadCommitted", source, StringComparison.Ordinal);
        Assert.Contains("GenerateNextAsync(connection, transaction", source, StringComparison.Ordinal);
        Assert.Contains("InsertPreferencesAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsUserExamUniqueViolation", source, StringComparison.Ordinal);
        Assert.Contains("ApplicationWriteStatus.AlreadyApplied", source, StringComparison.Ordinal);
        Assert.Contains("ApplicationWriteStatus.Conflict", source, StringComparison.Ordinal);
        Assert.Contains(
            "DEFAULT SYSDATETIME()",
            File.ReadAllText(Path.Combine(LocateDatabaseRoot(), "OzelYetenekSinavSistemi.sql")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MapCreateError_DoesNotLeakSqlExceptionText()
    {
        var source = File.ReadAllText(LocateUnder(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "CandidateApplicationService.cs"));
        Assert.Contains("Bu sınav dönemine zaten başvurdunuz.", source, StringComparison.Ordinal);
        Assert.Contains("İşlem başka bir değişiklikle çakıştı. Yeniden deneyiniz.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("2627", source, StringComparison.Ordinal);
        Assert.DoesNotContain("deadlock", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Live_ParallelApplications_DuplicateSubmit_SecondPeriod_Rollback_AreSafe()
    {
        if (!await CanConnectToSqlServerAsync())
            return;

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var databaseName = $"{AuditPrefix}{stamp}";
        Assert.StartsWith(AuditPrefix, databaseName, StringComparison.OrdinalIgnoreCase);

        await CleanupLeftoverAuditDatabasesAsync();
        var baseline = await CaptureDevelopmentSnapshotAsync();

        try
        {
            await CreateEmptyDatabaseAsync(databaseName);
            await ApplySchemaScriptsAsync(databaseName);
            await SeedBaseAsync(databaseName);

            var connectionString = BuildConnectionString(databaseName);
            var sut = BuildApplicationService(connectionString);

            var users = await InsertSyntheticCandidatesAsync(databaseName, count: 50, idOffset: 1);
            var sw = Stopwatch.StartNew();
            var barrier = new Barrier(users.Count);
            var results = new ConcurrentBag<(Guid UserId, bool Success, int? CandidateNo, string? Error)>();

            var tasks = users.Select(userId => Task.Run(async () =>
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(30));
                try
                {
                    var result = await sut.ApplyAsync(
                        userId,
                        Model(Exam1Id, Option1Id),
                        "127.0.0.1",
                        $"parallel-{userId:N}");
                    results.Add((userId, result.Success, result.Data?.CandidateNo, result.ErrorMessage));
                }
                catch (Exception ex)
                {
                    results.Add((userId, false, null, ex.GetType().Name + ": " + ex.Message));
                }
            })).ToArray();

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromMinutes(2));
            sw.Stop();

            Assert.Equal(50, results.Count);
            Assert.All(results, r => Assert.True(r.Success, r.Error));
            var numbers = results.Select(r => r.CandidateNo!.Value).OrderBy(n => n).ToArray();
            Assert.Equal(50, numbers.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, 50), numbers);
            await AssertNoOrphansAsync(databaseName, Exam1Id, expectedApplications: 50);
            Assert.True(sw.Elapsed < TimeSpan.FromMinutes(2), $"50 parallel took {sw.Elapsed}");

            var duplicateUser = (await InsertSyntheticCandidatesAsync(databaseName, count: 1, idOffset: 200)).Single();
            var dupBarrier = new Barrier(10);
            var dupResults = new ConcurrentBag<(bool Success, string? Error)>();
            var dupTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
            {
                dupBarrier.SignalAndWait(TimeSpan.FromSeconds(30));
                try
                {
                    var result = await sut.ApplyAsync(duplicateUser, Model(Exam1Id, Option1Id), "127.0.0.1", $"dup-{Guid.NewGuid():N}");
                    dupResults.Add((result.Success, result.ErrorMessage));
                }
                catch (Exception ex)
                {
                    dupResults.Add((false, ex.GetType().Name + ": " + ex.Message));
                }
            })).ToArray();

            await Task.WhenAll(dupTasks).WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(1, dupResults.Count(r => r.Success));
            Assert.Equal(9, dupResults.Count(r => !r.Success));
            Assert.All(dupResults.Where(r => !r.Success), r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Error));
                Assert.DoesNotContain("SqlException", r.Error!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("UNIQUE KEY", r.Error!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("2627", r.Error!, StringComparison.Ordinal);
            });

            await using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                var appCount = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.CandidateApplications WHERE UserId = @UserId AND ExamPeriodId = @ExamId",
                    new { UserId = duplicateUser, ExamId = Exam1Id });
                Assert.Equal(1, appCount);
            }

            Assert.Equal(51, await GetMaxCandidateNoAsync(databaseName, Exam1Id));

            var exam1Snapshot = await GetCandidateNosAsync(databaseName, Exam1Id);
            var users2 = await InsertSyntheticCandidatesAsync(databaseName, count: 20, idOffset: 300);
            var barrier2 = new Barrier(users2.Count);
            var results2 = new ConcurrentBag<int>();
            var tasks2 = users2.Select(userId => Task.Run(async () =>
            {
                barrier2.SignalAndWait(TimeSpan.FromSeconds(30));
                var result = await sut.ApplyAsync(userId, Model(Exam2Id, Option2Id), "127.0.0.1", $"exam2-{userId:N}");
                Assert.True(result.Success, result.ErrorMessage);
                results2.Add(result.Data!.CandidateNo);
            })).ToArray();
            await Task.WhenAll(tasks2).WaitAsync(TimeSpan.FromMinutes(1));
            Assert.Equal(Enumerable.Range(1, 20), results2.OrderBy(n => n).ToArray());
            Assert.Equal(exam1Snapshot, await GetCandidateNosAsync(databaseName, Exam1Id));

            await InstallForceRollbackTriggerAsync(databaseName);
            var beforeMax = await GetMaxCandidateNoAsync(databaseName, Exam1Id);
            var rollbackUser = (await InsertSyntheticCandidatesAsync(databaseName, count: 1, idOffset: 400)).Single();
            var threw = false;
            try
            {
                await sut.ApplyAsync(rollbackUser, Model(Exam1Id, Option1Id), "127.0.0.1", ForceRollbackCorrelation);
            }
            catch (SqlException)
            {
                threw = true;
            }

            Assert.True(threw);
            await using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM dbo.CandidateApplications WHERE UserId = @UserId",
                    new { UserId = rollbackUser }));
            }

            Assert.Equal(beforeMax, await GetMaxCandidateNoAsync(databaseName, Exam1Id));

            var recoveryUser = (await InsertSyntheticCandidatesAsync(databaseName, count: 1, idOffset: 401)).Single();
            var recovery = await sut.ApplyAsync(recoveryUser, Model(Exam1Id, Option1Id), "127.0.0.1", "recovery-ok");
            Assert.True(recovery.Success, recovery.ErrorMessage);
            Assert.Equal(beforeMax + 1, recovery.Data!.CandidateNo);

            Assert.True(await RunLoadRoundsAsync(databaseName, sut, rounds: 5, parallel: 50));

            if (baseline is not null)
            {
                var after = await CaptureDevelopmentSnapshotAsync();
                Assert.NotNull(after);
                baseline.AssertUnchanged(after!);
            }
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
            await CleanupLeftoverAuditDatabasesAsync();
        }
    }

    private static CreateApplicationViewModel Model(Guid examId, Guid optionId) => new()
    {
        ExamPeriodId = examId,
        SelectedPreferenceOptionIds = new List<Guid> { optionId }
    };

    private static async Task<bool> RunLoadRoundsAsync(
        string databaseName,
        CandidateApplicationService sut,
        int rounds,
        int parallel)
    {
        for (var round = 0; round < rounds; round++)
        {
            var examId = Guid.NewGuid();
            var optionId = Guid.NewGuid();
            await SeedExtraExamAsync(databaseName, examId, optionId, $"Load Round {round}");
            var users = await InsertSyntheticCandidatesAsync(databaseName, parallel, idOffset: 1000 + round * 1000);
            var barrier = new Barrier(parallel);
            var numbers = new ConcurrentBag<int>();
            var tasks = users.Select(userId => Task.Run(async () =>
            {
                barrier.SignalAndWait(TimeSpan.FromSeconds(45));
                var result = await sut.ApplyAsync(userId, Model(examId, optionId), "127.0.0.1", $"load-{round}-{userId:N}");
                if (!result.Success || result.Data is null)
                    throw new InvalidOperationException(result.ErrorMessage ?? "load apply failed");
                numbers.Add(result.Data.CandidateNo);
            })).ToArray();

            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromMinutes(2));
            var ordered = numbers.OrderBy(n => n).ToArray();
            if (ordered.Length != parallel || ordered.Distinct().Count() != parallel)
                return false;
            if (!ordered.SequenceEqual(Enumerable.Range(1, parallel)))
                return false;
            await AssertNoOrphansAsync(databaseName, examId, expectedApplications: parallel);
        }

        return true;
    }

    private static CandidateApplicationService BuildApplicationService(string connectionString)
    {
        IDbConnectionFactory factory = new SqlConnectionFactory(connectionString);
        var appRepo = new CandidateApplicationRepository(factory, new CandidateNumberService());
        return new CandidateApplicationService(
            new ExamPeriodRepository(factory),
            new ExamPreferenceOptionRepository(factory),
            appRepo,
            new Mock<IAuditService>().Object);
    }

    private static async Task SeedBaseAsync(string databaseName)
    {
        await using var conn = new SqlConnection(BuildConnectionString(databaseName));
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
INSERT INTO dbo.Users
(Id, RoleId, TcNo, IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber,
 NationalityCountryCode, PasswordHash, Email, FirstName, LastName, SecurityStamp)
VALUES
(@Id, @RoleId, '11111111110', 1, '11111111110', '11111111110',
 'TR', 'TEST_HASH', 'audit-admin@example.test', N'Audit', N'Admin', NEWID());",
            new { Id = SuperAdminId, RoleId = DomainConstants.RoleIds.SuperAdmin });
        await SeedExtraExamAsync(databaseName, Exam1Id, Option1Id, "Concurrency Exam 1", conn);
        await SeedExtraExamAsync(databaseName, Exam2Id, Option2Id, "Concurrency Exam 2", conn);
    }

    private static async Task SeedExtraExamAsync(
        string databaseName,
        Guid examId,
        Guid optionId,
        string title,
        SqlConnection? existing = null)
    {
        var owns = existing is null;
        var conn = existing ?? new SqlConnection(BuildConnectionString(databaseName));
        if (owns)
            await conn.OpenAsync();
        try
        {
            await conn.ExecuteAsync(@"
IF NOT EXISTS (SELECT 1 FROM dbo.ExamPeriods WHERE Id = @ExamId)
BEGIN
    INSERT INTO dbo.ExamPeriods
    (Id, Title, [Description], StartDate, EndDate, MaxPreferences, IsActive, IsClosed, IsDeleted, CreatedByUserId)
    VALUES
    (@ExamId, @Title, N'Sentetik concurrency sınavı', DATEADD(DAY, -1, SYSDATETIME()), DATEADD(DAY, 30, SYSDATETIME()),
     3, 1, 0, 0, @AdminId);
    INSERT INTO dbo.ExamPeriodCounters (ExamPeriodId, LastCandidateNo) VALUES (@ExamId, 0);
    INSERT INTO dbo.ExamPreferenceOptions (Id, ExamPeriodId, PreferenceName, DisplayOrder, IsActive)
    VALUES (@OptionId, @ExamId, N'Tercih A', 1, 1);
END", new { ExamId = examId, OptionId = optionId, Title = title, AdminId = SuperAdminId });
        }
        finally
        {
            if (owns)
                await conn.DisposeAsync();
        }
    }

    private static async Task<List<Guid>> InsertSyntheticCandidatesAsync(string databaseName, int count, int idOffset)
    {
        await using var conn = new SqlConnection(BuildConnectionString(databaseName));
        await conn.OpenAsync();
        var ids = new List<Guid>(count);
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            var serial = idOffset + i;
            var identity = (90000000000L + serial).ToString(CultureInfo.InvariantCulture);
            await conn.ExecuteAsync(@"
INSERT INTO dbo.Users
(Id, RoleId, TcNo, IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber,
 NationalityCountryCode, PasswordHash, Email, FirstName, LastName, Phone, SecurityStamp)
VALUES
(@Id, @RoleId, @Identity, 1, @Identity, @Identity,
 'TR', 'TEST_HASH', @Email, @FirstName, N'Test', N'+905551112233', NEWID());",
                new
                {
                    Id = id,
                    RoleId = DomainConstants.RoleIds.Candidate,
                    Identity = identity,
                    Email = $"candidate{serial}@example.test",
                    FirstName = $"Aday{serial}"
                });
            ids.Add(id);
        }

        return ids;
    }

    private static async Task InstallForceRollbackTriggerAsync(string databaseName)
    {
        await using var conn = new SqlConnection(BuildConnectionString(databaseName));
        await conn.OpenAsync();
        await conn.ExecuteAsync($@"
CREATE OR ALTER TRIGGER dbo.trg_CandidateNoAudit_ForceRollback
ON dbo.AuditLogs AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE CorrelationId = N'{ForceRollbackCorrelation}')
        THROW 50099, N'Forced audit rollback for CandidateNo concurrency test.', 1;
END;");
    }

    private static async Task AssertNoOrphansAsync(string databaseName, Guid examPeriodId, int expectedApplications)
    {
        await using var conn = new SqlConnection(BuildConnectionString(databaseName));
        await conn.OpenAsync();
        Assert.Equal(expectedApplications, await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.CandidateApplications WHERE ExamPeriodId = @ExamPeriodId",
            new { ExamPeriodId = examPeriodId }));
        Assert.Equal(expectedApplications, await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1) FROM dbo.CandidateSelectedPreferences p
INNER JOIN dbo.CandidateApplications a ON a.Id = p.ApplicationId
WHERE a.ExamPeriodId = @ExamPeriodId", new { ExamPeriodId = examPeriodId }));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1) FROM dbo.CandidateSelectedPreferences p
WHERE NOT EXISTS (SELECT 1 FROM dbo.CandidateApplications a WHERE a.Id = p.ApplicationId)"));
    }

    private static async Task<int> GetMaxCandidateNoAsync(string databaseName, Guid examPeriodId)
    {
        await using var conn = new SqlConnection(BuildConnectionString(databaseName));
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT ISNULL(MAX(CandidateNo), 0) FROM dbo.CandidateApplications WHERE ExamPeriodId = @ExamPeriodId",
            new { ExamPeriodId = examPeriodId });
    }

    private static async Task<int[]> GetCandidateNosAsync(string databaseName, Guid examPeriodId)
    {
        await using var conn = new SqlConnection(BuildConnectionString(databaseName));
        await conn.OpenAsync();
        return (await conn.QueryAsync<int>(
            "SELECT CandidateNo FROM dbo.CandidateApplications WHERE ExamPeriodId = @ExamPeriodId ORDER BY CandidateNo",
            new { ExamPeriodId = examPeriodId })).ToArray();
    }

    private static async Task CreateEmptyDatabaseAsync(string databaseName)
    {
        Assert.StartsWith(AuditPrefix, databaseName, StringComparison.OrdinalIgnoreCase);
        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync();
        await master.ExecuteAsync($"CREATE DATABASE [{databaseName}]");
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        if (!databaseName.StartsWith(AuditPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to drop non-audit database: {databaseName}");

        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync();
        await master.ExecuteAsync($@"
IF DB_ID(N'{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END");
    }

    private static async Task CleanupLeftoverAuditDatabasesAsync()
    {
        await using var master = new SqlConnection(BuildConnectionString("master"));
        await master.OpenAsync();
        var names = (await master.QueryAsync<string>(
            "SELECT name FROM sys.databases WHERE name LIKE 'OYS_CandidateNoAudit[_]%' ESCAPE '\\'")).ToList();
        foreach (var name in names)
            await DropDatabaseAsync(name);
    }

    private static async Task ApplySchemaScriptsAsync(string databaseName)
    {
        var root = LocateDatabaseRoot();
        var scripts = new List<string> { Path.Combine(root, "OzelYetenekSinavSistemi.sql") };
        scripts.AddRange(Directory.GetFiles(Path.Combine(root, "migrations"), "*.sql")
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase));
        foreach (var script in scripts)
            await RunSqlCmdAsync(databaseName, script);
    }

    private static async Task RunSqlCmdAsync(string databaseName, string scriptPath)
    {
        var builder = new SqlConnectionStringBuilder(BuildConnectionString(databaseName));
        var args = new List<string> { "-S", builder.DataSource, "-d", databaseName, "-i", $"\"{scriptPath}\"", "-b", "-I" };
        if (builder.IntegratedSecurity)
            args.Add("-E");
        else
        {
            args.Add("-U");
            args.Add(builder.UserID);
            args.Add("-P");
            args.Add(builder.Password);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "sqlcmd",
            Arguments = string.Join(' ', args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("sqlcmd başlatılamadı.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"sqlcmd failed for {Path.GetFileName(scriptPath)}: {stderr} {stdout}");
    }

    private static async Task<bool> CanConnectToSqlServerAsync()
    {
        try
        {
            await using var connection = new SqlConnection(BuildConnectionString("master"));
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildConnectionString(string initialCatalog)
    {
        var baseConnection = Environment.GetEnvironmentVariable("OYS_TEST_CONNECTION")
            ?? @"Server=.\SQLEXPRESS;Database=OzelYetenekSinavSistemi;Trusted_Connection=True;TrustServerCertificate=True;";
        return new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = initialCatalog }.ConnectionString;
    }

    private static async Task<DevSnapshot?> CaptureDevelopmentSnapshotAsync()
    {
        try
        {
            await using var connection = new SqlConnection(BuildConnectionString("OzelYetenekSinavSistemi"));
            await connection.OpenAsync();
            return await connection.QuerySingleAsync<DevSnapshot>(@"
SELECT
    (SELECT COUNT(1) FROM sys.tables) AS TableCount,
    (SELECT COUNT(1) FROM dbo.Users) AS Users,
    (SELECT COUNT(1) FROM dbo.CandidateApplications) AS Applications,
    (SELECT COUNT(1) FROM dbo.ExamPeriodCounters) AS Counters,
    (SELECT ISNULL(MAX(CAST(CreatedDate AS BIGINT)), 0) FROM dbo.Users) AS UsersMaxTicks;");
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
        public int Applications { get; init; }
        public int Counters { get; init; }
        public long UsersMaxTicks { get; init; }

        public void AssertUnchanged(DevSnapshot after)
        {
            Assert.Equal(TableCount, after.TableCount);
            Assert.Equal(Users, after.Users);
            Assert.Equal(Applications, after.Applications);
            Assert.Equal(Counters, after.Counters);
            Assert.Equal(UsersMaxTicks, after.UsersMaxTicks);
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

[CollectionDefinition(nameof(CandidateNoConcurrencyCollection), DisableParallelization = true)]
public sealed class CandidateNoConcurrencyCollection;

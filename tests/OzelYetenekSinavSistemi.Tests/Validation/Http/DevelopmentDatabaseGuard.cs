using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Tests.TestSupport;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

/// <summary>
/// HTTP test fixture'ları aktifken geliştirme veritabanı aggregate sayılarının değişmediğini doğrular.
/// </summary>
internal static class DevelopmentDatabaseGuard
{
    private static readonly object Sync = new();
    private static int _activeFixtures;
    private static DevelopmentDatabaseSnapshot? _baseline;

    internal static async Task EnterAsync()
    {
        bool captureBaseline;
        lock (Sync)
        {
            _activeFixtures++;
            captureBaseline = _activeFixtures == 1;
        }

        if (captureBaseline)
            _baseline = await DevelopmentDatabaseSnapshot.TryCaptureAsync().ConfigureAwait(false);
    }

    internal static async Task ExitAsync()
    {
        int remaining;
        lock (Sync)
        {
            remaining = --_activeFixtures;
        }

        if (remaining > 0)
            return;

        if (_baseline is null)
            return;

        var after = await DevelopmentDatabaseSnapshot.TryCaptureAsync().ConfigureAwait(false);
        if (after is null)
            return;

        _baseline.AssertUnchanged(after);
        _baseline = null;
    }
}

internal sealed class DevelopmentDatabaseSnapshot
{
    public int Users { get; init; }
    public int Roles { get; init; }
    public int ExamPeriods { get; init; }
    public int ExamPreferenceOptions { get; init; }
    public long UsersMaxTicks { get; init; }
    public long ExamPeriodsMaxTicks { get; init; }

    internal static async Task<DevelopmentDatabaseSnapshot?> TryCaptureAsync()
    {
        try
        {
            var builder = TestSqlServerConnection.CreateBuilder("OzelYetenekSinavSistemi");

            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            const string sql = @"
SELECT
    (SELECT COUNT(1) FROM dbo.Users) AS Users,
    (SELECT COUNT(1) FROM dbo.Roles) AS Roles,
    (SELECT COUNT(1) FROM dbo.ExamPeriods) AS ExamPeriods,
    (SELECT COUNT(1) FROM dbo.ExamPreferenceOptions) AS ExamPreferenceOptions,
    (SELECT ISNULL(MAX(CAST(CreatedDate AS BIGINT)), 0) FROM dbo.Users) AS UsersMaxTicks,
    (SELECT ISNULL(MAX(CAST(CreatedDate AS BIGINT)), 0) FROM dbo.ExamPeriods) AS ExamPeriodsMaxTicks;";

            return await connection.QuerySingleAsync<DevelopmentDatabaseSnapshot>(sql).ConfigureAwait(false);
        }
        catch (SqlException)
        {
            return null;
        }
    }

    internal void AssertUnchanged(DevelopmentDatabaseSnapshot after)
    {
        if (Users != after.Users)
            throw new InvalidOperationException($"Users satır sayısı değişti: {Users} -> {after.Users}.");
        if (Roles != after.Roles)
            throw new InvalidOperationException($"Roles satır sayısı değişti: {Roles} -> {after.Roles}.");
        if (ExamPeriods != after.ExamPeriods)
            throw new InvalidOperationException($"ExamPeriods satır sayısı değişti: {ExamPeriods} -> {after.ExamPeriods}.");
        if (ExamPreferenceOptions != after.ExamPreferenceOptions)
            throw new InvalidOperationException(
                $"ExamPreferenceOptions satır sayısı değişti: {ExamPreferenceOptions} -> {after.ExamPreferenceOptions}.");
        if (UsersMaxTicks != after.UsersMaxTicks)
            throw new InvalidOperationException("Users CreatedDate aggregate değişti.");
        if (ExamPeriodsMaxTicks != after.ExamPeriodsMaxTicks)
            throw new InvalidOperationException("ExamPeriods CreatedDate aggregate değişti.");
    }
}

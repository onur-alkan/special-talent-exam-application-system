using Microsoft.Data.SqlClient;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class TestDatabaseSafetyTests
{
    [Fact]
    public void EnsureIsolatedTestDatabase_RejectsDevelopmentCatalog()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(
                @"Server=.\SQLEXPRESS;Database=OzelYetenekSinavSistemi;Trusted_Connection=True;TrustServerCertificate=True;"));

        Assert.Contains("Yasak", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureIsolatedTestDatabase_RejectsEmptyCatalog()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(
                @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True;"));

        Assert.Contains("Initial Catalog", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureIsolatedTestDatabase_RejectsProductionLikeCatalog()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(
                @"Server=prod-sql;Database=OYS;User Id=test;Password=secret;TrustServerCertificate=True;"));

        Assert.Contains("Yasak", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureIsolatedTestDatabase_AcceptsValidationHttpPrefix()
    {
        var connectionString =
            $@"Server=.\SQLEXPRESS;Database={TestDatabaseSafetyGuard.AllowedPrefix}abc123;Trusted_Connection=True;TrustServerCertificate=True;";

        var exception = Record.Exception(() => TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(connectionString));
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureIsolatedTestDatabase_RejectsUnknownPrefix()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(
                @"Server=.\SQLEXPRESS;Database=MyRandomTestDb;Trusted_Connection=True;TrustServerCertificate=True;"));

        Assert.Contains(TestDatabaseSafetyGuard.AllowedPrefix, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsStaleTestDatabaseName_DetectsOldValidationHttpDatabase()
    {
        var oldName = $"{TestDatabaseSafetyGuard.AllowedPrefix}20200101000000_{Guid.NewGuid():N}";
        Assert.True(ValidationHttpTestDatabase.IsStaleTestDatabaseName(oldName));
    }

    [Fact]
    public void IsStaleTestDatabaseName_RejectsRecentValidationHttpDatabase()
    {
        var recentName = $"{TestDatabaseSafetyGuard.AllowedPrefix}{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        Assert.False(ValidationHttpTestDatabase.IsStaleTestDatabaseName(recentName));
    }
}

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class LeftoverTestDatabaseTests
{
    [Fact]
    public async Task ValidationHttpTestDatabase_Dispose_RemovesDatabase()
    {
        var database = new ValidationHttpTestDatabase();
        await database.InitializeAsync();
        var databaseName = database.DatabaseName;

        await database.DisposeAsync();

        var leftovers = await ValidationHttpTestDatabase.ListLeftoverTestDatabasesAsync();
        Assert.DoesNotContain(databaseName, leftovers);
    }

    [Fact]
    public async Task SqlServer_HasNoLegacyValAdminDatabases()
    {
        await ValidationHttpTestDatabase.CleanupLegacyValAdminDatabasesAsync();

        var leftovers = await ValidationHttpTestDatabase.ListLeftoverTestDatabasesAsync();
        var legacy = leftovers.Where(name => name.StartsWith("OYS_ValAdmin_", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(
            legacy.Count == 0,
            $"Eski OYS_ValAdmin_ artık veritabanı kaldı: {string.Join(", ", legacy)}");
    }
}

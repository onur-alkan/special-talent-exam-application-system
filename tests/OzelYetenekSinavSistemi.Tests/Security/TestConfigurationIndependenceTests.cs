using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using OzelYetenekSinavSistemi.Tests.Validation.Http;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class TestConfigurationIndependenceTests
{
    [Fact]
    public void Gitignore_ExcludesLocalDevelopmentJson()
    {
        var gitignore = File.ReadAllText(FindUnder(".gitignore"));
        Assert.Contains("appsettings.Development.json", gitignore, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentExample_DefinesTrackedContractWithoutSecrets()
    {
        var webRoot = FindUnder("src", "OzelYetenekSinavSistemi.Web");
        var examplePath = Path.Combine(webRoot, "appsettings.Development.json.example");
        Assert.True(File.Exists(examplePath));

        var json = File.ReadAllText(examplePath);
        Assert.DoesNotContain("\"Password\":", json, StringComparison.Ordinal);
        Assert.Contains("\"DeliveryMode\": \"File\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void WebProject_ExcludesDevelopmentJsonFromPublish()
    {
        var csproj = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "OzelYetenekSinavSistemi.Web.csproj"));
        Assert.Contains("appsettings.Development.json", csproj, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory>Never", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public void EmailSecurityTests_UseTrackedDevelopmentExample()
    {
        var source = File.ReadAllText(FindUnder(
            "tests", "OzelYetenekSinavSistemi.Tests", "Security", "EmailSecurityTests.cs"));
        Assert.Contains("appsettings.Development.json.example", source, StringComparison.Ordinal);
        Assert.DoesNotContain("appsettings.Development.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpTestHost_Starts_WithIsolatedTestDatabase()
    {
        await DevelopmentDatabaseGuard.EnterAsync();
        try
        {
            await using var database = new ValidationHttpTestDatabase();
            await database.InitializeAsync();
            TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(database.ConnectionString);

            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", database.ConnectionString);
                builder.UseSetting("Seed:SuperAdminPassword", $"Tmp!{Guid.NewGuid():N}aA1");
                builder.UseSetting("Seed:SeedTestData", "false");
            });

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var response = await client.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await DevelopmentDatabaseGuard.ExitAsync();
        }
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (Directory.Exists(path) || File.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

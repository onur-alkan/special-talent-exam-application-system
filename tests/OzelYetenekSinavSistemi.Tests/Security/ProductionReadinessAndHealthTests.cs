using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;
using OzelYetenekSinavSistemi.Tests.Validation.Http;
using OzelYetenekSinavSistemi.Web.Configuration;
using OzelYetenekSinavSistemi.Web.Health;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class ProductionReadinessValidationTests
{
    [Fact]
    public void Production_EmptyConnectionString_Fails()
    {
        var errors = new List<string>();
        ProductionReadinessValidator.ValidateConnectionStringCore("", errors);
        Assert.Contains(errors, e => e.Contains("DefaultConnection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_LocalhostConnectionString_Fails()
    {
        var errors = new List<string>();
        ProductionReadinessValidator.ValidateConnectionStringCore(
            "Server=.\\SQLEXPRESS;Database=ProdLike;Trusted_Connection=True;TrustServerCertificate=True;",
            errors);
        Assert.Contains(errors, e => e.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                                     || e.Contains("geliştirme", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_LocalhostPublicUrl_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeEnv(Environments.Production),
                Config(("AllowedHosts", "basvuru.example.edu.tr")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "https://localhost:7102" }));
        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_HttpPublicUrl_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeEnv(Environments.Production),
                Config(("AllowedHosts", "basvuru.example.edu.tr")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "http://basvuru.example.edu.tr" }));
        Assert.Contains("https", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_AllowedHostsWildcard_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeEnv(Environments.Production),
                Config(("AllowedHosts", "*")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "https://basvuru.example.edu.tr" }));
        Assert.Contains("AllowedHosts", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_AllowedHostsOnlyLocalhost_Fails()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurityOptionsValidator.Validate(
                new FakeEnv(Environments.Production),
                Config(("AllowedHosts", "localhost;127.0.0.1")),
                new ReverseProxyOptions(),
                new PublicUrlOptions { BaseUrl = "https://basvuru.example.edu.tr" }));
        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_SeedTestDataTrue_Fails()
    {
        var result = new SeedOptionsValidator(new FakeEnv(Environments.Production))
            .Validate(null, new SeedOptions { SeedTestData = true });
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("SeedTestData", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_EmailDisabled_Fails()
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(
            new EmailOptions { DeliveryMode = EmailDeliveryMode.Disabled, FromAddress = "a@b.com", FromName = "X" },
            isProduction: true,
            errors);
        Assert.Contains(errors, e => e.Contains("Smtp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_MissingSmtpHost_Fails()
    {
        var errors = new List<string>();
        EmailOptionsValidator.ValidateCore(
            new EmailOptions
            {
                DeliveryMode = EmailDeliveryMode.Smtp,
                FromAddress = "a@b.com",
                FromName = "X",
                Host = "",
                Port = 587
            },
            isProduction: true,
            errors);
        Assert.Contains(errors, e => e.Contains("Host", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Development_LocalhostHttps_StillAccepted()
    {
        HostingSecurityOptionsValidator.Validate(
            new FakeEnv(Environments.Development),
            Config(("AllowedHosts", "localhost;127.0.0.1")),
            new ReverseProxyOptions { Enabled = false },
            new PublicUrlOptions { BaseUrl = "https://localhost:7102" });
    }

    [Fact]
    public void Program_RegistersHealthEndpoints_AndKeepsCookiePolicies()
    {
        var program = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Program.cs");
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program, StringComparison.Ordinal);
        Assert.Contains("UseDeveloperExceptionPage()", program, StringComparison.Ordinal);
        Assert.Contains("IsDevelopment()", program, StringComparison.Ordinal);
        Assert.Contains("UseHsts()", program, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(program, "Cookie.SecurePolicy = CookieSecurePolicy.Always"));
        Assert.Equal(2, CountOccurrences(program, "Cookie.HttpOnly = true"));
        Assert.Equal(2, CountOccurrences(program, "Cookie.SameSite = SameSiteMode.Lax"));
    }

    [Fact]
    public void ProductionExample_HasPlaceholders_NotLoadableSecretsFile()
    {
        var example = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "appsettings.Production.example.json");
        Assert.Contains("REPLACE_WITH_", example, StringComparison.Ordinal);
        Assert.Contains("\"SeedTestData\": false", example, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin!2345", example, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            FindRoot(), "src", "OzelYetenekSinavSistemi.Web", "appsettings.Production.json")));
    }

    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    private static string ReadProjectFile(params string[] parts)
    {
        var path = Path.Combine(new[] { FindRoot() }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OzelYetenekSinavSistemi.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException();
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

internal sealed class FakeEnv : IHostEnvironment
{
    public FakeEnv(string name) => EnvironmentName = name;
    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

[Collection(nameof(RegisterFormLiveCollection))]
public sealed class HealthEndpointLiveTests : RegisterFormLiveTestBase
{
    public HealthEndpointLiveTests(RegisterFormLiveFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task HealthLive_Returns200_WithoutDependencyDetails()
    {
        var client = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);
        AssertSafeHealthBody(body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.False(doc.RootElement.TryGetProperty("checks", out _));
    }

    [Fact]
    public async Task HealthReady_HealthyEnvironment_Returns200_WithSafeChecks()
    {
        var client = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);
        AssertSafeHealthBody(body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task SqlSelectOneHealthCheck_UnreachableDb_IsUnhealthy()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=127.0.0.1,1;Database=OYS_Health_Missing;User Id=oys;Password=not-a-secret;Connect Timeout=1;TrustServerCertificate=True"
            })
            .Build();

        var check = new SqlSelectOneHealthCheck(config, new FakeEnv(Environments.Development));
        var result = await check.CheckHealthAsync(new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("sql", check, failureStatus: null, tags: null)
        });

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("sql_unavailable", result.Description);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task HealthReady_BrokenPhotoStorage_Returns503()
    {
        var blocker = Path.Combine(Path.GetTempPath(), $"oys-health-block-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(blocker, "not-a-directory");
        try
        {
            var factory = Fixture.Factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("PhotoUpload:StorageRootPath", blocker);
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.GetAsync("/health/ready");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            AssertSafeHealthBody(body);
            Assert.DoesNotContain(blocker, body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(blocker))
                File.Delete(blocker);
        }
    }

    [Fact]
    public async Task DataProtectionKeysHealthCheck_InaccessiblePath_IsUnhealthy()
    {
        var blocker = Path.Combine(Path.GetTempPath(), $"oys-dp-block-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(blocker, "not-a-directory");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataProtection:KeysPath"] = blocker,
                    ["DataProtection:ApplicationName"] = "OzelYetenekSinavSistemi"
                })
                .Build();

            var check = new DataProtectionKeysHealthCheck(new FakeEnv(Environments.Development), config);
            var result = await check.CheckHealthAsync(new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("dataprotection_keys", check, failureStatus: null, tags: null)
            });

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Equal("dataprotection_keys_unavailable", result.Description);
            Assert.DoesNotContain(blocker, result.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Null(result.Exception);
        }
        finally
        {
            if (File.Exists(blocker))
                File.Delete(blocker);
        }
    }

    [Fact]
    public async Task HealthReady_WhenSqlCheckUnhealthy_Returns503_WithoutSecrets()
    {
        // Host startup için gerçek DB gerekir; readiness 503’ü SQL check’i izole değiştirerek doğrulanır.
        var factory = Fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration(
                        "sql",
                        new UnreachableSqlHealthCheckStub(),
                        failureStatus: HealthStatus.Unhealthy,
                        tags: new[] { HealthCheckTags.Ready }));
                });
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        AssertNoStore(response);
        AssertSafeHealthBody(body);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class UnreachableSqlHealthCheckStub : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("sql_unavailable"));
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore == true
                    || response.Headers.TryGetValues("Cache-Control", out var values)
                    && values.Any(v => v.Contains("no-store", StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertSafeHealthBody(string body)
    {
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Connection String", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Source", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", body, StringComparison.Ordinal);
        Assert.DoesNotContain("/App_Data/", body, StringComparison.OrdinalIgnoreCase);
    }
}

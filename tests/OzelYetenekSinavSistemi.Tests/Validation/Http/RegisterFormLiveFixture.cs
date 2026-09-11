using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class RegisterFormLiveFixture : IAsyncLifetime
{
    private readonly ValidationHttpTestDatabase _database = new();
    private readonly string _seedPassword = $"Tmp!{Guid.NewGuid():N}aA1";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ConnectionString => _database.ConnectionString;

    public async Task InitializeAsync()
    {
        await DevelopmentDatabaseGuard.EnterAsync().ConfigureAwait(false);
        await _database.InitializeAsync().ConfigureAwait(false);
        TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(_database.ConnectionString);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);
            builder.UseSetting("Seed:SuperAdminPassword", _seedPassword);
            builder.UseSetting("Seed:SeedTestData", "false");
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<SessionOptions>(options =>
                {
                    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                });
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                });
            });
        });
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await _database.DisposeAsync().ConfigureAwait(false);
        await DevelopmentDatabaseGuard.ExitAsync().ConfigureAwait(false);
    }
}

[CollectionDefinition(nameof(RegisterFormLiveCollection))]
public sealed class RegisterFormLiveCollection : ICollectionFixture<RegisterFormLiveFixture>
{
}

[Collection(nameof(RegisterFormLiveCollection))]
public abstract class RegisterFormLiveTestBase
{
    protected RegisterFormLiveTestBase(RegisterFormLiveFixture fixture) => Fixture = fixture;

    protected RegisterFormLiveFixture Fixture { get; }
}

using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Tests.Validation.Http;

namespace OzelYetenekSinavSistemi.Tests.Validation.Admin;

public sealed class ValidationAdminFixture : IAsyncLifetime
{
    internal static readonly Guid SuperAdminUserId = new("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    internal static readonly Guid ApplicationManagerUserId = new("EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE");
    internal static readonly Guid CandidateUserId = new("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC");
    internal static readonly Guid TestApplicationId = new("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD");
    internal static readonly Guid TestExamPeriodId = new("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    private readonly ValidationHttpTestDatabase _database = new();
    private readonly string _superAdminPassword = $"Tmp!{Guid.NewGuid():N}aA1";
    private readonly string _applicationManagerPassword = $"Tmp!{Guid.NewGuid():N}aA1";
    private readonly string _candidatePassword = $"Tmp!{Guid.NewGuid():N}aA1";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public HttpClient SuperAdminClient { get; private set; } = null!;

    public HttpClient ApplicationManagerClient { get; private set; } = null!;

    public HttpClient CandidateClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await DevelopmentDatabaseGuard.EnterAsync().ConfigureAwait(false);
        await _database.InitializeAsync().ConfigureAwait(false);
        TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(_database.ConnectionString);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);
            builder.UseSetting("Seed:SuperAdminPassword", _superAdminPassword);
            builder.UseSetting("Seed:SeedTestData", "true");
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

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ValidationAdminTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ValidationAdminTestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, ValidationAdminTestAuthHandler>(
                    ValidationAdminTestAuthHandler.SchemeName,
                    _ => { });
            });
        });

        using var scope = Factory.Services.CreateScope();
        await SeedAdditionalUsersAsync(scope.ServiceProvider).ConfigureAwait(false);

        SuperAdminClient = ValidationAdminHttpTestSupport.CreateClientForRole(Factory, "SuperAdmin");
        ApplicationManagerClient = ValidationAdminHttpTestSupport.CreateClientForRole(Factory, "ApplicationManager");
        CandidateClient = ValidationAdminHttpTestSupport.CreateClientForRole(Factory, "Candidate");
    }

    public async Task DisposeAsync()
    {
        SuperAdminClient?.Dispose();
        ApplicationManagerClient?.Dispose();
        CandidateClient?.Dispose();
        Factory?.Dispose();
        await _database.DisposeAsync().ConfigureAwait(false);
        await DevelopmentDatabaseGuard.ExitAsync().ConfigureAwait(false);
    }

    private async Task SeedAdditionalUsersAsync(IServiceProvider services)
    {
        var passwordService = services.GetRequiredService<IPasswordService>();
        var connectionFactory = services.GetRequiredService<IDbConnectionFactory>();
        using var connection = await connectionFactory.CreateOpenConnectionAsync().ConfigureAwait(false);

        const string insertUserSql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @Id)
INSERT INTO dbo.Users
    (Id, RoleId, TcNo, IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber, NationalityCountryCode,
     PasswordHash, Email, FirstName, LastName, Nationality, IsActive, MustChangePassword, SecurityStamp, CreatedDate)
VALUES
    (@Id, @RoleId, @TcNo, 1, @TcNo, @TcNo, 'TR',
     @PasswordHash, @Email, @FirstName, @LastName, N'T.C.', 1, 0, NEWID(), SYSDATETIME());";

        await connection.ExecuteAsync(insertUserSql, new
        {
            Id = ApplicationManagerUserId,
            RoleId = DomainConstants.RoleIds.ApplicationManager,
            TcNo = "10000000146",
            PasswordHash = passwordService.Hash(_applicationManagerPassword),
            Email = "appmanager@test.local",
            FirstName = "Dönem",
            LastName = "Yöneticisi"
        }).ConfigureAwait(false);

        await connection.ExecuteAsync(insertUserSql, new
        {
            Id = CandidateUserId,
            RoleId = DomainConstants.RoleIds.Candidate,
            TcNo = "19191919190",
            PasswordHash = passwordService.Hash(_candidatePassword),
            Email = "candidate@test.local",
            FirstName = "Test",
            LastName = "Aday"
        }).ConfigureAwait(false);

        const string assignManagerSql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.ExamPeriodManagers WHERE ExamPeriodId = @ExamPeriodId AND ManagerUserId = @ManagerUserId)
    INSERT INTO dbo.ExamPeriodManagers (ExamPeriodId, ManagerUserId, AssignedByUserId, AssignedDate)
    VALUES (@ExamPeriodId, @ManagerUserId, @AssignedByUserId, SYSDATETIME());";

        await connection.ExecuteAsync(assignManagerSql, new
        {
            ExamPeriodId = TestExamPeriodId,
            ManagerUserId = ApplicationManagerUserId,
            AssignedByUserId = SuperAdminUserId
        }).ConfigureAwait(false);

        const string insertApplicationSql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.CandidateApplications WHERE Id = @Id)
BEGIN
    INSERT INTO dbo.CandidateApplications
        (Id, UserId, ExamPeriodId, CandidateNo, VerificationCode, [Status])
    VALUES
        (@Id, @UserId, @ExamPeriodId, 1, 'TEST-VERIFY-001', 1);
END";

        await connection.ExecuteAsync(insertApplicationSql, new
        {
            Id = TestApplicationId,
            UserId = CandidateUserId,
            ExamPeriodId = TestExamPeriodId
        }).ConfigureAwait(false);

        await connection.ExecuteAsync("UPDATE dbo.Users SET MustChangePassword = 0;").ConfigureAwait(false);
    }
}

[CollectionDefinition(nameof(ValidationAdminCollection))]
public sealed class ValidationAdminCollection : ICollectionFixture<ValidationAdminFixture>
{
}

[Collection(nameof(ValidationAdminCollection))]
public abstract class ValidationAdminTestBase
{
    protected ValidationAdminTestBase(ValidationAdminFixture fixture) => Fixture = fixture;

    protected ValidationAdminFixture Fixture { get; }
}

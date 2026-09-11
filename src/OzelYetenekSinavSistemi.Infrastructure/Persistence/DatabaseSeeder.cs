using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Infrastructure.Configuration;

namespace OzelYetenekSinavSistemi.Infrastructure.Persistence;

/// <summary>
/// Uygulama başlangıcında çalışan idempotent seed servisi.
/// Rolleri ve varsayılan SuperAdmin kullanıcısını oluşturur; test verisi isteğe bağlıdır.
/// Parola ASP.NET Core PasswordHasher ile hash'lenir; mükerrer kayıt oluşturmaz.
/// </summary>
public sealed class DatabaseSeeder
{
    private static readonly Guid SuperAdminUserId = new("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    private static readonly Guid TestExamPeriodId = new("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPasswordService _passwordService;
    private readonly SeedOptions _seedOptions;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IDbConnectionFactory connectionFactory,
        IPasswordService passwordService,
        IOptions<SeedOptions> seedOptions,
        ILogger<DatabaseSeeder> logger)
    {
        _connectionFactory = connectionFactory;
        _passwordService = passwordService;
        _seedOptions = seedOptions.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await SeedRolesAsync(connection, cancellationToken).ConfigureAwait(false);
        await SeedSuperAdminAsync(connection, cancellationToken).ConfigureAwait(false);

        if (_seedOptions.SeedTestData)
            await SeedTestExamPeriodAsync(connection, cancellationToken).ConfigureAwait(false);
        else
            _logger.LogInformation("SeedTestData kapalı; test sınav dönemi oluşturulmadı.");

        _logger.LogInformation("Veritabanı seed işlemi tamamlandı.");
    }

    private static async Task SeedRolesAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Id = @Id)
    INSERT INTO dbo.Roles (Id, Name, [Description]) VALUES (@Id, @Name, @Description);";

        var roles = new[]
        {
            new { Id = DomainConstants.RoleIds.SuperAdmin, Name = DomainConstants.RoleNames.SuperAdmin, Description = "Sistemin tüm modüllerine tam erişim" },
            new { Id = DomainConstants.RoleIds.ApplicationManager, Name = DomainConstants.RoleNames.ApplicationManager, Description = "Sadece atanan sınav dönemlerini yönetir" },
            new { Id = DomainConstants.RoleIds.Candidate, Name = DomainConstants.RoleNames.Candidate, Description = "Aday - kendi başvurularını yönetir" }
        };

        foreach (var role in roles)
            await connection.ExecuteAsync(new CommandDefinition(sql, role, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task SeedSuperAdminAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        const string existsSql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @Id) THEN 1 ELSE 0 END;";
        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(existsSql, new { Id = SuperAdminUserId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (exists)
            return;

        if (string.IsNullOrWhiteSpace(_seedOptions.SuperAdminPassword))
        {
            throw new InvalidOperationException(
                "İlk SuperAdmin kullanıcısını oluşturmak için Seed:SuperAdminPassword User Secrets veya ortam değişkeni üzerinden yapılandırılmalıdır.");
        }

        var passwordHash = _passwordService.Hash(_seedOptions.SuperAdminPassword);

        const string insertSql = @"
INSERT INTO dbo.Users
    (Id, RoleId, TcNo, IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber, NationalityCountryCode,
     PasswordHash, Email, FirstName, LastName, Nationality, IsActive, MustChangePassword, SecurityStamp, CreatedDate)
VALUES
    (@Id, @RoleId, @TcNo, 1, @TcNo, @TcNo, 'TR',
     @PasswordHash, @Email, @FirstName, @LastName, N'T.C.', 1, 1, NEWID(), SYSDATETIME());";

        await connection.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Id = SuperAdminUserId,
            RoleId = DomainConstants.RoleIds.SuperAdmin,
            TcNo = _seedOptions.SuperAdminTcNo,
            PasswordHash = passwordHash,
            Email = _seedOptions.SuperAdminEmail,
            FirstName = _seedOptions.SuperAdminFirstName,
            LastName = _seedOptions.SuperAdminLastName
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        _logger.LogInformation(
            "Varsayılan SuperAdmin kullanıcısı oluşturuldu. UserId={UserId}, Role={Role}",
            SuperAdminUserId,
            DomainConstants.RoleNames.SuperAdmin);
    }

    private async Task SeedTestExamPeriodAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        const string existsSql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.ExamPeriods WHERE Id = @Id) THEN 1 ELSE 0 END;";
        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(existsSql, new { Id = TestExamPeriodId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (exists)
            return;

        const string insertPeriod = @"
INSERT INTO dbo.ExamPeriods (Id, Title, [Description], StartDate, EndDate, MaxPreferences, IsActive, IsClosed, IsDeleted, CreatedByUserId, CreatedDate)
VALUES (@Id, @Title, @Description, @StartDate, @EndDate, @MaxPreferences, 1, 0, 0, @CreatedByUserId, SYSDATETIME());

INSERT INTO dbo.ExamPeriodCounters (ExamPeriodId, LastCandidateNo, UpdatedDate) VALUES (@Id, 0, SYSDATETIME());

INSERT INTO dbo.ExamPreferenceOptions (ExamPeriodId, PreferenceName, DisplayOrder, IsActive)
VALUES (@Id, N'Resim', 1, 1), (@Id, N'Heykel', 2, 1);";

        await connection.ExecuteAsync(new CommandDefinition(insertPeriod, new
        {
            Id = TestExamPeriodId,
            Title = "2026 Resim ve Heykel Bölümü Özel Yetenek Sınavı",
            Description = "Test amaçlı örnek sınav dönemi.",
            StartDate = DateTime.Now.AddDays(-1),
            EndDate = DateTime.Now.AddDays(30),
            MaxPreferences = 2,
            CreatedByUserId = SuperAdminUserId
        }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        _logger.LogInformation("Test sınav dönemi oluşturuldu.");
    }
}

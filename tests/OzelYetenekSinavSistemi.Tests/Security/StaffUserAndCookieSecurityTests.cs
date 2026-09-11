using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.Users;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class CookieAuthenticationSecurityTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Stamp = Guid.NewGuid();

    [Fact]
    public async Task ValidatePrincipal_ActiveUnchangedUser_Continues()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var context = await CreateValidateContextAsync(
            UserId,
            DomainConstants.RoleNames.SuperAdmin,
            Stamp,
            mustChange: false,
            new UserAuthenticationState
            {
                UserId = UserId,
                RoleId = DomainConstants.RoleIds.SuperAdmin,
                IsActive = true,
                MustChangePassword = false,
                SecurityStamp = Stamp
            });

        await events.ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.Principal!.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task ValidatePrincipal_InactiveUser_Rejects()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var context = await CreateValidateContextAsync(
            UserId,
            DomainConstants.RoleNames.ApplicationManager,
            Stamp,
            mustChange: false,
            new UserAuthenticationState
            {
                UserId = UserId,
                RoleId = DomainConstants.RoleIds.ApplicationManager,
                IsActive = false,
                MustChangePassword = false,
                SecurityStamp = Stamp
            });

        await events.ValidatePrincipal(context);
        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_RoleChanged_Rejects()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var context = await CreateValidateContextAsync(
            UserId,
            DomainConstants.RoleNames.ApplicationManager,
            Stamp,
            mustChange: false,
            new UserAuthenticationState
            {
                UserId = UserId,
                RoleId = DomainConstants.RoleIds.Candidate,
                IsActive = true,
                MustChangePassword = false,
                SecurityStamp = Stamp
            });

        await events.ValidatePrincipal(context);
        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_SecurityStampChanged_Rejects()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var context = await CreateValidateContextAsync(
            UserId,
            DomainConstants.RoleNames.SuperAdmin,
            Stamp,
            mustChange: false,
            new UserAuthenticationState
            {
                UserId = UserId,
                RoleId = DomainConstants.RoleIds.SuperAdmin,
                IsActive = true,
                MustChangePassword = false,
                SecurityStamp = Guid.NewGuid()
            });

        await events.ValidatePrincipal(context);
        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_UserNotFound_Rejects()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var context = await CreateValidateContextAsync(
            UserId,
            DomainConstants.RoleNames.SuperAdmin,
            Stamp,
            mustChange: false,
            state: null);

        await events.ValidatePrincipal(context);
        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_InvalidUserId_Rejects()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var context = await CreateValidateContextAsync(
            Guid.Empty,
            DomainConstants.RoleNames.SuperAdmin,
            Stamp,
            mustChange: false,
            new UserAuthenticationState
            {
                UserId = UserId,
                RoleId = DomainConstants.RoleIds.SuperAdmin,
                IsActive = true,
                SecurityStamp = Stamp
            },
            overrideNameIdentifier: "not-a-guid");

        await events.ValidatePrincipal(context);
        Assert.Null(context.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_DatabaseError_RejectsFailClosed()
    {
        var events = new ActiveUserCookieAuthenticationEvents();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetAuthenticationStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var context = await CreateValidateContextAsync(
            UserId,
            DomainConstants.RoleNames.SuperAdmin,
            Stamp,
            mustChange: false,
            state: null,
            repository: repo);

        await events.ValidatePrincipal(context);
        Assert.Null(context.Principal);
    }

    [Fact]
    public void Program_RegistersCookieEventsType()
    {
        var program = File.ReadAllText(FindProgramCs());
        Assert.Contains("EventsType = typeof(ActiveUserCookieAuthenticationEvents)", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<ActiveUserCookieAuthenticationEvents>", program, StringComparison.Ordinal);
    }

    private static async Task<CookieValidatePrincipalContext> CreateValidateContextAsync(
        Guid userId,
        string role,
        Guid stamp,
        bool mustChange,
        UserAuthenticationState? state,
        Mock<IUserRepository>? repository = null,
        string? overrideNameIdentifier = null)
    {
        repository ??= new Mock<IUserRepository>();
        repository.Setup(r => r.GetAuthenticationStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        var services = new ServiceCollection();
        services.AddSingleton(repository.Object);
        services.AddLogging();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();
        var sp = services.BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = sp };
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, overrideNameIdentifier ?? userId.ToString()),
            new(ClaimTypes.Role, role),
            new(DomainConstants.ClaimTypesCustom.SecurityStamp, stamp.ToString("D")),
            new("MustChangePassword", mustChange ? "true" : "false")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(CookieAuthenticationHandler));

        var options = new CookieAuthenticationOptions();
        var context = new CookieValidatePrincipalContext(
            http,
            new AuthenticationScheme(scheme.Name, scheme.DisplayName, scheme.HandlerType),
            options,
            new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme));

        await Task.CompletedTask;
        return context;
    }

    private static string FindProgramCs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Web", "Program.cs");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Program.cs");
    }
}

public sealed class UserManagementSecurityTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordService> _passwords = new();
    private readonly Mock<ITurkishIdentityNumberValidator> _tc = new();
    private readonly Mock<ISensitiveDataMaskingService> _masking = new();

    private UserManagementService CreateSut() =>
        new(_users.Object, _passwords.Object, _tc.Object, _masking.Object);

    [Fact]
    public void UserManagementController_RequiresSuperAdminOnly()
    {
        var attr = typeof(UserManagementController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("SuperAdminOnly", attr!.Policy);
    }

    [Fact]
    public async Task CreateStaff_RejectsCandidateRoleId()
    {
        _tc.Setup(t => t.IsValid(It.IsAny<string>())).Returns(true);
        var model = new CreateStaffUserViewModel
        {
            TcNo = "10000000146",
            Email = "a@b.com",
            FirstName = "Ad",
            LastName = "Soyad",
            RoleId = DomainConstants.RoleIds.Candidate,
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var result = await CreateSut().CreateStaffUserAsync(model, Guid.NewGuid(), null, null);
        Assert.False(result.Success);
        _users.Verify(u => u.CreateStaffUserAtomicAsync(It.IsAny<CreateStaffUserRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaff_RejectsRandomRoleId()
    {
        _tc.Setup(t => t.IsValid(It.IsAny<string>())).Returns(true);
        var model = new CreateStaffUserViewModel
        {
            TcNo = "10000000146",
            Email = "a@b.com",
            FirstName = "Ad",
            LastName = "Soyad",
            RoleId = Guid.NewGuid(),
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var result = await CreateSut().CreateStaffUserAsync(model, Guid.NewGuid(), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateStaff_LastActiveSuperAdmin_MapsMessage()
    {
        _users.Setup(u => u.UpdateStaffUserAtomicAsync(It.IsAny<UpdateStaffUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementWriteResult.Fail(UserManagementWriteStatus.LastActiveSuperAdmin));

        var result = await CreateSut().UpdateStaffUserAsync(
            new EditStaffUserViewModel { Id = Guid.NewGuid(), RoleId = DomainConstants.RoleIds.ApplicationManager, IsActive = false },
            Guid.NewGuid(), null, null);

        Assert.False(result.Success);
        Assert.Contains("Son aktif SuperAdmin", result.ErrorMessage!, StringComparison.Ordinal);
        Assert.DoesNotContain("Sql", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaff_CannotModifyOwnAccount_MapsMessage()
    {
        _users.Setup(u => u.UpdateStaffUserAtomicAsync(It.IsAny<UpdateStaffUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserManagementWriteResult.Fail(UserManagementWriteStatus.CannotModifyOwnAccount));

        var result = await CreateSut().UpdateStaffUserAsync(
            new EditStaffUserViewModel { Id = Guid.NewGuid(), RoleId = DomainConstants.RoleIds.ApplicationManager, IsActive = true },
            Guid.NewGuid(), null, null);

        Assert.False(result.Success);
        Assert.Contains("Kendi hesabınızı", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdatePasswordSql_RenewsSecurityStamp()
    {
        var source = File.ReadAllText(FindFile("UserRepository.cs"));
        Assert.Contains("SecurityStamp = NEWID()", source, StringComparison.Ordinal);
        Assert.Contains("GetAuthenticationStateAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordResetSql_RenewsSecurityStamp()
    {
        var source = File.ReadAllText(FindFile("PasswordResetTransactionService.cs"));
        Assert.Contains("SecurityStamp = NEWID()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IsManagerOfExamPeriod_JoinsUsersAndExamPeriods()
    {
        var source = File.ReadAllText(FindFile("ExamPeriodManagerRepository.cs"));
        Assert.Contains("u.IsActive = 1", source, StringComparison.Ordinal);
        Assert.Contains("u.RoleId = @ApplicationManagerRoleId", source, StringComparison.Ordinal);
        Assert.Contains("ep.IsDeleted = 0", source, StringComparison.Ordinal);
    }

    private static string FindFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", name),
                         Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Infrastructure", "Services", name)
                     })
            {
                if (File.Exists(candidate)) return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(name);
    }
}

public sealed class ManagerAssignmentSecurityTests
{
    [Fact]
    public void AssignAtomic_ChecksActiveApplicationManager()
    {
        var source = File.ReadAllText(FindRepo("ExamPeriodManagerRepository.cs"));
        Assert.Contains("AssignManagerAtomicAsync", source, StringComparison.Ordinal);
        Assert.Contains("ManagerNotEligible", source, StringComparison.Ordinal);
        Assert.Contains("AlreadyAssigned", source, StringComparison.Ordinal);
        Assert.Contains("ManagerAssigned", source, StringComparison.Ordinal);
        Assert.Contains("ManagerAssignmentRemoved", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveResultAtomic_ManagerCheckIncludesActiveRole()
    {
        var source = File.ReadAllText(FindRepo("CandidateExamResultRepository.cs"));
        Assert.Contains("u.IsActive = 1", source, StringComparison.Ordinal);
        Assert.Contains("u.RoleId = @ApplicationManagerRoleId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationScript_IsIdempotent()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "database", "migrations", "001_AddUsersSecurityStamp.sql");
            if (File.Exists(path))
            {
                var sql = File.ReadAllText(path);
                Assert.Contains("COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("SecurityStamp", sql, StringComparison.Ordinal);
                Assert.Contains("NEWID()", sql, StringComparison.OrdinalIgnoreCase);
                return;
            }
            dir = dir.Parent;
        }
        Assert.Fail("Migration script bulunamadı.");
    }

    private static string FindRepo(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", name);
            if (File.Exists(path)) return path;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(name);
    }
}

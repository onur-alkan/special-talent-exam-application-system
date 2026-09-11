using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Models;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class LoggingAndRetentionSecurityTests
{
    [Fact]
    public void SeederSource_DoesNotLogTcEmailOrPassword()
    {
        var seederSource = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "DatabaseSeeder.cs"));
        Assert.DoesNotContain("TcNo: {TcNo}", seederSource, StringComparison.Ordinal);
        Assert.Contains("Varsayılan SuperAdmin kullanıcısı oluşturuldu. UserId={UserId}, Role={Role}", seederSource, StringComparison.Ordinal);

        var logBlockStart = seederSource.IndexOf("Varsayılan SuperAdmin kullanıcısı oluşturuldu", StringComparison.Ordinal);
        Assert.True(logBlockStart >= 0);
        var logBlock = seederSource.Substring(logBlockStart, Math.Min(250, seederSource.Length - logBlockStart));
        Assert.DoesNotContain("SuperAdminTcNo", logBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperAdminEmail", logBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("SuperAdminPassword", logBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", logBlock, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_DevelopmentUsesDeveloperExceptionPage_ProductionUsesExceptionHandler()
    {
        var program = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Program.cs"));
        Assert.Contains("UseDeveloperExceptionPage", program, StringComparison.Ordinal);
        Assert.Contains("IsDevelopment()", program, StringComparison.Ordinal);
        Assert.Contains("UseExceptionHandler(\"/Home/Error\")", program, StringComparison.Ordinal);
        Assert.Contains("rollOnFileSizeLimit: true", program, StringComparison.Ordinal);
        Assert.Contains("retainedFileCountLimit: 30", program, StringComparison.Ordinal);
        Assert.Contains("UseSerilogRequestLogging", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.QueryString", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Body", program, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRequestBody", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_DoesNotDestructurePasswordOrTokenModelFields()
    {
        var program = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Program.cs"));
        Assert.DoesNotContain("Destructure", program, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".Password", program, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", program, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorView_HasNoExceptionOrStackTrace()
    {
        var view = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "Home", "Error.cshtml"));
        Assert.DoesNotContain("Exception", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", view, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.Ordinal);
        Assert.Contains("@Model!.RequestId", view, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeError_UsesOnlyTraceIdentifier()
    {
        var controller = new HomeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.TraceIdentifier = "trace-abc-123";

        var result = Assert.IsType<ViewResult>(controller.Error());
        var model = Assert.IsType<ErrorViewModel>(result.Model);
        Assert.Equal("trace-abc-123", model.RequestId);
        Assert.True(model.ShowRequestId);
    }

    [Fact]
    public void AuditLogController_RequiresSuperAdminOnly()
    {
        var attr = typeof(AuditLogController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("SuperAdminOnly", attr!.Policy);
    }

    [Fact]
    public void AuditLogRepository_ClampsTakeToMax200()
    {
        Assert.Equal(1, AuditLogRepository.MinTake);
        Assert.Equal(200, AuditLogRepository.MaxTake);
    }

    [Fact]
    public async Task AuditLogController_ClampsPageSize()
    {
        var repo = new Mock<IAuditLogRepository>();
        repo.Setup(r => r.GetRecentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AuditLog>());

        var controller = new AuditLogController(repo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(Guid.NewGuid(), DomainConstants.RoleNames.SuperAdmin)
            }
        };

        await controller.Index(page: 1, pageSize: 999, CancellationToken.None);

        repo.Verify(r => r.GetRecentAsync(200, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AuditLogView_DoesNotUseHtmlRaw()
    {
        var view = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Views", "AuditLog", "Index.cshtml"));
        Assert.DoesNotContain("Html.Raw", view, StringComparison.Ordinal);
        Assert.Contains("@log.Description", view, StringComparison.Ordinal);
    }

    [Fact]
    public void SerilogEnrichmentMiddleware_UsesPathNotQuery()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "Infrastructure", "SerilogEnrichmentMiddleware.cs"));
        Assert.Contains("Request.Path.Value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.QueryString", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Body", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Cookies", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Request.Headers", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Appsettings_ContainsRetentionPolicyWithoutSecrets()
    {
        var json = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Web", "appsettings.json"));
        Assert.Contains("\"DataRetention\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SerilogSqlDays\": 90", json, StringComparison.Ordinal);
        Assert.Contains("\"AuditLogDays\": 365", json, StringComparison.Ordinal);
        Assert.Contains("\"PasswordResetTokenDays\": 7", json, StringComparison.Ordinal);

        var retentionSection = json.Split("\"DataRetention\"", StringSplitOptions.None)[1];
        retentionSection = retentionSection[..retentionSection.IndexOf('}', StringComparison.Ordinal)];
        Assert.DoesNotContain("ConnectionString", retentionSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SuperAdminPassword", retentionSection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Password\":", retentionSection, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanupService_ErrorLog_DoesNotIncludeRowContentPlaceholders()
    {
        var source = File.ReadAllText(FindUnder("src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "DataRetentionCleanupService.cs"));
        Assert.Contains("Table={Table}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Description={", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TcNo={", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Email={", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IpAddress={", source, StringComparison.Ordinal);
    }

    private static DefaultHttpContext CreateHttpContext(Guid userId, string role)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, authenticationType: "Test");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    private static string FindUnder(params string[] parts)
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

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;
using OzelYetenekSinavSistemi.Web.Controllers;

namespace OzelYetenekSinavSistemi.Tests.Security;

public sealed class ExamResultControllerTests
{
    private static readonly Guid ExamId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Controller_HasAdminAreaPolicy()
    {
        var attr = typeof(ExamResultController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("AdminArea", attr!.Policy);
    }

    [Fact]
    public async Task ExportExcel_Unauthorized_DoesNotCallExportService()
    {
        var examPeriod = new Mock<IExamPeriodService>();
        var examResult = new Mock<IExamResultService>();
        var excel = new Mock<IExcelExportService>();
        var pdf = new Mock<IPdfExportService>();
        var audit = new Mock<IAuditService>();

        examPeriod.Setup(s => s.GetByIdForUserAsync(ExamId, UserId, DomainConstants.RoleNames.ApplicationManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ExamPeriod>.Fail("yetki yok"));

        var controller = CreateController(examPeriod, examResult, excel, pdf, audit, DomainConstants.RoleNames.ApplicationManager);
        var result = await controller.ExportExcel(ExamId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        excel.Verify(e => e.ExportCandidateResults(It.IsAny<string>(), It.IsAny<IReadOnlyList<CandidateApplicationDetail>>()), Times.Never);
        audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportExcel_Success_SetsSecureHeadersAndAudits()
    {
        var examPeriod = new Mock<IExamPeriodService>();
        var examResult = new Mock<IExamResultService>();
        var excel = new Mock<IExcelExportService>();
        var pdf = new Mock<IPdfExportService>();
        var audit = new Mock<IAuditService>();

        examPeriod.Setup(s => s.GetByIdForUserAsync(ExamId, UserId, DomainConstants.RoleNames.SuperAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ExamPeriod>.Ok(new ExamPeriod { Id = ExamId, Title = "Sınav" }));
        examResult.Setup(s => s.GetCandidatesForExamAsync(ExamId, UserId, DomainConstants.RoleNames.SuperAdmin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<IReadOnlyList<CandidateApplicationDetail>>.Ok(Array.Empty<CandidateApplicationDetail>()));
        excel.Setup(e => e.ExportCandidateResults("Sınav", It.IsAny<IReadOnlyList<CandidateApplicationDetail>>()))
            .Returns(new byte[] { 1, 2, 3 });

        var controller = CreateController(examPeriod, examResult, excel, pdf, audit, DomainConstants.RoleNames.SuperAdmin);
        var result = await controller.ExportExcel(ExamId, CancellationToken.None);

        Assert.IsType<FileContentResult>(result);
        Assert.Contains("no-store", controller.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-cache", controller.Response.Headers.Pragma.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        audit.Verify(a => a.LogAsync(
            "ExamResultsExportedExcel",
            It.Is<string>(d => d.Contains(ExamId.ToString(), StringComparison.Ordinal)),
            UserId,
            It.IsAny<string?>(),
            null,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportPdf_Unauthorized_DoesNotCallExportService()
    {
        var examPeriod = new Mock<IExamPeriodService>();
        var examResult = new Mock<IExamResultService>();
        var excel = new Mock<IExcelExportService>();
        var pdf = new Mock<IPdfExportService>();
        var audit = new Mock<IAuditService>();

        examPeriod.Setup(s => s.GetByIdForUserAsync(ExamId, UserId, DomainConstants.RoleNames.ApplicationManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<ExamPeriod>.Fail("yetki yok"));

        var controller = CreateController(examPeriod, examResult, excel, pdf, audit, DomainConstants.RoleNames.ApplicationManager);
        var result = await controller.ExportPdf(ExamId, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        pdf.Verify(e => e.ExportCandidateResults(It.IsAny<string>(), It.IsAny<IReadOnlyList<CandidateApplicationDetail>>()), Times.Never);
    }

    private static ExamResultController CreateController(
        Mock<IExamPeriodService> examPeriod,
        Mock<IExamResultService> examResult,
        Mock<IExcelExportService> excel,
        Mock<IPdfExportService> pdf,
        Mock<IAuditService> audit,
        string role)
    {
        var controller = new ExamResultController(
            examPeriod.Object, examResult.Object, excel.Object, pdf.Object, audit.Object,
            new CountryCatalog(), new SensitiveDataMaskingService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(UserId, role)
            }
        };
        return controller;
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
}

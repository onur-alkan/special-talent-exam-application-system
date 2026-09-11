using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Documents;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Web.Configuration;
using OzelYetenekSinavSistemi.Web.Controllers;
using OzelYetenekSinavSistemi.Web.Infrastructure;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExamEntranceDocumentAccessTests
{
    private static readonly DateTime ApplicationEnd = new(2026, 7, 22, 17, 0, 0);

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void IsAvailable_BoundarySeconds_MatchesPolicy(int offsetSeconds, bool expected)
    {
        var current = ApplicationEnd.AddSeconds(offsetSeconds);
        Assert.Equal(expected, ExamEntranceDocumentAccess.IsAvailable(ApplicationEnd, current));
    }

    [Fact]
    public void IsAvailable_UsesTimeProviderLocalNow_WithoutMachineTimezoneGuess()
    {
        var time = new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-1));
        Assert.False(ExamEntranceDocumentAccess.IsAvailable(ApplicationEnd, time));

        time = new FixedLocalTimeProvider(ApplicationEnd);
        Assert.True(ExamEntranceDocumentAccess.IsAvailable(ApplicationEnd, time));

        time = new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(1));
        Assert.True(ExamEntranceDocumentAccess.IsAvailable(ApplicationEnd, time));
    }

    [Fact]
    public void NotYetAvailableMessage_MatchesProductCopy()
    {
        Assert.Equal(
            "Sınava giriş belgesi, başvuru süresi sona erdikten sonra erişime açılacaktır.",
            ExamEntranceDocumentAccess.NotYetAvailableMessage);
    }
}

public sealed class ExamEntranceDocumentGateServiceTests
{
    private static readonly Guid ApplicationId = Guid.NewGuid();
    private static readonly Guid CandidateUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid ManagerUserId = Guid.NewGuid();
    private static readonly Guid ExamPeriodId = Guid.NewGuid();
    private static readonly DateTime ApplicationEnd = new(2026, 7, 22, 17, 0, 0);
    private const string VerificationBaseUrl = "https://localhost/DocumentVerification/Verify";
    private const string VerificationCode = "ABCDEF0123456789ABCDEF0123456789";

    private readonly Mock<ICandidateApplicationRepository> _appRepo = new();
    private readonly Mock<IExamPeriodManagerRepository> _managerRepo = new();
    private readonly Mock<IQrCodeService> _qr = new();
    private readonly Mock<ISensitiveDataMaskingService> _masking = new();
    private readonly CountryCatalog _countryCatalog = new();

    public ExamEntranceDocumentGateServiceTests()
    {
        _masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        _masking.Setup(m => m.MaskIdentityNumber(
                It.IsAny<IdentityDocumentType?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns("***********");
        _qr.Setup(q => q.GenerateBase64PngDataUri(It.IsAny<string>())).Returns("data:image/png;base64,qq");
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public async Task GetEntranceDocument_Candidate_TimeBoundary(int offsetSeconds, bool expectSuccess)
    {
        SetupDetail(CreateDetail());
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(offsetSeconds)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.Equal(expectSuccess, result.Success);
        if (!expectSuccess)
        {
            Assert.Equal(ExamEntranceDocumentAccess.NotYetAvailableMessage, result.ErrorMessage);
            Assert.Null(result.Data);
        }
        else
        {
            Assert.NotNull(result.Data);
            Assert.Equal(VerificationCode, result.Data!.VerificationCode);
        }
    }

    [Fact]
    public async Task GetEntranceDocument_BeforeEnd_DoesNotLeakDocumentPayload()
    {
        SetupDetail(CreateDetail());
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-1)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.DoesNotContain(VerificationCode, result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("PhotoPath", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ali@example.com", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEntranceDocument_OtherCandidate_DeniedEvenAfterEnd()
    {
        SetupDetail(CreateDetail());
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddHours(1)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, OtherUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.False(result.Success);
        Assert.Contains("yetkiniz yok", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetEntranceDocument_MissingApplication_FailsWithoutDocument()
    {
        _appRepo.Setup(r => r.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CandidateApplicationDetail?)null);
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddHours(1)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.False(result.Success);
        Assert.Contains("bulunamadı", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetEntranceDocument_SuperAdmin_BypassesTimeGate()
    {
        SetupDetail(CreateDetail());
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-30)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, Guid.NewGuid(), DomainConstants.RoleNames.SuperAdmin, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetEntranceDocument_AssignedManager_BypassesTimeGate()
    {
        SetupDetail(CreateDetail());
        _managerRepo.Setup(m => m.IsManagerOfExamPeriodAsync(ManagerUserId, ExamPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-30)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, ManagerUserId, DomainConstants.RoleNames.ApplicationManager, VerificationBaseUrl);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetEntranceDocument_UnassignedManager_CannotBypass()
    {
        SetupDetail(CreateDetail());
        _managerRepo.Setup(m => m.IsManagerOfExamPeriodAsync(ManagerUserId, ExamPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddHours(2)));

        var result = await sut.GetEntranceDocumentAsync(
            ApplicationId, ManagerUserId, DomainConstants.RoleNames.ApplicationManager, VerificationBaseUrl);

        Assert.False(result.Success);
        Assert.Contains("yetkiniz yok", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExamDocumentController_BeforeEnd_RedirectsWithToast_NoViewModel()
    {
        SetupDetail(CreateDetail());
        var documents = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-1)));
        var controller = CreateController(documents, CandidateUserId, DomainConstants.RoleNames.Candidate);

        var action = await controller.View(ApplicationId, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal("My", redirect.ActionName);
        Assert.Equal("CandidateApplication", redirect.ControllerName);
        Assert.Equal(ExamEntranceDocumentAccess.NotYetAvailableMessage, controller.TempData["ToastError"]);
    }

    [Fact]
    public async Task ExamDocumentController_AfterEnd_ReturnsView()
    {
        SetupDetail(CreateDetail());
        var documents = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(1)));
        var controller = CreateController(documents, CandidateUserId, DomainConstants.RoleNames.Candidate);

        var action = await controller.View(ApplicationId, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(action);
        Assert.IsType<ExamEntranceDocumentViewModel>(view.Model);
    }

    private DocumentService CreateSut(TimeProvider timeProvider) =>
        new(_appRepo.Object, _managerRepo.Object, _qr.Object, _masking.Object, _countryCatalog, timeProvider);

    private void SetupDetail(CandidateApplicationDetail detail) =>
        _appRepo.Setup(r => r.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

    private static CandidateApplicationDetail CreateDetail() =>
        new()
        {
            ApplicationId = ApplicationId,
            UserId = CandidateUserId,
            ExamPeriodId = ExamPeriodId,
            CandidateNo = 42,
            VerificationCode = VerificationCode,
            RegistrationDate = ApplicationEnd.AddDays(-10),
            ExamPeriodEndDate = ApplicationEnd,
            CanAccessExamEntranceDocument = false,
            TcNo = "10000000146",
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" }
        };

    private static ExamDocumentController CreateController(IDocumentService documents, Guid userId, string role)
    {
        var publicUrl = new PublicUrlBuilder(Options.Create(new PublicUrlOptions
        {
            BaseUrl = "https://basvuru.example.edu.tr"
        }));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "Test");

        var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new ExamDocumentController(documents, publicUrl)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = http,
                ActionDescriptor = new ControllerActionDescriptor()
            },
            TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
        };
    }
}

public sealed class ExamEntranceDocumentUiSourceTests
{
    [Fact]
    public void DetailView_GatesEntranceDocumentLinkBehindServerFlag()
    {
        var detail = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateApplication", "Detail.cshtml");

        Assert.Contains("CanAccessExamEntranceDocument", detail, StringComparison.Ordinal);
        Assert.Contains("Sınava giriş belgesi, başvuru süresi sona erdikten sonra erişime açılacaktır.", detail, StringComparison.Ordinal);
        Assert.Contains("Erişim tarihi:", detail, StringComparison.Ordinal);
        Assert.Contains("aria-disabled=\"true\"", detail, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"ExamDocument\"", detail, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"View\"", detail, StringComparison.Ordinal);

        // Aktif link yalnızca erişim bayrağı true iken üretilir.
        Assert.Contains("@if (Model.CanAccessExamEntranceDocument)", detail, StringComparison.Ordinal);
        var activeBranchStart = detail.IndexOf("@if (Model.CanAccessExamEntranceDocument)", StringComparison.Ordinal);
        var elseBranchStart = detail.IndexOf("else", activeBranchStart, StringComparison.Ordinal);
        var activeBranch = detail[activeBranchStart..elseBranchStart];
        var disabledBranch = detail[elseBranchStart..];
        Assert.Contains("asp-action=\"View\"", activeBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("asp-action=\"View\"", disabledBranch, StringComparison.Ordinal);
        Assert.Contains("aria-disabled=\"true\"", disabledBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailView_HidesVerificationCodeUntilDocumentAccessOpens()
    {
        var detail = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateApplication", "Detail.cshtml");
        Assert.Contains(
            ExamEntranceDocumentAccess.VerificationDetailsHiddenMessage,
            detail,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<dd class=\"col-sm-9\"><code>@Model.VerificationCode</code></dd>",
            detail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MyView_GatesEntranceDocumentLinkBehindServerFlag()
    {
        var my = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateApplication", "My.cshtml");

        Assert.Contains("CanAccessExamEntranceDocument", my, StringComparison.Ordinal);
        Assert.Contains("aria-disabled=\"true\"", my, StringComparison.Ordinal);
        Assert.Contains("@if (app.CanAccessExamEntranceDocument)", my, StringComparison.Ordinal);
        Assert.Contains("Sınava giriş belgesi, başvuru süresi sona erdikten sonra erişime açılacaktır.", my, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailSelect_ComputesEntranceAccessFromExamPeriodEndDate()
    {
        var repo = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "CandidateApplicationRepository.cs");

        Assert.Contains("ep.EndDate AS ExamPeriodEndDate", repo, StringComparison.Ordinal);
        Assert.Contains("SYSDATETIME() >= ep.EndDate", repo, StringComparison.Ordinal);
        Assert.Contains("CanAccessExamEntranceDocument", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentService_GatesCandidatesWithTimeProvider_NotClientClock()
    {
        var source = ReadProjectFile("src", "OzelYetenekSinavSistemi.Application", "Services", "DocumentService.cs");

        Assert.Contains("ExamEntranceDocumentAccess.IsAvailable", source, StringComparison.Ordinal);
        Assert.Contains("TimeProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.UtcNow", source, StringComparison.Ordinal);
        Assert.Contains("DomainConstants.RoleNames.Candidate", source, StringComparison.Ordinal);
        Assert.Contains("NotYetAvailableMessage", source, StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

/// <summary>
/// Makine yerel saat dilimine bağlı kalmadan sabit yerel an üretir.
/// TimeProvider.GetLocalNow() override edilemez; UtcNow + sıfır ofsetli LocalTimeZone kullanılır.
/// </summary>
internal sealed class FixedLocalTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedLocalTimeProvider(DateTime localDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        _utcNow = new DateTimeOffset(unspecified, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("ExamEntranceTest", TimeSpan.Zero, "ExamEntranceTest", "ExamEntranceTest");
}

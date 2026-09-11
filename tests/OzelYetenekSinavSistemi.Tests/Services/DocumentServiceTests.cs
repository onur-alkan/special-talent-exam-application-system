using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class DocumentServiceTests
{
    private static readonly Guid ApplicationId = Guid.NewGuid();
    private static readonly Guid CandidateUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid ManagerUserId = Guid.NewGuid();
    private static readonly Guid ExamPeriodId = Guid.NewGuid();
    private const string VerificationBaseUrl = "https://localhost/DocumentVerification/Verify";
    private const string VerificationCode = "ABCDEF0123456789ABCDEF0123456789";

    private readonly Mock<ICandidateApplicationRepository> _appRepo = new();
    private readonly Mock<IExamPeriodManagerRepository> _managerRepo = new();
    private readonly Mock<IQrCodeService> _qr = new();
    private readonly Mock<ISensitiveDataMaskingService> _masking = new();
    private readonly CountryCatalog _countryCatalog = new();

    private DocumentService CreateSut(TimeProvider? timeProvider = null) =>
        new(_appRepo.Object, _managerRepo.Object, _qr.Object, _masking.Object, _countryCatalog,
            timeProvider ?? new FixedLocalTimeProvider(new DateTime(2026, 7, 23, 12, 0, 0)));

    public DocumentServiceTests()
    {
        _masking.Setup(m => m.MaskTcNo(It.IsAny<string>())).Returns("***********");
        _masking.Setup(m => m.MaskIdentityNumber(
                It.IsAny<IdentityDocumentType?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns("***********");
        _qr.Setup(q => q.GenerateBase64PngDataUri(It.IsAny<string>())).Returns("data:image/png;base64,qq");
    }

    [Fact]
    public async Task GetResultDocument_CandidateOwnApplication_Succeeds()
    {
        SetupDetail(CreateDetail(AttendanceStatus.Attended, 88m));

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(88m, result.Data!.ExamScore);
        Assert.Contains("code=", result.Data.VerificationUrl, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(VerificationCode), result.Data.VerificationUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResultDocument_CandidateOtherApplication_Fails()
    {
        SetupDetail(CreateDetail(AttendanceStatus.Attended, 88m));

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, OtherUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.False(result.Success);
        Assert.Contains("yetkiniz yok", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResultDocument_SuperAdmin_Succeeds()
    {
        SetupDetail(CreateDetail(AttendanceStatus.Attended, 70m));

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, Guid.NewGuid(), DomainConstants.RoleNames.SuperAdmin, VerificationBaseUrl);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetResultDocument_AssignedManager_Succeeds()
    {
        SetupDetail(CreateDetail(AttendanceStatus.Attended, 70m));
        _managerRepo.Setup(m => m.IsManagerOfExamPeriodAsync(ManagerUserId, ExamPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, ManagerUserId, DomainConstants.RoleNames.ApplicationManager, VerificationBaseUrl);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetResultDocument_UnassignedManager_Fails()
    {
        SetupDetail(CreateDetail(AttendanceStatus.Attended, 70m));
        _managerRepo.Setup(m => m.IsManagerOfExamPeriodAsync(ManagerUserId, ExamPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, ManagerUserId, DomainConstants.RoleNames.ApplicationManager, VerificationBaseUrl);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetResultDocument_NoResult_Fails()
    {
        SetupDetail(CreateDetail(attendance: null, score: null));

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.False(result.Success);
        Assert.Contains("henüz açıklanmamış", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResultDocument_NotAttended_ScoreIsNull()
    {
        SetupDetail(CreateDetail(AttendanceStatus.NotAttended, score: null));

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Null(result.Data!.ExamScore);
        Assert.Equal(AttendanceStatus.NotAttended, result.Data.AttendanceStatus);
    }

    [Fact]
    public async Task GetResultDocument_Disqualified_HasNullScore_AndDoesNotExposeHiddenDescription()
    {
        var detail = CreateDetail(AttendanceStatus.Disqualified, score: null);
        detail = CloneWithDescription(detail, "Yalnız yönetici notu", isVisible: false);
        SetupDetail(detail);

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Disqualified, result.Data!.AttendanceStatus);
        Assert.Null(result.Data.ExamScore);
        Assert.Null(result.Data.CandidateVisibleDescription);
    }

    [Fact]
    public async Task GetResultDocument_DescriptionHidden_DoesNotIncludeAdminDescription()
    {
        var detail = CreateDetail(AttendanceStatus.Attended, 90m);
        detail = CloneWithDescription(detail, "Gizli not", isVisible: false);
        SetupDetail(detail);

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Null(result.Data!.CandidateVisibleDescription);
    }

    [Fact]
    public async Task GetResultDocument_DescriptionVisible_IncludesAdminDescription()
    {
        var detail = CreateDetail(AttendanceStatus.Attended, 90m);
        detail = CloneWithDescription(detail, "Adaya açık not", isVisible: true);
        SetupDetail(detail);

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal("Adaya açık not", result.Data!.CandidateVisibleDescription);
    }

    [Fact]
    public async Task GetEntranceDocument_StillWorks_ForCandidate()
    {
        SetupDetail(CreateDetail(AttendanceStatus.Attended, 80m));

        var result = await CreateSut().GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal(VerificationCode, result.Data!.VerificationCode);
        Assert.Contains(Uri.EscapeDataString(VerificationCode), result.Data.VerificationUrl, StringComparison.Ordinal);
    }

    private void SetupDetail(CandidateApplicationDetail detail) =>
        _appRepo.Setup(r => r.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

    private static CandidateApplicationDetail CreateDetail(AttendanceStatus? attendance, decimal? score) =>
        new()
        {
            ApplicationId = ApplicationId,
            UserId = CandidateUserId,
            ExamPeriodId = ExamPeriodId,
            CandidateNo = 42,
            VerificationCode = VerificationCode,
            RegistrationDate = DateTime.UtcNow.AddDays(-10),
            ExamPeriodEndDate = new DateTime(2026, 7, 1, 12, 0, 0),
            CanAccessExamEntranceDocument = true,
            TcNo = "10000000146",
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" },
            AttendanceStatus = attendance,
            ExamScore = score,
            AdminDescription = null,
            IsDescriptionVisibleToCandidate = false,
            EvaluatedDate = attendance.HasValue ? DateTime.UtcNow.AddDays(-1) : null
        };

    private static CandidateApplicationDetail CloneWithDescription(
        CandidateApplicationDetail source,
        string description,
        bool isVisible) =>
        new()
        {
            ApplicationId = source.ApplicationId,
            UserId = source.UserId,
            ExamPeriodId = source.ExamPeriodId,
            CandidateNo = source.CandidateNo,
            VerificationCode = source.VerificationCode,
            RegistrationDate = source.RegistrationDate,
            ExamPeriodEndDate = source.ExamPeriodEndDate,
            CanAccessExamEntranceDocument = source.CanAccessExamEntranceDocument,
            TcNo = source.TcNo,
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email,
            ExamTitle = source.ExamTitle,
            Preferences = source.Preferences,
            AttendanceStatus = source.AttendanceStatus,
            ExamScore = source.ExamScore,
            AdminDescription = description,
            IsDescriptionVisibleToCandidate = isVisible,
            EvaluatedDate = source.EvaluatedDate
        };
}

using Moq;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class IdentityDocumentServiceTests
{
    private static readonly Guid ApplicationId = Guid.NewGuid();
    private static readonly Guid CandidateUserId = Guid.NewGuid();
    private static readonly Guid ExamPeriodId = Guid.NewGuid();
    private const string VerificationBaseUrl = "https://localhost/DocumentVerification/Verify";
    private const string VerificationCode = "ABCDEF0123456789ABCDEF0123456789";
    private const string RawPassport = "AB1234567";
    private const string RawForeignId = "99000000001";

    private readonly Mock<ICandidateApplicationRepository> _appRepo = new();
    private readonly Mock<IExamPeriodManagerRepository> _managerRepo = new();
    private readonly Mock<IQrCodeService> _qr = new();
    private readonly SensitiveDataMaskingService _masking = new();
    private readonly CountryCatalog _countryCatalog = new();

    public IdentityDocumentServiceTests()
    {
        _qr.Setup(q => q.GenerateBase64PngDataUri(It.IsAny<string>())).Returns("data:image/png;base64,qq");
    }

    [Fact]
    public async Task EntranceDocument_TurkishCandidate_ShowsMaskedTcAndTurkey()
    {
        SetupDetail(CreateTurkishDetail());

        var result = await CreateSut().GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal("T.C. Kimlik Kartı", result.Data!.Identity.IdentityDocumentTypeDisplayName);
        Assert.Equal("T.C. Kimlik Numarası", result.Data.Identity.IdentityNumberLabel);
        Assert.Equal("100******46", result.Data.Identity.MaskedIdentityNumber);
        Assert.Equal("Türkiye", result.Data.Identity.NationalityDisplayName);
        Assert.DoesNotContain("10000000146", result.Data.Identity.MaskedIdentityNumber);
    }

    [Fact]
    public async Task EntranceDocument_ForeignCandidate_ShowsMaskedForeignIdentity()
    {
        SetupDetail(CreateForeignDetail());

        var result = await CreateSut().GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal("Yabancı Kimlik Numarası", result.Data!.Identity.IdentityDocumentTypeDisplayName);
        Assert.Equal("Almanya", result.Data.Identity.NationalityDisplayName);
        Assert.DoesNotContain(RawForeignId, result.Data.Identity.MaskedIdentityNumber);
    }

    [Fact]
    public async Task EntranceDocument_PassportCandidate_DoesNotExposeFullPassportNumber()
    {
        SetupDetail(CreatePassportDetail());

        var result = await CreateSut().GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal("Pasaport", result.Data!.Identity.IdentityDocumentTypeDisplayName);
        Assert.Equal("Pasaport Numarası", result.Data.Identity.IdentityNumberLabel);
        Assert.Equal("Almanya", result.Data.Identity.IssuingCountryDisplayName);
        Assert.Equal("15.06.2030", result.Data.Identity.PassportExpiryDateDisplay);
        Assert.DoesNotContain(RawPassport, result.Data.Identity.MaskedIdentityNumber, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EntranceDocument_LegacyTcOnlyUser_DoesNotFail()
    {
        SetupDetail(new CandidateApplicationDetail
        {
            ApplicationId = ApplicationId,
            UserId = CandidateUserId,
            ExamPeriodId = ExamPeriodId,
            CandidateNo = 42,
            VerificationCode = VerificationCode,
            RegistrationDate = DateTime.UtcNow.AddDays(-10),
            ExamPeriodEndDate = new DateTime(2026, 7, 1, 12, 0, 0),
            CanAccessExamEntranceDocument = true,
            IdentityDocumentType = null,
            IdentityNumber = null,
            TcNo = "10000000146",
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" }
        });

        var result = await CreateSut().GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal("T.C. Kimlik Kartı", result.Data!.Identity.IdentityDocumentTypeDisplayName);
    }

    [Fact]
    public async Task EntranceDocument_VerificationUrl_DoesNotContainIdentityNumber()
    {
        SetupDetail(CreateTurkishDetail());

        var result = await CreateSut().GetEntranceDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Contains(Uri.EscapeDataString(VerificationCode), result.Data!.VerificationUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("10000000146", result.Data.VerificationUrl, StringComparison.Ordinal);
        Assert.DoesNotContain("10000000146", result.Data.QrCodeDataUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultDocument_UsesSameMaskedIdentityPresentation()
    {
        SetupDetail(new CandidateApplicationDetail
        {
            ApplicationId = ApplicationId,
            UserId = CandidateUserId,
            ExamPeriodId = ExamPeriodId,
            CandidateNo = 42,
            VerificationCode = VerificationCode,
            RegistrationDate = DateTime.UtcNow.AddDays(-10),
            IdentityDocumentType = IdentityDocumentType.Passport,
            IdentityNumber = RawPassport,
            TcNo = string.Empty,
            NationalityCountryCode = "DE",
            IssuingCountryCode = "DE",
            PassportExpiryDate = new DateOnly(2030, 6, 15),
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" },
            AttendanceStatus = AttendanceStatus.Attended,
            ExamScore = 88m,
            EvaluatedDate = DateTime.UtcNow.AddDays(-1)
        });

        var result = await CreateSut().GetResultDocumentAsync(
            ApplicationId, CandidateUserId, DomainConstants.RoleNames.Candidate, VerificationBaseUrl);

        Assert.True(result.Success);
        Assert.Equal("Pasaport", result.Data!.Identity.IdentityDocumentTypeDisplayName);
        Assert.DoesNotContain(RawPassport, result.Data.Identity.MaskedIdentityNumber, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CandidateApplicationDetail_DoesNotExposeNormalizedIdentityNumberProperty()
    {
        Assert.DoesNotContain(
            typeof(CandidateApplicationDetail).GetProperties().Select(p => p.Name),
            name => name.Contains("NormalizedIdentityNumber", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryDetailSelect_IncludesIdentityProjectionFields()
    {
        var source = ReadRepositorySource();
        Assert.Contains("u.IdentityDocumentType", source, StringComparison.Ordinal);
        Assert.Contains("u.IdentityNumber", source, StringComparison.Ordinal);
        Assert.Contains("u.NationalityCountryCode", source, StringComparison.Ordinal);
        Assert.Contains("u.IssuingCountryCode", source, StringComparison.Ordinal);
        Assert.Contains("u.PassportExpiryDate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NormalizedIdentityNumber", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentVerificationView_DoesNotExposeIdentityNumber()
    {
        var verify = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "DocumentVerification", "Verify.cshtml");
        Assert.DoesNotContain("IdentityNumber", verify, StringComparison.Ordinal);
        Assert.DoesNotContain("TcNo", verify, StringComparison.Ordinal);
        Assert.DoesNotContain("Pasaport", verify, StringComparison.Ordinal);
        Assert.Contains("MaskedCandidateName", verify, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationService_LoginAudit_UsesMaskIdentityNumber()
    {
        var source = ReadProjectFile("src", "OzelYetenekSinavSistemi.Application", "Services", "AuthenticationService.cs");
        Assert.Contains("MaskIdentityNumber", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"LoginFailed.*Identity=\{[^}]*normalizedTc[^}]*\}", source);
        Assert.DoesNotMatch(@"LoginFailed.*Identity=\{[^}]*trimmed[^}]*\}", source);
    }

    [Fact]
    public void ServerExportServices_DoNotIncludeIdentityFields()
    {
        var excel = ReadProjectFile("src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "ExcelExportService.cs");
        var pdf = ReadProjectFile("src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "PdfExportService.cs");
        Assert.DoesNotContain("IdentityNumber", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("Passport", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("Uyruk", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("IdentityNumber", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("Passport", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("Uyruk", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminCandidateListMarkup_DoesNotExposeFullIdentityColumn()
    {
        var list = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateManagement", "List.cshtml");
        Assert.DoesNotContain("IdentityNumber", list, StringComparison.Ordinal);
        Assert.DoesNotContain("MaskedIdentity", list, StringComparison.Ordinal);
        Assert.DoesNotContain("TcNo", list, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateView_ShowsMaskedIdentitySection()
    {
        var evaluate = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "Evaluate.cshtml");
        Assert.Contains("Kimlik ve Uyruk Bilgileri", evaluate, StringComparison.Ordinal);
        Assert.Contains("candidateIdentity.DisplayIdentityNumber", evaluate, StringComparison.Ordinal);
    }

    private DocumentService CreateSut() =>
        new(_appRepo.Object, _managerRepo.Object, _qr.Object, _masking, _countryCatalog,
            new FixedLocalTimeProvider(new DateTime(2026, 7, 23, 12, 0, 0)));

    private void SetupDetail(CandidateApplicationDetail detail) =>
        _appRepo.Setup(r => r.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

    private static CandidateApplicationDetail CreateTurkishDetail() =>
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
            IdentityDocumentType = IdentityDocumentType.TurkishIdentityNumber,
            IdentityNumber = "10000000146",
            TcNo = "10000000146",
            NationalityCountryCode = "TR",
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" }
        };

    private static CandidateApplicationDetail CreateForeignDetail() =>
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
            IdentityDocumentType = IdentityDocumentType.ForeignIdentityNumber,
            IdentityNumber = RawForeignId,
            TcNo = string.Empty,
            NationalityCountryCode = "DE",
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" }
        };

    private static CandidateApplicationDetail CreatePassportDetail() =>
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
            IdentityDocumentType = IdentityDocumentType.Passport,
            IdentityNumber = RawPassport,
            TcNo = string.Empty,
            NationalityCountryCode = "DE",
            IssuingCountryCode = "DE",
            PassportExpiryDate = new DateOnly(2030, 6, 15),
            FirstName = "Ali",
            LastName = "Veli",
            Email = "ali@example.com",
            ExamTitle = "2026 Özel Yetenek",
            Preferences = new[] { "Resim", "Heykel" }
        };

    private static CandidateApplicationDetail BaseDetail() => CreateTurkishDetail();

    private static string ReadRepositorySource() =>
        ReadProjectFile("src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "CandidateApplicationRepository.cs");

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

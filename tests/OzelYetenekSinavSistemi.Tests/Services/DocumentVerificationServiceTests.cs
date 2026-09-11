using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class DocumentVerificationServiceTests
{
    private static readonly DateTime ApplicationEnd = new(2026, 7, 22, 17, 0, 0);
    private static readonly Guid ApplicationId = Guid.NewGuid();
    private const string ValidCode = "ABCDEF0123456789ABCDEF0123456789";

    private readonly Mock<IDocumentVerificationRepository> _verificationRepo = new();
    private readonly Mock<ICandidateApplicationRepository> _appRepo = new();

    private DocumentVerificationService CreateSut(TimeProvider? timeProvider = null) =>
        new(_verificationRepo.Object, _appRepo.Object,
            timeProvider ?? new FixedLocalTimeProvider(ApplicationEnd.AddHours(1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABC")]
    [InlineData("ABCDEF0123456789ABCDEF012345678")]   // 31
    [InlineData("ABCDEF0123456789ABCDEF0123456789A")] // 33
    public async Task Verify_InvalidLength_DoesNotQueryRepository(string? code)
    {
        var result = await CreateSut().VerifyAsync(code);

        Assert.False(result.IsValid);
        Assert.False(result.IsNotYetAvailable);
        _verificationRepo.Verify(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _appRepo.Verify(r => r.GetDetailByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("ABCDEF0123456789ABCDEF012345678G")] // non-hex G
    [InlineData("ABCDEF0123456789ABCDEF01234567ZZ")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Verify_NonHexadecimal_DoesNotQueryRepository(string code)
    {
        var result = await CreateSut().VerifyAsync(code);

        Assert.False(result.IsValid);
        _verificationRepo.Verify(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_Valid32Hex_QueriesRepositoryWithUppercase()
    {
        SetupValidVerification();

        var result = await CreateSut().VerifyAsync(ValidCode.ToLowerInvariant());

        Assert.True(result.IsValid);
        Assert.False(result.IsNotYetAvailable);
        Assert.Equal(ValidCode, result.VerificationCode);
        Assert.Equal(7, result.CandidateNo);
        Assert.Contains("*", result.MaskedCandidateName);
        _verificationRepo.Verify(r => r.GetByCodeAsync(ValidCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verify_UnknownValidFormat_ReturnsGenericInvalid()
    {
        _verificationRepo.Setup(r => r.GetByCodeAsync(ValidCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentVerification?)null);

        var result = await CreateSut().VerifyAsync(ValidCode);

        Assert.False(result.IsValid);
        Assert.False(result.IsNotYetAvailable);
        Assert.Null(result.ExamTitle);
        Assert.Null(result.CandidateNo);
        Assert.Null(result.MaskedCandidateName);
        Assert.Equal(ValidCode, result.VerificationCode);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task Verify_TimeBoundary_GatesEntranceDocumentVerification(int offsetSeconds, bool expectNotYetAvailable)
    {
        SetupValidVerification();
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(offsetSeconds)));

        var result = await sut.VerifyAsync(ValidCode);

        if (expectNotYetAvailable)
        {
            Assert.False(result.IsValid);
            Assert.True(result.IsNotYetAvailable);
            Assert.Null(result.ExamTitle);
            Assert.Null(result.CandidateNo);
            Assert.Null(result.MaskedCandidateName);
            Assert.Equal(string.Empty, result.VerificationCode);
        }
        else
        {
            Assert.True(result.IsValid);
            Assert.False(result.IsNotYetAvailable);
            Assert.Equal("Sınav", result.ExamTitle);
            Assert.Equal(7, result.CandidateNo);
        }
    }

    [Fact]
    public async Task Verify_BeforeEnd_DoesNotLeakDocumentFields()
    {
        SetupValidVerification(firstName: "Ayşe", lastName: "Yılmaz", examTitle: "Gizli Sınav 2026", candidateNo: 99);
        var sut = CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-1)));

        var result = await sut.VerifyAsync(ValidCode);

        Assert.True(result.IsNotYetAvailable);
        Assert.False(result.IsValid);
        Assert.Null(result.ExamTitle);
        Assert.Null(result.CandidateNo);
        Assert.Null(result.MaskedCandidateName);
        Assert.DoesNotContain("Ayşe", result.MaskedCandidateName ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Gizli", result.ExamTitle ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(ValidCode, result.VerificationCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_InvalidAndEarly_BothDenyWithoutDocumentPayload()
    {
        _verificationRepo.Setup(r => r.GetByCodeAsync("11111111111111111111111111111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentVerification?)null);
        SetupValidVerification();

        var early = await CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddSeconds(-1))).VerifyAsync(ValidCode);
        var unknown = await CreateSut(new FixedLocalTimeProvider(ApplicationEnd.AddHours(1)))
            .VerifyAsync("11111111111111111111111111111111");

        Assert.False(early.IsValid);
        Assert.False(unknown.IsValid);
        Assert.Null(early.ExamTitle);
        Assert.Null(unknown.ExamTitle);
        Assert.Null(early.CandidateNo);
        Assert.Null(unknown.CandidateNo);
        Assert.Null(early.MaskedCandidateName);
        Assert.Null(unknown.MaskedCandidateName);
        Assert.DoesNotContain("Belge Geçerli", early.ExamTitle ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyView_GatesEarlyAccessWithoutDocumentFields()
    {
        var verify = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "DocumentVerification", "Verify.cshtml");

        Assert.Contains("IsNotYetAvailable", verify, StringComparison.Ordinal);
        Assert.Contains("VerificationNotYetAvailableMessage", verify, StringComparison.Ordinal);
        Assert.Contains("Belge Geçerli", verify, StringComparison.Ordinal);

        var notYetStart = verify.IndexOf("IsNotYetAvailable", StringComparison.Ordinal);
        var invalidStart = verify.IndexOf("else", notYetStart, StringComparison.Ordinal);
        var notYetBranch = verify[notYetStart..invalidStart];
        Assert.DoesNotContain("ExamTitle", notYetBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("CandidateNo", notYetBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("MaskedCandidateName", notYetBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("VerificationCode", notYetBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("Belge Geçerli", notYetBranch, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailView_HidesVerificationCodeUntilEntranceDocumentAvailable()
    {
        var detail = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateApplication", "Detail.cshtml");

        Assert.Contains("CanAccessExamEntranceDocument", detail, StringComparison.Ordinal);
        Assert.Contains(
            "Belge doğrulama bilgileri, başvuru süresi sona erdikten sonra görüntülenecektir.",
            detail,
            StringComparison.Ordinal);

        var codeSectionStart = detail.IndexOf("Doğrulama Kodu", StringComparison.Ordinal);
        Assert.True(codeSectionStart >= 0);
        var codeSection = detail[codeSectionStart..];
        Assert.Contains("@if (Model.CanAccessExamEntranceDocument)", codeSection, StringComparison.Ordinal);
        Assert.Contains("@Model.VerificationCode", codeSection, StringComparison.Ordinal);

        var hiddenBranchStart = codeSection.IndexOf("else", StringComparison.Ordinal);
        var hiddenBranch = codeSection[hiddenBranchStart..];
        Assert.DoesNotContain("@Model.VerificationCode", hiddenBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("DocumentVerification/Verify", hiddenBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("QrCode", hiddenBranch, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentVerificationService_UsesSharedEntranceAccessPolicy()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Application", "Services", "DocumentVerificationService.cs");

        Assert.Contains("ExamEntranceDocumentAccess.IsAvailable", source, StringComparison.Ordinal);
        Assert.Contains("TimeProvider", source, StringComparison.Ordinal);
        Assert.Contains("NotYetAvailableResult", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoleNames.SuperAdmin", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationManager", source, StringComparison.Ordinal);
    }

    private void SetupValidVerification(
        string firstName = "Ali",
        string lastName = "Veli",
        string examTitle = "Sınav",
        int candidateNo = 7)
    {
        _verificationRepo.Setup(r => r.GetByCodeAsync(ValidCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentVerification
            {
                Id = Guid.NewGuid(),
                ApplicationId = ApplicationId,
                VerificationCode = ValidCode,
                IsValid = true
            });
        _appRepo.Setup(r => r.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplicationDetail
            {
                ApplicationId = ApplicationId,
                ExamTitle = examTitle,
                CandidateNo = candidateNo,
                FirstName = firstName,
                LastName = lastName,
                VerificationCode = ValidCode,
                ExamPeriodEndDate = ApplicationEnd,
                CanAccessExamEntranceDocument = true
            });
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

using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.DTOs.ExamResults;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExamResultServiceTests
{
    private readonly Mock<ICandidateApplicationRepository> _appRepo = new();
    private readonly Mock<ICandidateExamResultRepository> _resultRepo = new();
    private readonly Mock<IExamPeriodManagerRepository> _managerRepo = new();

    private static readonly Guid ExamId = Guid.NewGuid();
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly Guid ApplicationId = Guid.NewGuid();
    private static readonly Guid SuperAdminId = Guid.NewGuid();

    private ExamResultService CreateSut() =>
        new(_appRepo.Object, _resultRepo.Object, _managerRepo.Object);

    private void SetupApplication()
    {
        _appRepo.Setup(r => r.GetDetailByIdAsync(ApplicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplicationDetail { ApplicationId = ApplicationId, ExamPeriodId = ExamId });
    }

    private static ExamResultFormViewModel AttendedModel(decimal? score = 85, string? description = null) => new()
    {
        ApplicationId = ApplicationId,
        AttendanceStatus = AttendanceStatus.Attended,
        ExamScore = score,
        AdminDescription = description
    };

    [Fact]
    public async Task GetCandidates_SuperAdmin_HasFullAccess()
    {
        _appRepo.Setup(r => r.GetDetailsByExamPeriodAsync(ExamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandidateApplicationDetail>());

        var result = await CreateSut().GetCandidatesForExamAsync(ExamId, Guid.NewGuid(), DomainConstants.RoleNames.SuperAdmin);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetCandidates_AssignedManager_HasAccess()
    {
        _managerRepo.Setup(r => r.IsManagerOfExamPeriodAsync(ManagerId, ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _appRepo.Setup(r => r.GetDetailsByExamPeriodAsync(ExamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandidateApplicationDetail>());

        var result = await CreateSut().GetCandidatesForExamAsync(ExamId, ManagerId, DomainConstants.RoleNames.ApplicationManager);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GetCandidates_UnassignedManager_IsDenied()
    {
        _managerRepo.Setup(r => r.IsManagerOfExamPeriodAsync(ManagerId, ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().GetCandidatesForExamAsync(ExamId, ManagerId, DomainConstants.RoleNames.ApplicationManager);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetCandidates_Candidate_IsDenied()
    {
        var result = await CreateSut().GetCandidatesForExamAsync(ExamId, Guid.NewGuid(), DomainConstants.RoleNames.Candidate);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SaveResult_UndefinedAttendanceStatus_Fails()
    {
        SetupApplication();
        var model = new ExamResultFormViewModel
        {
            ApplicationId = ApplicationId,
            AttendanceStatus = (AttendanceStatus)99,
            ExamScore = 50
        };

        var result = await CreateSut().SaveResultAsync(model, SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);
        Assert.False(result.Success);
        Assert.Equal("Geçerli bir sınava girme durumu seçiniz.", result.ErrorMessage);
        Assert.DoesNotContain("Sql", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveResult_AttendedWithoutScore_Fails()
    {
        SetupApplication();
        var result = await CreateSut().SaveResultAsync(AttendedModel(null), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task SaveResult_AttendedBoundaryScores_Succeed(int score)
    {
        SetupApplication();
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var result = await CreateSut().SaveResultAsync(AttendedModel(score), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);
        Assert.True(result.Success);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task SaveResult_ScoreOutOfRange_Fails(int score)
    {
        SetupApplication();
        var result = await CreateSut().SaveResultAsync(AttendedModel(score), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(AttendanceStatus.NotAttended)]
    [InlineData(AttendanceStatus.Cancelled)]
    [InlineData(AttendanceStatus.Disqualified)]
    public async Task SaveResult_NonAttended_ForcesNullScore(AttendanceStatus status)
    {
        SetupApplication();
        ExamResultWriteRequest? captured = null;
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ExamResultWriteRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var model = new ExamResultFormViewModel
        {
            ApplicationId = ApplicationId,
            AttendanceStatus = status,
            ExamScore = 77
        };

        var result = await CreateSut().SaveResultAsync(model, SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(status, captured!.AttendanceStatus);
        Assert.Null(captured!.ExamScore);
    }

    [Fact]
    public async Task SaveResult_Description2000_Accepted()
    {
        SetupApplication();
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var result = await CreateSut().SaveResultAsync(
            AttendedModel(50, new string('a', 2000)), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task SaveResult_Description2001_Rejected()
    {
        SetupApplication();
        var result = await CreateSut().SaveResultAsync(
            AttendedModel(50, new string('a', 2001)), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.False(result.Success);
        _resultRepo.Verify(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveResult_InvalidAdminDescription_FailsWithoutRepositoryWrite()
    {
        SetupApplication();
        var model = AttendedModel(40, "--------");

        var result = await CreateSut().SaveResultAsync(model, SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.False(result.Success);
        Assert.Equal(InputTextRules.FormatMeaningfulTextMessage("Yönetici Açıklaması"), result.ErrorMessage);
        _resultRepo.Verify(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveResult_BlankDescription_NormalizedToNullAndVisibilityFalse()
    {
        SetupApplication();
        ExamResultWriteRequest? captured = null;
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ExamResultWriteRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var model = AttendedModel(40, "   ");
        model.IsDescriptionVisibleToCandidate = true;

        var result = await CreateSut().SaveResultAsync(model, SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Null(captured!.AdminDescription);
        Assert.False(captured.IsDescriptionVisibleToCandidate);
    }

    [Fact]
    public async Task SaveResult_SuperAdmin_CanSave()
    {
        SetupApplication();
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var result = await CreateSut().SaveResultAsync(AttendedModel(), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);
        Assert.True(result.Success);
        _resultRepo.Verify(r => r.SaveResultAtomicAsync(
            It.Is<ExamResultWriteRequest>(x => x.IsSuperAdmin && x.ExamScore == 85),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveResult_AssignedManager_CanSave()
    {
        SetupApplication();
        _managerRepo.Setup(r => r.IsManagerOfExamPeriodAsync(ManagerId, ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var result = await CreateSut().SaveResultAsync(AttendedModel(), ManagerId, DomainConstants.RoleNames.ApplicationManager, null);
        Assert.True(result.Success);
        _resultRepo.Verify(r => r.SaveResultAtomicAsync(
            It.Is<ExamResultWriteRequest>(x => !x.IsSuperAdmin && x.EvaluatorUserId == ManagerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveResult_UnassignedManager_IsDenied()
    {
        SetupApplication();
        _managerRepo.Setup(r => r.IsManagerOfExamPeriodAsync(ManagerId, ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().SaveResultAsync(
            new ExamResultFormViewModel { ApplicationId = ApplicationId, AttendanceStatus = AttendanceStatus.NotAttended },
            ManagerId, DomainConstants.RoleNames.ApplicationManager, null);

        Assert.False(result.Success);
        _resultRepo.Verify(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveResult_AssignmentRemovedAfterServiceCheck_AtomicDenies()
    {
        SetupApplication();
        _managerRepo.Setup(r => r.IsManagerOfExamPeriodAsync(ManagerId, ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Fail(ExamResultWriteStatus.ApplicationNotFoundOrUnauthorized));

        var result = await CreateSut().SaveResultAsync(AttendedModel(), ManagerId, DomainConstants.RoleNames.ApplicationManager, null);

        Assert.False(result.Success);
        Assert.Equal("Başvuru bulunamadı veya bu işlem için yetkiniz yok.", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveResult_MissingAndUnauthorized_SameMessage()
    {
        var missingId = Guid.NewGuid();
        _appRepo.Setup(r => r.GetDetailByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CandidateApplicationDetail?)null);
        SetupApplication();
        _managerRepo.Setup(r => r.IsManagerOfExamPeriodAsync(ManagerId, ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var missing = await CreateSut().SaveResultAsync(
            new ExamResultFormViewModel { ApplicationId = missingId, AttendanceStatus = AttendanceStatus.NotAttended },
            ManagerId, DomainConstants.RoleNames.ApplicationManager, null);
        var unauthorized = await CreateSut().SaveResultAsync(
            new ExamResultFormViewModel { ApplicationId = ApplicationId, AttendanceStatus = AttendanceStatus.NotAttended },
            ManagerId, DomainConstants.RoleNames.ApplicationManager, null);

        Assert.False(missing.Success);
        Assert.False(unauthorized.Success);
        Assert.Equal(missing.ErrorMessage, unauthorized.ErrorMessage);
    }

    [Fact]
    public async Task SaveResult_ParallelFirstInsert_SecondGetsConcurrencyConflictWithoutSqlLeak()
    {
        SetupApplication();
        var call = 0;
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                call++;
                return call == 1
                    ? ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow)
                    : ExamResultWriteResult.Fail(ExamResultWriteStatus.ConcurrencyConflict);
            });

        var first = await CreateSut().SaveResultAsync(AttendedModel(), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);
        var second = await CreateSut().SaveResultAsync(AttendedModel(), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("başka bir kullanıcı tarafından değiştirildi", second.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sql", second.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", second.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveResult_StaleExpectedUpdatedDate_Rejected()
    {
        SetupApplication();
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Fail(ExamResultWriteStatus.ConcurrencyConflict));

        var model = AttendedModel();
        model.ExpectedUpdatedDate = DateTime.UtcNow.AddMinutes(-5);

        var result = await CreateSut().SaveResultAsync(model, SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.False(result.Success);
        Assert.Contains("başka bir kullanıcı tarafından değiştirildi", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveResult_ValidAttended_Succeeds()
    {
        SetupApplication();
        _resultRepo.Setup(r => r.SaveResultAtomicAsync(It.IsAny<ExamResultWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExamResultWriteResult.Ok(Guid.NewGuid(), DateTime.UtcNow));

        var result = await CreateSut().SaveResultAsync(AttendedModel(85), SuperAdminId, DomainConstants.RoleNames.SuperAdmin, null);

        Assert.True(result.Success);
        _resultRepo.Verify(r => r.SaveResultAtomicAsync(
            It.Is<ExamResultWriteRequest>(x => x.ExamScore == 85 && x.AttendanceStatus == AttendanceStatus.Attended),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

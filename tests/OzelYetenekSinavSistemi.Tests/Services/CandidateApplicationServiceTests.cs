using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Applications;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class CandidateApplicationServiceTests
{
    private readonly Mock<IExamPeriodRepository> _examRepo = new();
    private readonly Mock<IExamPreferenceOptionRepository> _optionRepo = new();
    private readonly Mock<ICandidateApplicationRepository> _appRepo = new();
    private readonly Mock<IAuditService> _audit = new();

    private static readonly Guid ExamId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Option1 = Guid.NewGuid();
    private static readonly Guid Option2 = Guid.NewGuid();
    private static readonly Guid ForeignOption = Guid.NewGuid();

    private CandidateApplicationService CreateSut() =>
        new(_examRepo.Object, _optionRepo.Object, _appRepo.Object, _audit.Object);

    private static ExamPeriod OpenExam(int maxPreferences = 2) => new()
    {
        Id = ExamId,
        Title = "Test Sınavı",
        IsActive = true,
        IsClosed = false,
        IsDeleted = false,
        StartDate = DateTime.Now.AddDays(-1),
        EndDate = DateTime.Now.AddDays(1),
        MaxPreferences = maxPreferences
    };

    private void SetupExam(ExamPeriod exam)
    {
        _examRepo.Setup(r => r.GetByIdAsync(ExamId, It.IsAny<CancellationToken>())).ReturnsAsync(exam);
        _optionRepo.Setup(r => r.GetActiveByExamPeriodAsync(ExamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExamPreferenceOption>
            {
                new() { Id = Option1, ExamPeriodId = ExamId, PreferenceName = "Resim", IsActive = true },
                new() { Id = Option2, ExamPeriodId = ExamId, PreferenceName = "Müzik", IsActive = true }
            });
        _appRepo.Setup(r => r.GetByUserAndExamAsync(UserId, ExamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CandidateApplication?)null);
    }

    private static CreateApplicationViewModel Model(params Guid[] ids) => new()
    {
        ExamPeriodId = ExamId,
        SelectedPreferenceOptionIds = ids.ToList()
    };

    [Fact]
    public async Task ApplyAsync_InactiveExam_Fails()
    {
        var exam = OpenExam();
        exam.IsActive = false;
        SetupExam(exam);

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_ClosedExam_Fails()
    {
        var exam = OpenExam();
        exam.IsClosed = true;
        SetupExam(exam);

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_DeletedExam_Fails()
    {
        var exam = OpenExam();
        exam.IsDeleted = true;
        SetupExam(exam);

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_NotStartedExam_Fails()
    {
        var exam = OpenExam();
        exam.StartDate = DateTime.Now.AddDays(2);
        exam.EndDate = DateTime.Now.AddDays(5);
        SetupExam(exam);

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_EndedExam_Fails()
    {
        var exam = OpenExam();
        exam.StartDate = DateTime.Now.AddDays(-5);
        exam.EndDate = DateTime.Now.AddDays(-1);
        SetupExam(exam);

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_EmptyPreferences_Fails()
    {
        SetupExam(OpenExam());
        var result = await CreateSut().ApplyAsync(UserId, Model(), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_TooManyPreferences_Fails()
    {
        SetupExam(OpenExam(maxPreferences: 1));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1, Option2), null, null);
        Assert.False(result.Success);
        Assert.Equal("En fazla 1 tercih seçebilirsiniz.", result.ErrorMessage);
    }

    [Fact]
    public async Task ApplyAsync_DuplicatePreference_Fails()
    {
        SetupExam(OpenExam(maxPreferences: 3));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1, Option1), null, null);
        Assert.False(result.Success);
        Assert.Equal("Aynı tercih birden fazla kez seçilemez.", result.ErrorMessage);
    }

    [Fact]
    public async Task ApplyAsync_GuidEmptyPreference_Fails()
    {
        SetupExam(OpenExam());
        var result = await CreateSut().ApplyAsync(UserId, Model(Guid.Empty), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_PreferenceFromAnotherExam_Fails()
    {
        SetupExam(OpenExam());

        var result = await CreateSut().ApplyAsync(UserId, Model(ForeignOption), null, null);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ApplyAsync_SecondApplicationToSameExam_Fails()
    {
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.GetByUserAndExamAsync(UserId, ExamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = Guid.NewGuid(), UserId = UserId, ExamPeriodId = ExamId });

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
        Assert.Contains("zaten başvurdunuz", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_Valid_ReturnsCandidateNo()
    {
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.CreateApplicationAtomicAsync(It.IsAny<CandidateApplicationCreationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Ok(Guid.NewGuid(), 1, "ABC123"));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1, Option2), "127.0.0.1", "corr-1");

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.CandidateNo);
    }

    [Fact]
    public async Task ApplyAsync_AtomicAlreadyApplied_DoesNotLeakSql()
    {
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.CreateApplicationAtomicAsync(It.IsAny<CandidateApplicationCreationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.AlreadyApplied));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);

        Assert.False(result.Success);
        Assert.DoesNotContain("Sql", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zaten başvurdunuz", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_AtomicConflict_MapsSafely()
    {
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.CreateApplicationAtomicAsync(It.IsAny<CandidateApplicationCreationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.Conflict));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);

        Assert.False(result.Success);
        Assert.Contains("çakıştı", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_ParallelSecondRequest_GetsAlreadyApplied()
    {
        SetupExam(OpenExam());
        var call = 0;
        _appRepo.Setup(r => r.CreateApplicationAtomicAsync(It.IsAny<CandidateApplicationCreationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                call++;
                return call == 1
                    ? ApplicationWriteResult.Ok(Guid.NewGuid(), 7, "CODE")
                    : ApplicationWriteResult.Fail(ApplicationWriteStatus.AlreadyApplied);
            });

        var first = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        var second = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("zaten başvurdunuz", second.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_AtomicExamNotOpen_MapsMessage()
    {
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.CreateApplicationAtomicAsync(It.IsAny<CandidateApplicationCreationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.ExamNotOpen));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
        Assert.Contains("şu anda başvuru yapılamaz", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAsync_AtomicInvalidPreference_MapsMessage()
    {
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.CreateApplicationAtomicAsync(It.IsAny<CandidateApplicationCreationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.InvalidOrInactivePreference));

        var result = await CreateSut().ApplyAsync(UserId, Model(Option1), null, null);
        Assert.False(result.Success);
        Assert.Contains("kullanılamıyor", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateApplicationAsync_WrongOwner_FailsWithGenericMessage()
    {
        var applicationId = Guid.NewGuid();
        _appRepo.Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = applicationId, UserId = Guid.NewGuid(), ExamPeriodId = ExamId });

        var result = await CreateSut().UpdateApplicationAsync(UserId, applicationId, Model(Option1), null);
        Assert.False(result.Success);
        Assert.Contains("yetkiniz yok", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateApplicationAsync_MissingAndUnauthorized_SameMessage()
    {
        var missingId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        _appRepo.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CandidateApplication?)null);
        _appRepo.Setup(r => r.GetByIdAsync(otherId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = otherId, UserId = Guid.NewGuid(), ExamPeriodId = ExamId });

        var missing = await CreateSut().UpdateApplicationAsync(UserId, missingId, Model(Option1), null);
        var unauthorized = await CreateSut().UpdateApplicationAsync(UserId, otherId, Model(Option1), null);

        Assert.False(missing.Success);
        Assert.False(unauthorized.Success);
        Assert.Equal(missing.ErrorMessage, unauthorized.ErrorMessage);
    }

    [Fact]
    public async Task UpdateApplicationAsync_AtomicExamNotOpen_DoesNotAudit()
    {
        var applicationId = Guid.NewGuid();
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = applicationId, UserId = UserId, ExamPeriodId = ExamId });
        _appRepo.Setup(r => r.UpdatePreferencesAtomicAsync(applicationId, UserId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.ExamNotOpen));

        var result = await CreateSut().UpdateApplicationAsync(UserId, applicationId, Model(Option1), null);

        Assert.False(result.Success);
        Assert.Contains("güncelleme süresi dışında", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        _audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateApplicationAsync_Success_WritesAudit()
    {
        var applicationId = Guid.NewGuid();
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = applicationId, UserId = UserId, ExamPeriodId = ExamId });
        _appRepo.Setup(r => r.UpdatePreferencesAtomicAsync(applicationId, UserId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.OkUpdated(applicationId));

        var result = await CreateSut().UpdateApplicationAsync(UserId, applicationId, Model(Option1), "127.0.0.1");

        Assert.True(result.Success);
        _audit.Verify(a => a.LogAsync("ApplicationUpdated", It.IsAny<string>(), UserId, "127.0.0.1", null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateApplicationAsync_AtomicUnauthorized_GenericMessage()
    {
        var applicationId = Guid.NewGuid();
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = applicationId, UserId = UserId, ExamPeriodId = ExamId });
        _appRepo.Setup(r => r.UpdatePreferencesAtomicAsync(applicationId, UserId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.ApplicationNotFoundOrUnauthorized));

        var result = await CreateSut().UpdateApplicationAsync(UserId, applicationId, Model(Option1), null);
        Assert.False(result.Success);
        Assert.Equal("Başvuru bulunamadı veya bu işlem için yetkiniz yok.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateApplicationAsync_AtomicInvalidPreference_DoesNotAudit()
    {
        var applicationId = Guid.NewGuid();
        SetupExam(OpenExam());
        _appRepo.Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = applicationId, UserId = UserId, ExamPeriodId = ExamId });
        _appRepo.Setup(r => r.UpdatePreferencesAtomicAsync(applicationId, UserId, It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationWriteResult.Fail(ApplicationWriteStatus.InvalidOrInactivePreference));

        var result = await CreateSut().UpdateApplicationAsync(UserId, applicationId, Model(Option1), null);

        Assert.False(result.Success);
        Assert.Contains("kullanılamıyor", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        _audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateApplicationAsync_ClosedExam_KeepsEarlyFailWithoutAtomicCall()
    {
        var applicationId = Guid.NewGuid();
        var exam = OpenExam();
        exam.IsClosed = true;
        SetupExam(exam);
        _appRepo.Setup(r => r.GetByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplication { Id = applicationId, UserId = UserId, ExamPeriodId = ExamId });

        var result = await CreateSut().UpdateApplicationAsync(UserId, applicationId, Model(Option1), null);

        Assert.False(result.Success);
        Assert.Contains("güncelleme süresi dışında", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        _appRepo.Verify(r => r.UpdatePreferencesAtomicAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMyApplicationDetailAsync_WrongOwner_Fails()
    {
        var applicationId = Guid.NewGuid();
        _appRepo.Setup(r => r.GetDetailByIdAsync(applicationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CandidateApplicationDetail { ApplicationId = applicationId, UserId = Guid.NewGuid() });

        var result = await CreateSut().GetMyApplicationDetailAsync(applicationId, UserId);
        Assert.False(result.Success);
    }
}

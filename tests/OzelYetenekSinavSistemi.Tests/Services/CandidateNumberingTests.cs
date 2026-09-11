using Moq;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Applications;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

/// <summary>
/// CandidateNo'nun sınav dönemi bazında ardışık üretildiğini ve farklı sınav
/// dönemlerindeki sayaçların birbirinden bağımsız olduğunu, atomik oluşturma
/// sözleşmesini taklit eden bellek içi bir repository ile doğrular.
/// </summary>
public sealed class CandidateNumberingTests
{
    private static readonly Guid Exam1 = Guid.NewGuid();
    private static readonly Guid Exam2 = Guid.NewGuid();
    private static readonly Guid Option1 = Guid.NewGuid();

    private static (CandidateApplicationService sut, FakeApplicationRepository repo) BuildSut()
    {
        var examRepo = new Mock<IExamPeriodRepository>();
        var optionRepo = new Mock<IExamPreferenceOptionRepository>();
        var audit = new Mock<IAuditService>();

        void SetupExam(Guid examId)
        {
            examRepo.Setup(r => r.GetByIdAsync(examId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExamPeriod
                {
                    Id = examId,
                    IsActive = true,
                    IsClosed = false,
                    IsDeleted = false,
                    StartDate = DateTime.Now.AddDays(-1),
                    EndDate = DateTime.Now.AddDays(1),
                    MaxPreferences = 3
                });
            optionRepo.Setup(r => r.GetActiveByExamPeriodAsync(examId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ExamPreferenceOption>
                {
                    new() { Id = Option1, ExamPeriodId = examId, IsActive = true, PreferenceName = "Bölüm" }
                });
        }

        SetupExam(Exam1);
        SetupExam(Exam2);

        var repo = new FakeApplicationRepository();
        var sut = new CandidateApplicationService(examRepo.Object, optionRepo.Object, repo, audit.Object);
        return (sut, repo);
    }

    private static CreateApplicationViewModel Model(Guid examId) => new()
    {
        ExamPeriodId = examId,
        SelectedPreferenceOptionIds = new List<Guid> { Option1 }
    };

    [Fact]
    public async Task CandidateNo_IsSequentialPerExamPeriod()
    {
        var (sut, _) = BuildSut();

        var first = await sut.ApplyAsync(Guid.NewGuid(), Model(Exam1), null, null);
        var second = await sut.ApplyAsync(Guid.NewGuid(), Model(Exam1), null, null);
        var third = await sut.ApplyAsync(Guid.NewGuid(), Model(Exam1), null, null);

        Assert.Equal(1, first.Data!.CandidateNo);
        Assert.Equal(2, second.Data!.CandidateNo);
        Assert.Equal(3, third.Data!.CandidateNo);
    }

    [Fact]
    public async Task CandidateNo_CountersAreIndependentAcrossExamPeriods()
    {
        var (sut, _) = BuildSut();

        await sut.ApplyAsync(Guid.NewGuid(), Model(Exam1), null, null);
        await sut.ApplyAsync(Guid.NewGuid(), Model(Exam1), null, null);

        var firstOfExam2 = await sut.ApplyAsync(Guid.NewGuid(), Model(Exam2), null, null);

        // Exam1'de iki aday olsa da Exam2'nin ilk adayı 1 olmalıdır.
        Assert.Equal(1, firstOfExam2.Data!.CandidateNo);
    }

    /// <summary>
    /// Atomik CandidateNo üretimini taklit eden bellek içi repository.
    /// Gerçek sıralama ve kilitleme SQL Server transaction'ında yapılır.
    /// </summary>
    private sealed class FakeApplicationRepository : ICandidateApplicationRepository
    {
        private readonly Dictionary<Guid, int> _counters = new();
        private readonly object _lock = new();

        public Task<ApplicationWriteResult> CreateApplicationAtomicAsync(
            CandidateApplicationCreationRequest request, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _counters.TryGetValue(request.ExamPeriodId, out var last);
                var next = last + 1;
                _counters[request.ExamPeriodId] = next;

                return Task.FromResult(ApplicationWriteResult.Ok(
                    Guid.NewGuid(),
                    next,
                    Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()));
            }
        }

        public Task<CandidateApplication?> GetByUserAndExamAsync(Guid userId, Guid examPeriodId, CancellationToken cancellationToken = default)
            => Task.FromResult<CandidateApplication?>(null);

        public Task<CandidateApplicationDetail?> GetDetailByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CandidateApplicationDetail>> GetDetailsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CandidateApplicationDetail>> GetDetailsByExamPeriodAsync(Guid examPeriodId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CandidateSelectedPreference>> GetSelectedPreferencesAsync(Guid applicationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ApplicationWriteResult> UpdatePreferencesAtomicAsync(Guid applicationId, Guid userId, IReadOnlyList<Guid> orderedPreferenceOptionIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CandidateApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CandidateApplication>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Guid> AddAsync(CandidateApplication entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> UpdateAsync(CandidateApplication entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

using Moq;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Tests.Services;

/// <summary>
/// High-value coverage for multi-active exam periods and manager IDOR-style access denial.
/// </summary>
public sealed class ExamPeriodAccessAndMultiActiveTests
{
    private readonly Mock<IExamPeriodRepository> _examRepo = new(MockBehavior.Strict);
    private readonly Mock<IExamPreferenceOptionRepository> _optionRepo = new(MockBehavior.Strict);
    private readonly Mock<IExamPeriodManagerRepository> _managerRepo = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<IAuditService> _audit = new(MockBehavior.Strict);
    private readonly Mock<ICandidateApplicationRepository> _appRepo = new(MockBehavior.Strict);

    private ExamPeriodService CreateExamPeriodService() =>
        new(_examRepo.Object, _optionRepo.Object, _managerRepo.Object, _userRepo.Object, _audit.Object);

    private CandidateApplicationService CreateCandidateApplicationService() =>
        new(_examRepo.Object, _optionRepo.Object, _appRepo.Object, _audit.Object);

    [Fact]
    public async Task GetActiveExamPeriodsAsync_ReturnsAllOverlappingActivePeriods()
    {
        var now = DateTime.Now;
        var periods = new List<ExamPeriod>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Dönem A",
                IsActive = true,
                IsClosed = false,
                IsDeleted = false,
                StartDate = now.AddDays(-2),
                EndDate = now.AddDays(5),
                MaxPreferences = 2
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Dönem B",
                IsActive = true,
                IsClosed = false,
                IsDeleted = false,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(10),
                MaxPreferences = 3
            }
        };

        _examRepo
            .Setup(r => r.GetActiveForCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(periods);

        var result = await CreateCandidateApplicationService().GetActiveExamPeriodsAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Title == "Dönem A");
        Assert.Contains(result, p => p.Title == "Dönem B");
        _examRepo.Verify(
            r => r.GetActiveForCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdForUserAsync_ApplicationManager_NotAssigned_IsDenied()
    {
        var examId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var period = new ExamPeriod
        {
            Id = examId,
            Title = "Yetkisiz Dönem",
            IsDeleted = false,
            IsActive = true,
            IsClosed = false,
            StartDate = DateTime.Now.AddDays(-1),
            EndDate = DateTime.Now.AddDays(1),
            MaxPreferences = 1
        };

        _examRepo.Setup(r => r.GetByIdAsync(examId, It.IsAny<CancellationToken>())).ReturnsAsync(period);
        _managerRepo
            .Setup(r => r.IsManagerOfExamPeriodAsync(managerId, examId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateExamPeriodService().GetByIdForUserAsync(
            examId,
            managerId,
            DomainConstants.RoleNames.ApplicationManager);

        Assert.False(result.Success);
        Assert.Equal("Bu sınav dönemine erişim yetkiniz yok.", result.ErrorMessage);
        _managerRepo.Verify(
            r => r.IsManagerOfExamPeriodAsync(managerId, examId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetVisibleForUserAsync_ApplicationManager_SeesOnlyAssignedPeriods()
    {
        var managerId = Guid.NewGuid();
        var assigned = new List<ExamPeriod>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Atanmış",
                IsDeleted = false,
                IsActive = true,
                IsClosed = false,
                StartDate = DateTime.Now.AddDays(-1),
                EndDate = DateTime.Now.AddDays(1),
                MaxPreferences = 1
            }
        };

        _examRepo
            .Setup(r => r.GetByManagerAsync(managerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assigned);

        var result = await CreateExamPeriodService().GetVisibleForUserAsync(
            managerId,
            DomainConstants.RoleNames.ApplicationManager);

        Assert.Single(result);
        Assert.Equal("Atanmış", result[0].Title);
        _examRepo.Verify(r => r.GetAllNotDeletedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetActiveForCandidatesSql_DoesNotForceSingleActiveRow()
    {
        var source = File.ReadAllText(FindUnder(
            "src",
            "OzelYetenekSinavSistemi.Infrastructure",
            "Persistence",
            "Repositories",
            "ExamPeriodRepository.cs"));

        Assert.Contains("GetActiveForCandidatesAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsActive = 1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TOP 1", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IReadOnlyList<ExamPeriod>", source, StringComparison.Ordinal);
    }

    private static string FindUnder(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(path) || Directory.Exists(path))
                return path;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

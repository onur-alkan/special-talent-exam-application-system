using System.Globalization;
using System.Text.RegularExpressions;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Application.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using OzelYetenekSinavSistemi.Domain.Enums;
using Moq;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExamResultScoreOrderingTests
{
    [Fact]
    public void OrderByExamScoreDescending_ManualAcceptanceScenario_IsCorrect()
    {
        // A:90.25, B:null, C:95.50, D:9.75, E:95.50 — eşitlikte aday no artan
        var rows = new[]
        {
            Detail(candidateNo: 3, score: 90.25m, label: "A"),
            Detail(candidateNo: 5, score: null, label: "B", status: AttendanceStatus.NotAttended),
            Detail(candidateNo: 1, score: 95.50m, label: "C"),
            Detail(candidateNo: 4, score: 9.75m, label: "D"),
            Detail(candidateNo: 2, score: 95.50m, label: "E")
        };

        var ordered = CandidateResultOrdering.OrderByExamScoreDescending(rows);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ordered.Select(r => r.CandidateNo).ToArray());
        Assert.Equal(new decimal?[] { 95.50m, 95.50m, 90.25m, 9.75m, null }, ordered.Select(r => r.ExamScore).ToArray());
        Assert.Equal("C", ordered[0].FirstName);
        Assert.Equal("E", ordered[1].FirstName);
        Assert.Equal("A", ordered[2].FirstName);
        Assert.Equal("D", ordered[3].FirstName);
        Assert.Equal("B", ordered[4].FirstName);
    }

    [Fact]
    public void OrderByExamScoreDescending_NullAndCancelledWithoutScore_ComeLast()
    {
        var rows = new[]
        {
            Detail(10, 50m, "Scored"),
            Detail(11, null, "Missing", AttendanceStatus.Cancelled),
            Detail(12, null, "NotAttended", AttendanceStatus.NotAttended),
            Detail(9, 80m, "Higher")
        };

        var ordered = CandidateResultOrdering.OrderByExamScoreDescending(rows);
        Assert.Equal(new[] { 9, 10, 11, 12 }, ordered.Select(r => r.CandidateNo).ToArray());
        Assert.All(ordered.Take(2), r => Assert.True(r.ExamScore.HasValue));
        Assert.All(ordered.Skip(2), r => Assert.False(r.ExamScore.HasValue));
    }

    [Fact]
    public void OrderByExamScoreDescending_DoesNotInventZeroForMissingScores()
    {
        var ordered = CandidateResultOrdering.OrderByExamScoreDescending(new[]
        {
            Detail(1, null, "X"),
            Detail(2, 0m, "Zero")
        });

        Assert.Equal(0m, ordered[0].ExamScore);
        Assert.Null(ordered[1].ExamScore);
    }

    [Theory]
    [InlineData(95.50, "95.5")]
    [InlineData(90.25, "90.25")]
    [InlineData(9.75, "9.75")]
    public void ToDataTablesOrderValue_IsCultureIndependentNumeric(decimal score, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, CandidateResultOrdering.ToDataTablesOrderValue(score));
            Assert.DoesNotContain(",", CandidateResultOrdering.ToDataTablesOrderValue(score), StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ToDataTablesOrderValue_MissingScore_UsesSortSentinelNotDisplayedZero()
    {
        Assert.Equal(CandidateResultOrdering.MissingScoreSortKey, CandidateResultOrdering.ToDataTablesOrderValue(null));
        Assert.Equal("-1", CandidateResultOrdering.ToDataTablesOrderValue(null));
        Assert.NotEqual("0", CandidateResultOrdering.ToDataTablesOrderValue(null));
    }

    [Fact]
    public async Task GetCandidatesForExamAsync_AppliesScoreOrdering()
    {
        var examId = Guid.NewGuid();
        var appRepo = new Mock<ICandidateApplicationRepository>();
        var resultRepo = new Mock<ICandidateExamResultRepository>();
        var managerRepo = new Mock<IExamPeriodManagerRepository>();

        appRepo.Setup(r => r.GetDetailsByExamPeriodAsync(examId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CandidateApplicationDetail>
            {
                Detail(3, 90.25m, "A"),
                Detail(5, null, "B"),
                Detail(1, 95.50m, "C"),
                Detail(4, 9.75m, "D"),
                Detail(2, 95.50m, "E")
            });

        var sut = new ExamResultService(appRepo.Object, resultRepo.Object, managerRepo.Object);
        var result = await sut.GetCandidatesForExamAsync(examId, Guid.NewGuid(), DomainConstants.RoleNames.SuperAdmin);

        Assert.True(result.Success);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.Data!.Select(r => r.CandidateNo).ToArray());
    }

    [Fact]
    public void ExamResultList_DefaultsToScoreDescendingWithCandidateTieBreak()
    {
        var list = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "List.cshtml");
        var siteJs = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");

        Assert.Contains("data-order-col=\"3\"", list, StringComparison.Ordinal);
        Assert.Contains("data-order-dir=\"desc\"", list, StringComparison.Ordinal);
        Assert.Contains("data-secondary-order-col=\"0\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain("data-order-col=\"0\"", list, StringComparison.Ordinal);
        Assert.Contains("CandidateResultOrdering.ToDataTablesOrderValue(c.ExamScore)", list, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.GetCultureInfo(\"tr-TR\")", list, StringComparison.Ordinal);
        Assert.Contains("data-order=\"@CandidateResultOrdering.ToDataTablesOrderValue(c.ExamScore)\"", list, StringComparison.Ordinal);

        // Manuel sütun sıralaması: DataTables varsayılanı bozulmadan order API'si korunur.
        Assert.Contains("options.order = [[orderCol, orderDir]]", siteJs, StringComparison.Ordinal);
        Assert.Contains("secondaryOrderCol", siteJs, StringComparison.Ordinal);
        Assert.Contains("options.order.push([secondaryOrderCol, \"asc\"])", siteJs, StringComparison.Ordinal);
    }

    [Fact]
    public void ExamResultList_ScoreColumnIndex_MatchesPuanHeader()
    {
        var list = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "List.cshtml");
        var headers = Regex.Matches(list, @"<th(?:\s[^>]*)?>([^<]+)</th>")
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        Assert.Equal(new[] { "Aday No", "Ad Soyad", "Katılım", "Puan", "İşlem" }, headers);
        Assert.Equal(3, headers.IndexOf("Puan"));
        Assert.Equal(0, headers.IndexOf("Aday No"));
        Assert.Equal(1, headers.IndexOf("Ad Soyad"));
    }

    [Fact]
    public void ExportServices_ConsumeOrderedRowsWithoutReordering()
    {
        var excel = ReadProjectFile("src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "ExcelExportService.cs");
        var pdf = ReadProjectFile("src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "PdfExportService.cs");
        var controller = ReadProjectFile("src", "OzelYetenekSinavSistemi.Web", "Controllers", "ExamResultController.cs");

        Assert.Contains("foreach (var row in rows)", excel, StringComparison.Ordinal);
        Assert.Contains("foreach (var row in rows)", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderBy", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderBy", pdf, StringComparison.Ordinal);
        Assert.Contains("GetCandidatesForExamAsync", controller, StringComparison.Ordinal);
        Assert.Contains("ExportCandidateResults(examResult.Data.Title, candidates.Data)", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveResultAndAttendanceRules_UnchangedInServiceSource()
    {
        var service = ReadProjectFile("src", "OzelYetenekSinavSistemi.Application", "Services", "ExamResultService.cs");
        Assert.Contains("Sınava giren aday için puanı giriniz.", service, StringComparison.Ordinal);
        Assert.Contains("Sınav puanı 0-100 aralığında olmalıdır.", service, StringComparison.Ordinal);
        Assert.Contains("// NotAttended / Cancelled / Disqualified: istemciden gelse bile puan kaydedilmez.", service, StringComparison.Ordinal);
        Assert.Contains("CandidateResultOrdering.OrderByExamScoreDescending(rows)", service, StringComparison.Ordinal);
    }

    private static CandidateApplicationDetail Detail(
        int candidateNo,
        decimal? score,
        string label,
        AttendanceStatus? status = AttendanceStatus.Attended) =>
        new()
        {
            CandidateNo = candidateNo,
            FirstName = label,
            LastName = "Test",
            ExamScore = score,
            AttendanceStatus = status
        };

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

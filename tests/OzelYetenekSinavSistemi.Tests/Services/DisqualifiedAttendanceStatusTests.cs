using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Domain.Extensions;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class DisqualifiedAttendanceStatusTests
{
    [Fact]
    public void Enum_PreservesExistingValues_AndAppendsDisqualified()
    {
        Assert.Equal(1, (int)AttendanceStatus.Attended);
        Assert.Equal(2, (int)AttendanceStatus.NotAttended);
        Assert.Equal(3, (int)AttendanceStatus.Cancelled);
        Assert.Equal(4, (int)AttendanceStatus.Disqualified);
        Assert.Equal(
            (int)AttendanceStatus.Cancelled + 1,
            (int)AttendanceStatus.Disqualified);
    }

    [Theory]
    [InlineData(AttendanceStatus.Attended, "Girdi")]
    [InlineData(AttendanceStatus.NotAttended, "Girmedi")]
    [InlineData(AttendanceStatus.Cancelled, "İptal")]
    [InlineData(AttendanceStatus.Disqualified, "Diskalifiye")]
    public void DisplayText_IsCentralizedAndTurkish(AttendanceStatus status, string expected)
    {
        Assert.Equal(expected, status.ToDisplayText());
    }

    [Fact]
    public void DisplayText_UnknownAndNull_UseFallback()
    {
        Assert.Equal("-", ((AttendanceStatus)99).ToDisplayText());
        Assert.Equal("-", ((AttendanceStatus?)null).ToDisplayText());
    }

    [Fact]
    public void PdfExport_UsesDisqualifiedText_AndProducesPdf()
    {
        Assert.Equal("Diskalifiye", PdfExportService.AttendanceText(AttendanceStatus.Disqualified));

        var bytes = new PdfExportService().ExportCandidateResults(
            "2026 Özel Yetenek Sınavı",
            new[]
            {
                new CandidateApplicationDetail
                {
                    CandidateNo = 10,
                    FirstName = "Ayşe",
                    LastName = "Yılmaz",
                    AttendanceStatus = AttendanceStatus.Disqualified,
                    ExamScore = null
                }
            });

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Views_ExposeDisqualifiedThroughCentralDisplayText()
    {
        var evaluate = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "Evaluate.cshtml");
        var list = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "List.cshtml");
        var candidate = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "CandidateApplication", "Detail.cshtml");
        var resultDocument = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamDocument", "Result.cshtml");

        Assert.Contains("AttendanceStatus.Disqualified", evaluate, StringComparison.Ordinal);
        Assert.Contains("ToDisplayText()", evaluate, StringComparison.Ordinal);
        Assert.Contains("AttendanceStatus.Disqualified => \"text-bg-dark\"", list, StringComparison.Ordinal);
        Assert.Contains("ToDisplayText()", list, StringComparison.Ordinal);
        Assert.Contains("Sınava Girme Durumu", candidate, StringComparison.Ordinal);
        Assert.Contains("ToDisplayText()", candidate, StringComparison.Ordinal);
        Assert.Contains("Sınava Girme Durumu", resultDocument, StringComparison.Ordinal);
        Assert.Contains("ToDisplayText()", resultDocument, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselineSchema_UsesIntAndAllowsAllFourValues_WithoutUnneededMigration()
    {
        var schema = ReadProjectFile("database", "OzelYetenekSinavSistemi.sql");
        var root = FindProjectRoot();

        Assert.Contains("AttendanceStatus               INT           NULL", schema, StringComparison.Ordinal);
        Assert.Contains("CK_CandidateExamResults_AttendanceStatus", schema, StringComparison.Ordinal);
        Assert.Contains("AttendanceStatus IN (1, 2, 3, 4)", schema, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            root, "database", "migrations", "004_AddDisqualifiedAttendanceStatus.sql")));
    }

    [Fact]
    public void Repository_KeepsParameterizedNumericWrites_AndAuditsEnumName()
    {
        var repository = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories",
            "CandidateExamResultRepository.cs");

        Assert.Contains("AttendanceStatus = (int)request.AttendanceStatus", repository, StringComparison.Ordinal);
        Assert.Contains("@AttendanceStatus", repository, StringComparison.Ordinal);
        Assert.Contains("AttendanceStatus={request.AttendanceStatus}", repository, StringComparison.Ordinal);
        Assert.Contains("Enum.IsDefined", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void Disqualified_IsDefinedSelectableAttendanceStatus_WithCentralizedTurkishLabel()
    {
        Assert.True(Enum.IsDefined(typeof(AttendanceStatus), AttendanceStatus.Disqualified));
        Assert.Equal(4, Enum.GetValues<AttendanceStatus>().Length);
        Assert.Equal("Diskalifiye", AttendanceStatus.Disqualified.ToDisplayText());

        var evaluate = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "Evaluate.cshtml");
        Assert.Contains(
            "<option value=\"@((int)AttendanceStatus.Disqualified)\">@AttendanceStatus.Disqualified.ToDisplayText()</option>",
            evaluate,
            StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { FindProjectRoot() }.Concat(parts).ToArray()));

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OzelYetenekSinavSistemi.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }
}

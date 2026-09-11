using ClosedXML.Excel;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExcelExportServiceTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+cmd")]
    [InlineData("-1+2")]
    [InlineData("@SUM(A1)")]
    [InlineData("\t=1+1")]
    [InlineData("\n=1+1")]
    [InlineData("\r=1+1")]
    public void SanitizeSpreadsheetText_NeutralizesFormulaPrefixes(string input)
    {
        var sanitized = ExcelExportService.SanitizeSpreadsheetText(input);
        Assert.StartsWith("'", sanitized, StringComparison.Ordinal);
        Assert.Equal("'" + input, sanitized);
    }

    [Fact]
    public void SanitizeSpreadsheetText_PreservesNormalTurkishText()
    {
        Assert.Equal("Ayşe Yılmaz", ExcelExportService.SanitizeSpreadsheetText("Ayşe Yılmaz"));
    }

    [Fact]
    public void Export_FormulaLikeNames_AreNotFormulas()
    {
        var service = new ExcelExportService();
        var rows = new List<CandidateApplicationDetail>
        {
            new()
            {
                ApplicationId = Guid.NewGuid(),
                CandidateNo = 12,
                FirstName = "=1+1",
                LastName = "Test",
                AttendanceStatus = AttendanceStatus.Attended,
                ExamScore = 88.5m
            }
        };

        var bytes = service.ExportCandidateResults("=Evil Title", rows);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);

        Assert.False(ws.Cell(1, 1).HasFormula);
        Assert.Equal("=Evil Title", ws.Cell(1, 1).GetString());
        Assert.True(string.IsNullOrEmpty(ws.Cell(1, 1).FormulaA1));
        Assert.True(string.IsNullOrEmpty(ws.Cell(1, 1).FormulaR1C1));

        var dataRow = 4;
        Assert.False(ws.Cell(dataRow, 2).HasFormula);
        Assert.Equal("=1+1 Test", ws.Cell(dataRow, 2).GetString());
        Assert.Equal("Girdi", ws.Cell(dataRow, 3).GetString());
        Assert.Equal(12, ws.Cell(dataRow, 1).GetValue<int>());
        Assert.Equal(88.5m, ws.Cell(dataRow, 4).GetValue<decimal>());
    }

    [Fact]
    public void WriteSafeText_NeverSetsFormula()
    {
        using var workbook = new XLWorkbook();
        var cell = workbook.AddWorksheet().Cell(1, 1);
        ExcelExportService.WriteSafeText(cell, "=1+1");

        Assert.False(cell.HasFormula);
        Assert.True(string.IsNullOrEmpty(cell.FormulaA1));
        Assert.True(string.IsNullOrEmpty(cell.FormulaR1C1));
    }

    [Fact]
    public void Export_UsesOnlyReportColumns_WithoutActionColumn()
    {
        var rows = new List<CandidateApplicationDetail>
        {
            new()
            {
                CandidateNo = 7,
                FirstName = "Ayşe",
                LastName = "Yılmaz",
                AttendanceStatus = AttendanceStatus.Attended,
                ExamScore = 90m
            }
        };

        var bytes = new ExcelExportService().ExportCandidateResults("Özel Yetenek Sınavı", rows);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Aday No", ws.Cell(3, 1).GetString());
        Assert.Equal("Ad Soyad", ws.Cell(3, 2).GetString());
        Assert.Equal("Katılım", ws.Cell(3, 3).GetString());
        Assert.Equal("Puan", ws.Cell(3, 4).GetString());
        Assert.True(string.IsNullOrWhiteSpace(ws.Cell(3, 5).GetString()));

        Assert.Equal("Ayşe Yılmaz", ws.Cell(4, 2).GetString());
        Assert.Equal("Girdi", ws.Cell(4, 3).GetString());
        Assert.Equal(90m, ws.Cell(4, 4).GetValue<decimal>());

        Assert.DoesNotContain("İşlem", CollectUsedText(ws));
        Assert.DoesNotContain("Değerlendir", CollectUsedText(ws));
    }

    [Fact]
    public void Export_Disqualified_WritesTurkishStatusAndDashScore()
    {
        var rows = new List<CandidateApplicationDetail>
        {
            new()
            {
                CandidateNo = 44,
                FirstName = "Ayşe",
                LastName = "Yılmaz",
                AttendanceStatus = AttendanceStatus.Disqualified,
                ExamScore = null
            }
        };

        var bytes = new ExcelExportService().ExportCandidateResults("Özel Yetenek Sınavı", rows);
        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Diskalifiye", ws.Cell(4, 3).GetString());
        Assert.Equal("-", ws.Cell(4, 4).GetString());
        Assert.False(ws.Cell(4, 3).HasFormula);
    }

    private static IReadOnlyList<string> CollectUsedText(IXLWorksheet ws)
    {
        var values = new List<string>();
        foreach (var cell in ws.CellsUsed())
            values.Add(cell.GetString());
        return values;
    }
}

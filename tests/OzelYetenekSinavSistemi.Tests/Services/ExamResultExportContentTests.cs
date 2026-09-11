using System.Text;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Infrastructure.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class ExamResultExportContentTests
{
    [Fact]
    public void PdfExport_ProducesDocument_WithDisqualifiedAndWithoutActionColumnsInSource()
    {
        var pdfSource = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "PdfExportService.cs");

        Assert.Contains("HeaderCell(header, \"Aday No\")", pdfSource, StringComparison.Ordinal);
        Assert.Contains("HeaderCell(header, \"Ad Soyad\")", pdfSource, StringComparison.Ordinal);
        Assert.Contains("HeaderCell(header, \"Katılım\")", pdfSource, StringComparison.Ordinal);
        Assert.Contains("HeaderCell(header, \"Puan\")", pdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("İşlem", pdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Değerlendir", pdfSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HeaderCell(header, \"Tercihler\")", pdfSource, StringComparison.Ordinal);
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
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void ExamResultList_KeepsActionColumn_ButMarksItNoExport()
    {
        var list = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "List.cshtml");
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");

        Assert.Contains(">İşlem</th>", list, StringComparison.Ordinal);
        Assert.Contains("class=\"no-export\"", list, StringComparison.Ordinal);
        Assert.Contains("İşlem</th>", list, StringComparison.Ordinal);
        Assert.Contains("class=\"text-end no-export\"", list, StringComparison.Ordinal);
        Assert.Contains("data-export-columns=\"0,1,2,3\"", list, StringComparison.Ordinal);
        Assert.Contains("data-oys-buttons=\"copy,csv,excel,pdf,print\"", list, StringComparison.Ordinal);
        Assert.DoesNotContain("data-buttons=", list, StringComparison.Ordinal);
        Assert.Contains("Değerlendir", list, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"Evaluate\"", list, StringComparison.Ordinal);
        Assert.Contains("oysResolveExportColumnIndexes", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBuildButtonsConfig", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-buttons", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("[DT-EXPORT-DIAG]", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("oysIsDevelopmentClient", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("data-oys-env", siteJs, StringComparison.Ordinal);
        // data-buttons is a DataTables native option — must not be used for our button list.
        Assert.Contains("data-oys-buttons for our button list", siteJs, StringComparison.Ordinal);
        Assert.Contains("exportOptions", siteJs, StringComparison.Ordinal);
        Assert.Contains("extend: \"csvHtml5\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysDownloadUtf8Csv", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBuildSemicolonCsv", siteJs, StringComparison.Ordinal);
        Assert.Contains("new Uint8Array([0xEF, 0xBB, 0xBF])", siteJs, StringComparison.Ordinal);
        Assert.Contains("dt.buttons.exportData", siteJs, StringComparison.Ordinal);
        Assert.Contains("var sep = \";\"", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("bom: true", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("columns: \":not(.no-export)\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("oysBuildPrintCustomize", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-oys-dt-print-fix", siteJs, StringComparison.Ordinal);
        Assert.Contains("extend: \"print\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("height: auto !important", siteJs, StringComparison.Ordinal);

        var siteCss = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "css", "site-security.css");
        Assert.Contains("body.dt-print-view", siteCss, StringComparison.Ordinal);
        Assert.Contains("min-height: 0 !important", siteCss, StringComparison.Ordinal);

        Assert.Contains("copy: \"Kopyala\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("copyTitle: \"Panoya Kopyalandı\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("1: \"1 satır panoya kopyalandı\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("_: \"%d satır panoya kopyalandı\"", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy to clipboard", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Copied ", siteJs, StringComparison.Ordinal);
        Assert.Contains("window.dtLangTR", siteJs, StringComparison.Ordinal);
        Assert.Contains("language: language", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-export-columns=\"0,1,2,3\"", list, StringComparison.Ordinal);

        var adminLayout = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "Shared", "_AdminLayout.cshtml");
        Assert.Contains("~/js/site.js", adminLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("site.min.js", adminLayout, StringComparison.Ordinal);
        Assert.Contains("asp-append-version=\"true\"", adminLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("data-oys-env", adminLayout, StringComparison.Ordinal);
    }

    [Fact]
    public void DataTablesCopyLanguage_IsTurkish_AndKeepsExportColumns()
    {
        var siteJs = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "wwwroot", "js", "site.js");
        var list = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Web", "Views", "ExamResult", "List.cshtml");

        Assert.Contains("copyTitle: \"Panoya Kopyalandı\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("1: \"1 satır panoya kopyalandı\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("_: \"%d satır panoya kopyalandı\"", siteJs, StringComparison.Ordinal);
        Assert.Contains("copy: \"Kopyala\"", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Copy to clipboard", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Copied one row", siteJs, StringComparison.Ordinal);
        Assert.DoesNotContain("Copied %d rows", siteJs, StringComparison.Ordinal);
        Assert.Contains("data-export-columns=\"0,1,2,3\"", list, StringComparison.Ordinal);
        Assert.Contains("class=\"no-export\"", list, StringComparison.Ordinal);
        Assert.Contains("İşlem</th>", list, StringComparison.Ordinal);
        Assert.Contains("class=\"text-end no-export\"", list, StringComparison.Ordinal);
        Assert.Contains("Değerlendir", list, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerExportServices_DoNotEmitActionColumn()
    {
        var excel = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "ExcelExportService.cs");
        var pdf = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Services", "PdfExportService.cs");

        Assert.Contains("\"Aday No\", \"Ad Soyad\", \"Katılım\", \"Puan\"", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("İşlem", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("Değerlendir", excel, StringComparison.Ordinal);
        Assert.DoesNotContain("İşlem", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("Değerlendir", pdf, StringComparison.Ordinal);
        Assert.Contains("HeaderCell(header, \"Katılım\")", pdf, StringComparison.Ordinal);
        Assert.Contains("ToDisplayText()", excel, StringComparison.Ordinal);
        Assert.Contains("ToDisplayText()", pdf, StringComparison.Ordinal);
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

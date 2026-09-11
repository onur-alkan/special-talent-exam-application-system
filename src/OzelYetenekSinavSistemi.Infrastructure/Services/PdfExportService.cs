using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Enums;
using OzelYetenekSinavSistemi.Domain.Extensions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

public sealed class PdfExportService : IPdfExportService
{
    private readonly string? _webRootPath;

    static PdfExportService()
    {
        // QuestPDF Community lisansı (ücretsiz). Dış lisans anahtarı gerektirmez.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfExportService()
        : this(null)
    {
    }

    public PdfExportService(string? webRootPath)
    {
        _webRootPath = string.IsNullOrWhiteSpace(webRootPath) ? null : webRootPath;
    }

    public byte[] ExportCandidateResults(string examTitle, IReadOnlyList<CandidateApplicationDetail> rows)
    {
        var logoBytes = TryLoadOfficialLogoBytes();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Row(row =>
                {
                    if (logoBytes is { Length: > 0 })
                    {
                        row.ConstantItem(90).AlignMiddle().Image(logoBytes).FitWidth();
                        row.ConstantItem(12);
                    }

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Özel Yetenek Sınavları - Aday Sınav Sonuçları").FontSize(14).Bold();
                        col.Item().Text(examTitle).FontSize(11);
                        col.Item().PaddingBottom(5).Text($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(60);   // Aday No
                        columns.RelativeColumn(3);     // Ad Soyad
                        columns.ConstantColumn(80);   // Katılım
                        columns.ConstantColumn(55);   // Puan
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "Aday No");
                        HeaderCell(header, "Ad Soyad");
                        HeaderCell(header, "Katılım");
                        HeaderCell(header, "Puan");
                    });

                    foreach (var row in rows)
                    {
                        table.Cell().Border(0.5f).Padding(3).Text(row.CandidateNo.ToString());
                        table.Cell().Border(0.5f).Padding(3).Text(row.FullName);
                        table.Cell().Border(0.5f).Padding(3).Text(AttendanceText(row.AttendanceStatus));
                        table.Cell().Border(0.5f).Padding(3).Text(row.ExamScore?.ToString("0.##") ?? "-");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[]? TryLoadOfficialLogoBytes()
    {
        if (string.IsNullOrWhiteSpace(_webRootPath))
            return null;

        var path = Path.GetFullPath(Path.Combine(
            _webRootPath,
            "images",
            "branding",
            "oys-logo.png"));

        var rootFull = Path.GetFullPath(_webRootPath);
        if (!path.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return null;

        return File.ReadAllBytes(path);
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).Padding(3).Text(text).Bold();

    internal static string AttendanceText(AttendanceStatus? status) => status.ToDisplayText();
}

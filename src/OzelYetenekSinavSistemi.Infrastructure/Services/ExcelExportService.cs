using ClosedXML.Excel;
using OzelYetenekSinavSistemi.Application.DTOs.Applications;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Extensions;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

public sealed class ExcelExportService : IExcelExportService
{
    public byte[] ExportCandidateResults(string examTitle, IReadOnlyList<CandidateApplicationDetail> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Aday Sonuçları");

        WriteSafeText(ws.Cell(1, 1), examTitle);
        ws.Range(1, 1, 1, 4).Merge().Style.Font.SetBold().Font.SetFontSize(14);

        var headerRow = 3;
        string[] headers = { "Aday No", "Ad Soyad", "Katılım", "Puan" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1ab394"));
            cell.Style.Font.SetFontColor(XLColor.White);
        }

        var r = headerRow + 1;
        foreach (var row in rows)
        {
            ws.Cell(r, 1).Value = row.CandidateNo;
            WriteSafeText(ws.Cell(r, 2), row.FullName);
            WriteSafeText(ws.Cell(r, 3), row.AttendanceStatus.ToDisplayText());
            if (row.ExamScore.HasValue)
                ws.Cell(r, 4).Value = row.ExamScore.Value;
            else
                WriteSafeText(ws.Cell(r, 4), "-");
            r++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Kullanıcı/veritabanı kaynaklı metni Excel formül enjeksiyonuna karşı nötrleştirir ve Text olarak yazar.
    /// </summary>
    internal static string SanitizeSpreadsheetText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var first = value[0];
        if (first is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
            return "'" + value;

        return value;
    }

    internal static void WriteSafeText(IXLCell cell, string? value)
    {
        var text = SanitizeSpreadsheetText(value);
        cell.Style.NumberFormat.Format = "@";
        cell.Value = text;
        // ClosedXML bazı sürümlerde '=' ile başlayan değeri formül sayabilir; Value sonrası güvence.
        if (cell.HasFormula)
        {
            cell.Clear(XLClearOptions.Contents);
            cell.Style.NumberFormat.Format = "@";
            cell.SetValue(text);
        }
    }
}

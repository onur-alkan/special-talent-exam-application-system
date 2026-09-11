using OzelYetenekSinavSistemi.Application.DTOs.Applications;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IExcelExportService
{
    byte[] ExportCandidateResults(string examTitle, IReadOnlyList<CandidateApplicationDetail> rows);
}

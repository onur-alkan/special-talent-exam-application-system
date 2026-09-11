using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Documents;

public sealed class CandidateResultDocumentViewModel
{
    public string UniversityName { get; init; } = "Özel Yetenek Sınavları Başvuru Sistemi";
    public string FacultyName { get; init; } = "Public Portfolio Edition";

    public Guid ApplicationId { get; init; }
    public string ExamTitle { get; init; } = string.Empty;
    public string? PhotoPath { get; init; }
    public DocumentIdentityViewModel Identity { get; init; } = new();
    public string FullName { get; init; } = string.Empty;
    public int CandidateNo { get; init; }
    public IReadOnlyList<string> Preferences { get; init; } = Array.Empty<string>();
    public AttendanceStatus AttendanceStatus { get; init; }
    public decimal? ExamScore { get; init; }
    public string? CandidateVisibleDescription { get; init; }
    public DateTime? EvaluatedDate { get; init; }
    public string VerificationCode { get; init; } = string.Empty;
    public string VerificationUrl { get; init; } = string.Empty;
    public string QrCodeDataUri { get; init; } = string.Empty;
}

using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Domain.Entities;

public class CandidateExamResult
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public AttendanceStatus? AttendanceStatus { get; set; }
    public decimal? ExamScore { get; set; }
    public string? AdminDescription { get; set; }
    public bool IsDescriptionVisibleToCandidate { get; set; }
    public Guid? EvaluatedByUserId { get; set; }
    public DateTime? UpdatedDate { get; set; }
}

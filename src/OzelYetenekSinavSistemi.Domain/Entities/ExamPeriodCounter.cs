namespace OzelYetenekSinavSistemi.Domain.Entities;

public class ExamPeriodCounter
{
    public Guid ExamPeriodId { get; set; }
    public int LastCandidateNo { get; set; }
    public DateTime UpdatedDate { get; set; }
}

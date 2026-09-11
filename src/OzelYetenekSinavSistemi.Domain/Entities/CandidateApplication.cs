using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Domain.Entities;

public class CandidateApplication
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ExamPeriodId { get; set; }

    /// <summary>
    /// Sınav dönemi içinde 1, 2, 3 şeklinde ardışık üretilen sıra numarası.
    /// Primary Key değildir; bu yüzden int olarak korunur.
    /// </summary>
    public int CandidateNo { get; set; }

    public DateTime RegistrationDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
}

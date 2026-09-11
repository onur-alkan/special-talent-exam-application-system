using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.DTOs.Applications;

/// <summary>
/// Yönetim ve aday ekranlarında kullanılan zenginleştirilmiş başvuru bilgisi.
/// </summary>
public sealed class CandidateApplicationDetail
{
    public Guid ApplicationId { get; init; }
    public Guid UserId { get; init; }
    public Guid ExamPeriodId { get; init; }
    public int CandidateNo { get; init; }
    public string VerificationCode { get; init; } = string.Empty;
    public DateTime RegistrationDate { get; init; }
    public ApplicationStatus Status { get; init; }

    public string TcNo { get; init; } = string.Empty;
    public IdentityDocumentType? IdentityDocumentType { get; init; }
    public string? IdentityNumber { get; init; }
    public string? NationalityCountryCode { get; init; }
    public string? IssuingCountryCode { get; init; }
    public DateOnly? PassportExpiryDate { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    /// <summary>Legacy yıl; BirthDate yoksa gösterim için.</summary>
    public short? BirthYear { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? PhotoPath { get; init; }

    public string ExamTitle { get; init; } = string.Empty;

    public IReadOnlyList<string> Preferences { get; set; } = Array.Empty<string>();

    public AttendanceStatus? AttendanceStatus { get; init; }
    public decimal? ExamScore { get; init; }
    public string? AdminDescription { get; init; }
    public bool IsDescriptionVisibleToCandidate { get; init; }
    public DateTime? EvaluatedDate { get; init; }

    /// <summary>
    /// Sunucu tarafında hesaplanır: sınav dönemi aday tercihi güncellemesine açıksa true.
    /// </summary>
    public bool CanEdit { get; init; }

    /// <summary>Sınav döneminin başvuru bitiş zamanı (yerel DB saati).</summary>
    public DateTime ExamPeriodEndDate { get; init; }

    /// <summary>
    /// Sunucu tarafında hesaplanır: başvuru bitiş anı geldiyse/geçtiyse sınava giriş belgesi erişilebilir.
    /// </summary>
    public bool CanAccessExamEntranceDocument { get; init; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

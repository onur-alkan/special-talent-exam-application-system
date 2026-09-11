using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.ViewModels.ExamResults;

public sealed class ExamResultFormViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Başvuruyu seçiniz.")]
    public Guid ApplicationId { get; set; }

    [Required(ErrorMessage = "Sınava girme durumunu seçiniz.")]
    [Display(Name = "Sınava Girme Durumu")]
    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Attended;

    [Range(0, 100, ErrorMessage = "Sınav puanını 0-100 aralığında giriniz.")]
    [Display(Name = "Sınav Puanı")]
    public decimal? ExamScore { get; set; }

    [StringLength(2000, ErrorMessage = "Yönetici açıklaması en fazla 2000 karakter olabilir.")]
    [MeaningfulText(Optional = true, Multiline = true)]
    [Display(Name = "Yönetici Açıklaması")]
    public string? AdminDescription { get; set; }

    [Display(Name = "Açıklama adaya gösterilsin")]
    public bool IsDescriptionVisibleToCandidate { get; set; }

    /// <summary>
    /// Optimistic concurrency: mevcut sonucun UpdatedDate değeri (yoksa null).
    /// </summary>
    public DateTime? ExpectedUpdatedDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(AttendanceStatus))
        {
            yield return new ValidationResult(
                ExamResultValidationMessages.InvalidAttendanceStatus,
                new[] { nameof(AttendanceStatus) });
            yield break;
        }

        if (AttendanceStatus == AttendanceStatus.Attended && ExamScore is null)
        {
            yield return new ValidationResult(
                "Sınava giren aday için puanı giriniz.",
                new[] { nameof(ExamScore) });
        }

        if (AttendanceStatus != AttendanceStatus.Attended && ExamScore is not null)
        {
            yield return new ValidationResult(
                ExamResultValidationMessages.ScoreNotAllowed,
                new[] { nameof(ExamScore) });
        }
    }
}

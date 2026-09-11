using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;

public sealed class ExamPeriodFormViewModel : IValidatableObject
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Sınav / başvuru adını giriniz.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Sınav / başvuru adı en az 3, en fazla 200 karakter olabilir.")]
    [MeaningfulTitle]
    [Display(Name = "Sınav / Başvuru Adı")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir.")]
    [MeaningfulText(Optional = true, Multiline = true)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Başlangıç tarihini giriniz.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Başlangıç Tarihi")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Bitiş tarihini giriniz.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Bitiş Tarihi")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);

    [Range(1, 50, ErrorMessage = "Maksimum tercih sayısını 1 veya daha büyük giriniz.")]
    [Display(Name = "Maksimum Tercih Sayısı")]
    public int MaxPreferences { get; set; } = 1;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate <= StartDate)
        {
            yield return new ValidationResult(
                "Bitiş tarihi başlangıç tarihinden sonra olmalıdır.",
                new[] { nameof(EndDate) });
        }
    }
}

using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Application.ViewModels.ExamPeriods;

public sealed class PreferenceOptionFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Sınav dönemini seçiniz.")]
    public Guid ExamPeriodId { get; set; }

    [Required(ErrorMessage = "Tercih adını giriniz.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Tercih adı en fazla 100 karakter olabilir.")]
    [MeaningfulTitle]
    [Display(Name = "Tercih Adı")]
    public string PreferenceName { get; set; } = string.Empty;

    [Range(1, 999, ErrorMessage = "Görünüm sırasını 1 veya daha büyük giriniz.")]
    [Display(Name = "Görünüm Sırası")]
    public int DisplayOrder { get; set; } = 1;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

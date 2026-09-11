using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Applications;

public sealed class CreateApplicationViewModel
{
    [Required(ErrorMessage = "Sınav dönemini seçiniz.")]
    public Guid ExamPeriodId { get; set; }

    /// <summary>
    /// Tercih sırasına göre seçilen tercih seçeneği Id'leri (1. tercih ilk sırada).
    /// </summary>
    [Required(ErrorMessage = "En az bir tercih seçiniz.")]
    [MinLength(1, ErrorMessage = "En az bir tercih seçiniz.")]
    [Display(Name = "Tercihler")]
    public List<Guid> SelectedPreferenceOptionIds { get; set; } = new();
}

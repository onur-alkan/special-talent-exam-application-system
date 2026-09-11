using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Profile;

/// <summary>
/// Aday profil güncelleme formu; kimlik alanları içermez.
/// </summary>
public sealed class ProfileUpdateViewModel : IValidatableObject
{
    private string? _phone;

    public Guid Id { get; set; }

    [Required(ErrorMessage = "Adınızı giriniz.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Ad en az 2, en fazla 50 karakter olabilir.")]
    [HumanName]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyadınızı giriniz.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Soyad en az 2, en fazla 50 karakter olabilir.")]
    [HumanName]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "Mezun olunan lise en fazla 150 karakter olabilir.")]
    [Display(Name = "Mezun Olunan Lise")]
    [MeaningfulText(Optional = true)]
    public string? HighSchool { get; set; }

    [StringLength(100, ErrorMessage = "Bölüm / alan en fazla 100 karakter olabilir.")]
    [Display(Name = "Bölüm / Alan")]
    [MeaningfulText(Optional = true)]
    public string? DepartmentField { get; set; }

    [Range(0, 560, ErrorMessage = "YGS puanını 0-560 aralığında giriniz.")]
    [Display(Name = "YGS Puanı")]
    public decimal? YgsScore { get; set; }

    [Display(Name = "YGS Yılı")]
    public Guid? YgsYearId { get; set; }

    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    [Display(Name = "Adres")]
    [MeaningfulText(Optional = true, Multiline = true)]
    public string? Address { get; set; }

    // Kayıt formu E.164 saklar; profil düzenleme eski TR biçimini de kabul eder (toplu dönüşüm yok).
    [RegularExpression(@"^(?:\+[1-9]\d{7,14}|0?5[0-9]{9})$", ErrorMessage = "Geçerli bir cep telefonu numarası giriniz.")]
    [StringLength(20, ErrorMessage = "Geçerli bir cep telefonu numarası giriniz.")]
    [Display(Name = "Telefon")]
    public string? Phone
    {
        get => _phone;
        set => _phone = value?.Trim();
    }

    [Display(Name = "Engel Durumu")]
    public bool HasDisability { get; set; }

    [StringLength(500, ErrorMessage = "Engel açıklaması en fazla 500 karakter olabilir.")]
    [Display(Name = "Engel Açıklaması")]
    [MeaningfulText(Optional = true, Multiline = true)]
    public string? DisabilityDetails { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DisabilityDetailsNormalizer.IsMissingWhenRequired(HasDisability, DisabilityDetails))
        {
            yield return new ValidationResult(
                DisabilityDetailsNormalizer.RequiredMessage,
                new[] { nameof(DisabilityDetails) });
        }
    }
}

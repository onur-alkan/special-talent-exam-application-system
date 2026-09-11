using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Account;

public sealed class RegisterViewModel : IValidatableObject
{
    private string _phone = string.Empty;
    private string _phoneCountryCode = MobilePhoneNumber.DefaultRegionCode;

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

    /// <summary>Geçiş dönemi uyumluluğu; yeni kayıt formu bu alanı göndermez.</summary>
    [Display(Name = "T.C. Kimlik Numarası")]
    public string? TcNo { get; set; }

    [Required(ErrorMessage = "Kimlik belgesi türünü seçiniz.")]
    [Display(Name = "Kimlik Belgesi Türü")]
    public IdentityDocumentType? IdentityDocumentType { get; set; }

    [Display(Name = "Kimlik Numarası")]
    [StringLength(32, ErrorMessage = "Kimlik numarası en fazla 32 karakter olabilir.")]
    public string? IdentityNumber { get; set; }

    [Display(Name = "Uyruk")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Uyruk için geçerli bir ülke seçiniz.")]
    public string? NationalityCountryCode { get; set; }

    [Display(Name = "Pasaportu Düzenleyen Ülke")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Pasaportu düzenleyen ülke için geçerli bir ülke seçiniz.")]
    public string? IssuingCountryCode { get; set; }

    [Display(Name = "Pasaport Son Geçerlilik Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? PassportExpiryDate { get; set; }

    [Display(Name = "Gün")]
    public int? BirthDateDay { get; set; }

    [Display(Name = "Ay")]
    public int? BirthDateMonth { get; set; }

    [Display(Name = "Yıl")]
    public int? BirthDateYear { get; set; }

    /// <summary>
    /// Sunucuda gün/ay/yıl seçimlerinden üretilir. Forma gizli alan olarak güvenilmez.
    /// </summary>
    [BirthDate]
    [Display(Name = "Doğum Tarihi")]
    public DateOnly? BirthDate { get; set; }

    public IReadOnlyList<int> BirthDateYearOptions { get; set; } = Array.Empty<int>();

    [Display(Name = "Uyruk")]
    [StringLength(50, ErrorMessage = "Uyruk en fazla 50 karakter olabilir.")]
    public string? Nationality { get; set; }

    [Display(Name = "Mezun Olunan Lise")]
    [StringLength(150, ErrorMessage = "Mezun olunan lise en fazla 150 karakter olabilir.")]
    [MeaningfulText(Optional = true)]
    public string? HighSchool { get; set; }

    [Display(Name = "Bölüm / Alan")]
    [StringLength(100, ErrorMessage = "Bölüm / alan en fazla 100 karakter olabilir.")]
    [MeaningfulText(Optional = true)]
    public string? DepartmentField { get; set; }

    [Required(ErrorMessage = "YGS puanınızı giriniz.")]
    [Range(0, 560, ErrorMessage = "YGS puanını 0-560 aralığında giriniz.")]
    [Display(Name = "YGS Puanı")]
    public decimal? YgsScore { get; set; }

    [Required(ErrorMessage = "YGS yılını seçiniz.")]
    [Display(Name = "YGS Yılı")]
    public Guid? YgsYearId { get; set; }

    [Display(Name = "Adres")]
    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    [MeaningfulText(Optional = true, Multiline = true)]
    public string? Address { get; set; }

    [Required(ErrorMessage = MobilePhoneNumber.CountryRequiredMessage)]
    [Display(Name = "Ülke Kodu")]
    // Not: StringLength(MinimumLength=2) select'te istemci tarafında seçili option SAYISINI
    // ölçer (jQuery Validate getLength); TR seçiliyken bile required benzeri hata üretir.
    [RegularExpression("^[A-Z]{2}$", ErrorMessage = MobilePhoneNumber.CountryRequiredMessage)]
    public string PhoneCountryCode
    {
        get => _phoneCountryCode;
        set => _phoneCountryCode = MobilePhoneNumber.NormalizeRegionCode(value)
                                  ?? (value?.Trim().ToUpperInvariant() ?? string.Empty);
    }

    [Required(ErrorMessage = MobilePhoneNumber.RequiredMessage)]
    [StringLength(MobilePhoneNumber.MaxInputLength, ErrorMessage = MobilePhoneNumber.InvalidMessage)]
    [MobilePhoneNumber]
    [Display(Name = "Cep Telefonu")]
    public string Phone
    {
        get => _phone;
        set => _phone = value?.Trim() ?? string.Empty;
    }

    [Display(Name = "Engel Durumu")]
    public bool HasDisability { get; set; }

    [Display(Name = "Engel Açıklaması")]
    [StringLength(500, ErrorMessage = "Engel açıklaması en fazla 500 karakter olabilir.")]
    [MeaningfulText(Optional = true, Multiline = true)]
    public string? DisabilityDetails { get; set; }

    [Required(ErrorMessage = "E-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Vesikalık Fotoğraf")]
    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "Parolanızı giriniz.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Parola en az 8 karakter olmalıdır.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Parola en az bir büyük harf, bir küçük harf ve bir rakam içermelidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarını giriniz.")]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola (Tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IReadOnlyList<CountryOption> ForeignNationalityCountries { get; set; } = Array.Empty<CountryOption>();

    public IReadOnlyList<CountryOption> IssuingCountries { get; set; } = Array.Empty<CountryOption>();

    public IReadOnlyList<PhoneCountryOption> PhoneCountries { get; set; } = Array.Empty<PhoneCountryOption>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DisabilityDetailsNormalizer.IsMissingWhenRequired(HasDisability, DisabilityDetails))
        {
            yield return new ValidationResult(
                DisabilityDetailsNormalizer.RequiredMessage,
                new[] { nameof(DisabilityDetails) });
        }

        // Kimlik numarası alanı T.C. / YKN / Pasaport için ortak; T.C. checksum yalnızca tür=T.C. iken uygulanır.
        if (IdentityDocumentType == Domain.Enums.IdentityDocumentType.TurkishIdentityNumber
            && !string.IsNullOrWhiteSpace(IdentityNumber)
            && !TurkishIdentityNumber.IsValid(IdentityNumber))
        {
            yield return new ValidationResult(
                TurkishIdentityNumber.InvalidMessage,
                new[] { nameof(IdentityNumber) });
        }

        var timeProvider = (TimeProvider?)validationContext.GetService(typeof(TimeProvider))
                           ?? TimeProvider.System;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var hasParts = BirthDateDay.HasValue || BirthDateMonth.HasValue || BirthDateYear.HasValue;

        if (hasParts)
        {
            if (!BirthDateRules.TryCompose(
                    BirthDateDay,
                    BirthDateMonth,
                    BirthDateYear,
                    today,
                    out var composed,
                    out var error))
            {
                yield return new ValidationResult(
                    error ?? BirthDateRules.InvalidMessage,
                    new[] { nameof(BirthDate) });
            }
            else
            {
                BirthDate = composed;
            }
        }
        else if (BirthDate is null)
        {
            yield return new ValidationResult(
                BirthDateRules.RequiredMessage,
                new[] { nameof(BirthDate) });
        }
    }
}

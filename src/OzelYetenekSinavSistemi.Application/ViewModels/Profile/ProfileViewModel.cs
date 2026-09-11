using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Profile;

public sealed class ProfileViewModel
{
    private string? _phone;

    public Guid Id { get; set; }

    [Display(Name = "Doğum Tarihi")]
    public DateOnly? BirthDate { get; set; }

    /// <summary>Legacy alan; BirthDate yoksa profilde yıl olarak gösterilir.</summary>
    [Display(Name = "Doğum Yılı")]
    public short? BirthYear { get; set; }

    public string BirthDateLabel => BirthDateRules.ResolveProfileLabel(BirthDate, BirthYear);

    public string BirthDateDisplay => BirthDateRules.ResolveProfileDisplay(BirthDate, BirthYear);

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

    public string? PhotoPath { get; set; }

    /// <summary>
    /// Sol profil kartı için güvenli önbellek yenileme değeri (yalnızca depolanan GUID dosya adı).
    /// </summary>
    public string? PhotoCacheVersion
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PhotoPath))
                return null;

            var fileName = Path.GetFileNameWithoutExtension(PhotoPath.Trim());
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
    }

    public ProfileIdentityDisplayViewModel Identity { get; set; } = new();

    public void ApplyEditableFields(ProfileUpdateViewModel update)
    {
        FirstName = update.FirstName;
        LastName = update.LastName;
        Email = update.Email;
        HighSchool = update.HighSchool;
        DepartmentField = update.DepartmentField;
        YgsScore = update.YgsScore;
        YgsYearId = update.YgsYearId;
        Address = update.Address;
        Phone = update.Phone;
        HasDisability = update.HasDisability;
        DisabilityDetails = update.DisabilityDetails;
    }
}

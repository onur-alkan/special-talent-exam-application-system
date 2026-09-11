using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Application.ViewModels.UserManagement;

public sealed class CreateStaffUserViewModel
{
    [Required(ErrorMessage = "T.C. kimlik numaranızı giriniz.")]
    [TurkishIdentityNumber]
    [Display(Name = "T.C. Kimlik Numarası")]
    public string TcNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta adresinizi giriniz.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [StringLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

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

    [Required(ErrorMessage = "Rolü seçiniz.")]
    [Display(Name = "Rol")]
    public Guid RoleId { get; set; }

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

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}

public sealed class EditStaffUserViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "T.C. (maskeli)")]
    public string MaskedTcNo { get; set; } = string.Empty;

    [Display(Name = "E-posta (maskeli)")]
    public string MaskedEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rolü seçiniz.")]
    [Display(Name = "Rol")]
    public Guid RoleId { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; }

    public bool IsSelf { get; set; }
}

public sealed class StaffUserListItemViewModel
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string MaskedTcNo { get; init; } = string.Empty;
    public string MaskedEmail { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public bool IsSelf { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Account;

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut parolanızı giriniz.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut Parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parolanızı giriniz.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Parola en az 8 karakter olmalıdır.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        ErrorMessage = "Parola en az bir büyük harf, bir küçük harf ve bir rakam içermelidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarını giriniz.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Parolalar eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Parola (Tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

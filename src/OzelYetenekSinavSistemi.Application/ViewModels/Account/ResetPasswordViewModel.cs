using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Account;

public sealed class ResetPasswordViewModel
{
    [Required(ErrorMessage = "Geçersiz parola sıfırlama bağlantısı.")]
    public string Token { get; set; } = string.Empty;

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

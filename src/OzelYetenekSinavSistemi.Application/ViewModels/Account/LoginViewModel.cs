using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "E-posta veya T.C. kimlik numaranızı giriniz.")]
    [StringLength(LoginIdentifierHelper.MaxLength)]
    [Display(Name = "E-posta veya T.C. Kimlik Numarası")]
    public string LoginIdentifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parolanızı giriniz.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Güvenlik kodunu giriniz.")]
    [Display(Name = "Güvenlik Kodu")]
    public string CaptchaInput { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.ViewModels.Account;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta veya T.C. kimlik numaranızı giriniz.")]
    [StringLength(LoginIdentifierHelper.MaxLength)]
    [Display(Name = "E-posta veya T.C. Kimlik Numarası")]
    public string TcNoOrEmail { get; set; } = string.Empty;
}

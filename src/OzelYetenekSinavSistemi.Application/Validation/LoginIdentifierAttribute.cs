using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Giriş ve parola sıfırlama alanlarında e-posta veya T.C. Kimlik Numarası doğrular.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LoginIdentifierAttribute : ValidationAttribute
{
    public LoginIdentifierAttribute()
        : base(() => LoginIdentifierHelper.InvalidMessage)
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string text)
            return false;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        return LoginIdentifierHelper.IsValidLoginIdentifier(text);
    }
}

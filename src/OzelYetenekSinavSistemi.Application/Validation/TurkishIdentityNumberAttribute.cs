using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// ViewModel özelliklerinde T.C. Kimlik Numarası algoritmasını doğrular.
/// Boş değerler için <see cref="RequiredAttribute"/> kullanılır.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TurkishIdentityNumberAttribute : ValidationAttribute
{
    public TurkishIdentityNumberAttribute()
        : base(() => TurkishIdentityNumber.InvalidMessage)
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

        return TurkishIdentityNumber.IsValid(text);
    }
}

using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Application.Services;

/// <summary>
/// T.C. Kimlik Numarası algoritmik doğrulaması.
/// </summary>
public sealed class TurkishIdentityNumberValidator : ITurkishIdentityNumberValidator
{
    public bool IsValid(string? value) => TurkishIdentityNumber.IsValid(value);
}

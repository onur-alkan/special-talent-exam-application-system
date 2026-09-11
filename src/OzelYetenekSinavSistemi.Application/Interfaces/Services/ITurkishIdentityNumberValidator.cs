namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// T.C. Kimlik Numarası algoritmik doğrulayıcısı.
/// </summary>
public interface ITurkishIdentityNumberValidator
{
    bool IsValid(string? value);
}

using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Giriş ve parola sıfırlama tanımlayıcılarını sınıflandırır ve doğrular.
/// </summary>
public static class LoginIdentifierHelper
{
    public const int MaxLength = 254;

    public const string InvalidMessage = "Geçerli bir e-posta adresi veya T.C. Kimlik Numarası giriniz.";

    public const string LoginFailureMessage = "E-posta/T.C. Kimlik Numarası veya parola hatalı.";

    public const string ForgotPasswordSuccessMessage =
        "Bilgileriniz sistemde kayıtlıysa parola sıfırlama bağlantısı e-posta adresinize gönderilmiştir.";

    private static readonly EmailAddressAttribute EmailValidator = new();

    public static bool LooksLikeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex >= trimmed.Length - 1)
            return false;

        return trimmed.IndexOf('.', atIndex + 1) > atIndex;
    }

    public static bool IsValidLoginIdentifier(string? value)
        => TryNormalizeLoginIdentifier(value, out _);

    public static bool TryNormalizeLoginIdentifier(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            return false;

        if (LooksLikeEmail(trimmed))
        {
            if (!EmailValidator.IsValid(trimmed))
                return false;

            normalized = trimmed;
            return true;
        }

        if (IsForeignIdentityPattern(trimmed) || ContainsLetters(trimmed))
            return false;

        var normalizedTc = IdentityNumberNormalizer.NormalizeTurkishIdentityNumber(trimmed);
        if (normalizedTc is null || !TurkishIdentityNumber.IsValid(normalizedTc))
            return false;

        normalized = normalizedTc;
        return true;
    }

    private static bool IsForeignIdentityPattern(string value)
        => value.Length == TurkishIdentityNumber.Length
           && value.All(char.IsDigit)
           && value[0] == '9';

    private static bool ContainsLetters(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
                return true;
        }

        return false;
    }
}

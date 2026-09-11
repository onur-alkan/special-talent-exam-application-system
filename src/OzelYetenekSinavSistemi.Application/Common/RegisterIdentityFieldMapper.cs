using OzelYetenekSinavSistemi.Application.ViewModels.Account;
using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Application.Common;

public static class RegisterIdentityFieldMapper
{
    public static string MapValidationErrorToField(string? errorMessage, IdentityDocumentType documentType)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return string.Empty;

        if (errorMessage.Contains("pasaport veren ülke", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("Pasaportu veren ülke", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("düzenleyen ülke", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(RegisterViewModel.IssuingCountryCode);
        }

        if (errorMessage.Contains("Pasaport geçerlilik", StringComparison.OrdinalIgnoreCase))
            return nameof(RegisterViewModel.PassportExpiryDate);

        if (errorMessage.Contains("uyruk", StringComparison.OrdinalIgnoreCase))
            return nameof(RegisterViewModel.NationalityCountryCode);

        if (errorMessage.Contains("kimlik belgesi türü", StringComparison.OrdinalIgnoreCase))
            return nameof(RegisterViewModel.IdentityDocumentType);

        return nameof(RegisterViewModel.IdentityNumber);
    }

    public static string MapRegistrationErrorToField(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return string.Empty;

        if (errorMessage.Contains("e-posta", StringComparison.OrdinalIgnoreCase))
            return nameof(RegisterViewModel.Email);

        if (errorMessage.Contains("pasaport numarası", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("kimlik numarası", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("T.C. Kimlik Numarası", StringComparison.OrdinalIgnoreCase))
        {
            return nameof(RegisterViewModel.IdentityNumber);
        }

        return string.Empty;
    }
}

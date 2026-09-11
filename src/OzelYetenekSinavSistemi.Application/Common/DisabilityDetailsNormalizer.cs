using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Engel durumu kapalıyken açıklama değerinin saklanmamasını sağlar.
/// </summary>
public static class DisabilityDetailsNormalizer
{
    public const string RequiredMessage = "Engel durumu işaretlendiğinde açıklama giriniz.";

    public static string? ForStorage(bool hasDisability, string? disabilityDetails)
    {
        if (!hasDisability)
            return null;

        return InputTextRules.IsValidRequiredMeaningfulText(disabilityDetails, multiline: true, out var normalized)
            ? normalized
            : disabilityDetails?.Trim();
    }

    public static bool IsMissingWhenRequired(bool hasDisability, string? disabilityDetails)
    {
        if (!hasDisability)
            return false;

        if (string.IsNullOrWhiteSpace(disabilityDetails))
            return true;

        return !InputTextRules.IsValidRequiredMeaningfulText(disabilityDetails, multiline: true, out _);
    }
}

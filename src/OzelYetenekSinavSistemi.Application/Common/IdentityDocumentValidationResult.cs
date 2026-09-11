namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Kimlik belgesi doğrulama sonucu.
/// </summary>
public sealed class IdentityDocumentValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public string? NormalizedIdentityNumber { get; init; }
    public string? NormalizedNationalityCountryCode { get; init; }
    public string? NormalizedIssuingCountryCode { get; init; }

    public static IdentityDocumentValidationResult Ok(
        string normalizedIdentityNumber,
        string? normalizedNationalityCountryCode,
        string? normalizedIssuingCountryCode) =>
        new()
        {
            IsValid = true,
            NormalizedIdentityNumber = normalizedIdentityNumber,
            NormalizedNationalityCountryCode = normalizedNationalityCountryCode,
            NormalizedIssuingCountryCode = normalizedIssuingCountryCode
        };

    public static IdentityDocumentValidationResult Fail(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}

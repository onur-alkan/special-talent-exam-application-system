namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Kimlik belgesi doğrulama isteği.
/// </summary>
public sealed class IdentityDocumentValidationRequest
{
    public Domain.Enums.IdentityDocumentType DocumentType { get; init; }
    public string? IdentityNumber { get; init; }
    public string? NationalityCountryCode { get; init; }
    public string? IssuingCountryCode { get; init; }
    public DateOnly? PassportExpiryDate { get; init; }
}

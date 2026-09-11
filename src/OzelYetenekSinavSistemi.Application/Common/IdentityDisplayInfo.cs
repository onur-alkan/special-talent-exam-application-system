namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// Kimlik belgesi türüne göre görüntüleme bilgileri (maskeli veya tam).
/// </summary>
public sealed class IdentityDisplayInfo
{
    public string IdentityDocumentTypeDisplayName { get; init; } = "-";

    public string IdentityNumberLabel { get; init; } = "-";

    public string DisplayIdentityNumber { get; init; } = "-";

    public string NationalityDisplayName { get; init; } = "-";

    public string? IssuingCountryDisplayName { get; init; }

    public string? PassportExpiryDateDisplay { get; init; }
}

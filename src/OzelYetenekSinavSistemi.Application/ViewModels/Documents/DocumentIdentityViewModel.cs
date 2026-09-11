namespace OzelYetenekSinavSistemi.Application.ViewModels.Documents;

/// <summary>
/// Belgelerde maskelenmiş kimlik gösterimi.
/// </summary>
public sealed class DocumentIdentityViewModel
{
    public string IdentityDocumentTypeDisplayName { get; init; } = "-";

    public string IdentityNumberLabel { get; init; } = "-";

    public string MaskedIdentityNumber { get; init; } = "-";

    public string NationalityDisplayName { get; init; } = "-";

    public string? IssuingCountryDisplayName { get; init; }

    public string? PassportExpiryDateDisplay { get; init; }

    /// <summary>Geçiş dönemi uyumluluğu.</summary>
    public string MaskedTcNo => MaskedIdentityNumber;
}

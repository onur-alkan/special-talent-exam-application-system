namespace OzelYetenekSinavSistemi.Application.ViewModels.Profile;

/// <summary>
/// Aday profilinde salt okunur kimlik bilgileri.
/// </summary>
public sealed class ProfileIdentityDisplayViewModel
{
    public string DocumentTypeDisplay { get; init; } = string.Empty;

    public string IdentityNumberLabel { get; init; } = string.Empty;

    public string IdentityNumber { get; init; } = string.Empty;

    public string NationalityLabel { get; init; } = "Uyruk";

    public string NationalityDisplay { get; init; } = string.Empty;

    public string? IssuingCountryLabel { get; init; }

    public string? IssuingCountryDisplay { get; init; }

    public string? PassportExpiryLabel { get; init; }

    public string? PassportExpiryDisplay { get; init; }
}

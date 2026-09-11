using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Profile;
using OzelYetenekSinavSistemi.Domain.Entities;

namespace OzelYetenekSinavSistemi.Application.Common;

public static class ProfileIdentityDisplayBuilder
{
    public static ProfileIdentityDisplayViewModel Build(User user, ICountryCatalog countryCatalog)
    {
        var info = IdentityDisplayBuilder.BuildFromUser(user, countryCatalog, maskIdentity: false);
        return MapToProfile(info);
    }

    internal static ProfileIdentityDisplayViewModel MapToProfile(IdentityDisplayInfo info) =>
        new()
        {
            DocumentTypeDisplay = info.IdentityDocumentTypeDisplayName,
            IdentityNumberLabel = info.IdentityNumberLabel,
            IdentityNumber = info.DisplayIdentityNumber,
            NationalityDisplay = info.NationalityDisplayName,
            IssuingCountryLabel = info.IssuingCountryDisplayName is null ? null : "Pasaportu Düzenleyen Ülke",
            IssuingCountryDisplay = info.IssuingCountryDisplayName,
            PassportExpiryLabel = info.PassportExpiryDateDisplay is null ? null : "Pasaport Son Geçerlilik Tarihi",
            PassportExpiryDisplay = info.PassportExpiryDateDisplay
        };
}

using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.ViewModels.Documents;

namespace OzelYetenekSinavSistemi.Application.Services;

internal static class DocumentIdentityMapper
{
    public static DocumentIdentityViewModel Map(
        IdentityDisplayInfo info) =>
        new()
        {
            IdentityDocumentTypeDisplayName = info.IdentityDocumentTypeDisplayName,
            IdentityNumberLabel = info.IdentityNumberLabel,
            MaskedIdentityNumber = info.DisplayIdentityNumber,
            NationalityDisplayName = info.NationalityDisplayName,
            IssuingCountryDisplayName = info.IssuingCountryDisplayName,
            PassportExpiryDateDisplay = info.PassportExpiryDateDisplay
        };
}

using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface IIdentityDocumentValidator
{
    IdentityDocumentValidationResult Validate(IdentityDocumentValidationRequest request);
}

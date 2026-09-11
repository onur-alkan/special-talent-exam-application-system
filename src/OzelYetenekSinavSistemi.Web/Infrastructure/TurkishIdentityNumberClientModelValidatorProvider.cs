using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// <see cref="TurkishIdentityNumberAttribute"/> için unobtrusive client validation üretir.
/// </summary>
public sealed class TurkishIdentityNumberClientModelValidatorProvider : IClientModelValidatorProvider
{
    public void CreateValidators(ClientValidatorProviderContext context)
    {
        var attribute = context.ValidatorMetadata.OfType<TurkishIdentityNumberAttribute>().FirstOrDefault();
        if (attribute is null)
            return;

        if (context.Results.Any(static result => result.Validator is TurkishIdentityNumberClientValidator))
            return;

        var message = string.IsNullOrWhiteSpace(attribute.ErrorMessage)
            ? TurkishIdentityNumber.InvalidMessage
            : attribute.ErrorMessage;

        context.Results.Add(new ClientValidatorItem
        {
            Validator = new TurkishIdentityNumberClientValidator(message),
            IsReusable = true
        });
    }
}

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// <see cref="MobilePhoneNumberAttribute"/> için unobtrusive client validation üretir.
/// </summary>
public sealed class MobilePhoneNumberClientModelValidatorProvider : IClientModelValidatorProvider
{
    public void CreateValidators(ClientValidatorProviderContext context)
    {
        if (context.ValidatorMetadata.OfType<MobilePhoneNumberAttribute>().FirstOrDefault() is null)
            return;

        if (context.Results.Any(static result => result.Validator is MobilePhoneNumberClientValidator))
            return;

        context.Results.Add(new ClientValidatorItem
        {
            Validator = new MobilePhoneNumberClientValidator(),
            IsReusable = true
        });
    }
}

internal sealed class MobilePhoneNumberClientValidator : IClientModelValidator
{
    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-mobilephone", MobilePhoneNumber.InvalidMessage);
        MergeAttribute(
            context.Attributes,
            "data-val-mobilephone-country",
            MobilePhoneNumberAttribute.CountryCodePropertyName);
        MergeAttribute(context.Attributes, "data-val-mobilephone-mismatch", MobilePhoneNumber.CountryMismatchMessage);
        MergeAttribute(context.Attributes, "data-val-mobilephone-required", MobilePhoneNumber.RequiredMessage);
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

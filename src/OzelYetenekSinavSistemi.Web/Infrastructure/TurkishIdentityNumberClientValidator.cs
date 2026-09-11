using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

internal sealed class TurkishIdentityNumberClientValidator : IClientModelValidator
{
    private readonly string _errorMessage;

    public TurkishIdentityNumberClientValidator(string errorMessage)
    {
        _errorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? TurkishIdentityNumber.InvalidMessage
            : errorMessage;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-turkishidentity", _errorMessage);
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

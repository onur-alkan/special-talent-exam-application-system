using Microsoft.AspNetCore.Mvc;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

internal static class TurkishModelBindingMessageConfiguration
{
    public static void Apply(MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var provider = options.ModelBindingMessageProvider;

        provider.SetValueMustBeANumberAccessor(TurkishModelBindingMessages.ValueMustBeANumber);
        provider.SetNonPropertyValueMustBeANumberAccessor(TurkishModelBindingMessages.NonPropertyValueMustBeANumber);
        provider.SetAttemptedValueIsInvalidAccessor(TurkishModelBindingMessages.AttemptedValueIsInvalid);
        provider.SetUnknownValueIsInvalidAccessor(TurkishModelBindingMessages.UnknownValueIsInvalid);
        provider.SetValueIsInvalidAccessor(TurkishModelBindingMessages.ValueIsInvalid);
        provider.SetValueMustNotBeNullAccessor(TurkishModelBindingMessages.ValueMustNotBeNull);
        provider.SetMissingBindRequiredValueAccessor(TurkishModelBindingMessages.MissingBindRequiredValue);
        provider.SetMissingKeyOrValueAccessor(TurkishModelBindingMessages.MissingKeyOrValue);
        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(TurkishModelBindingMessages.NonPropertyAttemptedValueIsInvalid);
        provider.SetNonPropertyUnknownValueIsInvalidAccessor(TurkishModelBindingMessages.NonPropertyUnknownValueIsInvalid);
        provider.SetMissingRequestBodyRequiredValueAccessor(TurkishModelBindingMessages.MissingRequestBodyRequiredValue);
    }
}

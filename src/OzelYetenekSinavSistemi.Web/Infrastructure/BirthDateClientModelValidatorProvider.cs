using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// <see cref="BirthDateAttribute"/> için unobtrusive client validation ve HTML max üretir.
/// </summary>
public sealed class BirthDateClientModelValidatorProvider : IClientModelValidatorProvider
{
    private readonly TimeProvider _timeProvider;

    public BirthDateClientModelValidatorProvider(TimeProvider timeProvider)
        => _timeProvider = timeProvider;

    public void CreateValidators(ClientValidatorProviderContext context)
    {
        if (context.ValidatorMetadata.OfType<BirthDateAttribute>().FirstOrDefault() is null)
            return;

        if (context.Results.Any(static result => result.Validator is BirthDateClientValidator))
            return;

        context.Results.Add(new ClientValidatorItem
        {
            Validator = new BirthDateClientValidator(_timeProvider),
            IsReusable = true
        });
    }
}

internal sealed class BirthDateClientValidator : IClientModelValidator
{
    private readonly TimeProvider _timeProvider;

    public BirthDateClientValidator(TimeProvider timeProvider)
        => _timeProvider = timeProvider;

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-birthdate", BirthDateRules.FutureMessage);
        MergeAttribute(context.Attributes, "max", today);
        MergeAttribute(context.Attributes, "data-msg-max", BirthDateRules.FutureMessage);
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

internal sealed class BirthYearClientValidator : IClientModelValidator
{
    private readonly TimeProvider _timeProvider;

    public BirthYearClientValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentYear = _timeProvider.GetUtcNow().Year;
        var min = BirthYearRules.MinimumYear(currentYear).ToString(CultureInfo.InvariantCulture);
        var max = currentYear.ToString(CultureInfo.InvariantCulture);

        // HTML min/max (and type=number) cause jQuery Validate to attach separate min/max/number
        // rules with English defaults; data-msg-* overrides those for this field only.
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-range", BirthYearRules.InvalidMessage);
        MergeAttribute(context.Attributes, "data-val-range-min", min);
        MergeAttribute(context.Attributes, "data-val-range-max", max);
        MergeAttribute(context.Attributes, "min", min);
        MergeAttribute(context.Attributes, "max", max);
        MergeAttribute(context.Attributes, "data-msg-min", BirthYearRules.InvalidMessage);
        MergeAttribute(context.Attributes, "data-msg-max", BirthYearRules.InvalidMessage);
        MergeAttribute(context.Attributes, "data-msg-number", TurkishModelBindingMessages.ValueMustBeANumber("Doğum Yılı"));
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

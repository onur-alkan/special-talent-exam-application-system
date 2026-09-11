using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// int/long dahil tüm sayısal alanlara Türkçe data-val-number üretir (kural değiştirmez).
/// </summary>
internal sealed class TurkishNumericClientModelValidatorProvider : IClientModelValidatorProvider
{
    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal)
    ];

    public void CreateValidators(ClientValidatorProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.ModelMetadata.ModelType;
        var underlying = Nullable.GetUnderlyingType(modelType) ?? modelType;
        if (!NumericTypes.Contains(underlying))
            return;

        if (context.Results.Any(static r => r.Validator is TurkishNumericClientModelValidator))
            return;

        context.Results.Add(new ClientValidatorItem
        {
            Validator = new TurkishNumericClientModelValidator(),
            IsReusable = true
        });
    }
}

internal sealed class TurkishNumericClientModelValidator : IClientModelValidator
{
    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var displayName = context.ModelMetadata.GetDisplayName();
        var message = TurkishModelBindingMessages.ValueMustBeANumber(displayName);

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-number", message);
        // HTML5 type=number için jQuery'nin ayrı number kuralı
        MergeAttribute(context.Attributes, "data-msg-number", message);
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
        else if (string.Equals(key, "data-val-number", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(key, "data-msg-number", StringComparison.OrdinalIgnoreCase))
            attributes[key] = value;
    }
}

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

internal sealed class HumanNameClientValidator : IClientModelValidator
{
    private readonly string _message;

    public HumanNameClientValidator(string message) => _message = message;

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-humanname", _message);
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

internal sealed class MeaningfulTitleClientValidator : IClientModelValidator
{
    private readonly string _message;

    public MeaningfulTitleClientValidator(string message) => _message = message;

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-meaningfultitle", _message);
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

internal sealed class MeaningfulTextClientValidator : IClientModelValidator
{
    private readonly string _message;
    private readonly bool _optional;
    private readonly bool _multiline;

    public MeaningfulTextClientValidator(string message, bool optional, bool multiline)
    {
        _message = message;
        _optional = optional;
        _multiline = multiline;
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-meaningfultext", _message);
        MergeAttribute(context.Attributes, "data-val-meaningfultext-optional", _optional ? "true" : "false");
        MergeAttribute(context.Attributes, "data-val-meaningfultext-multiline", _multiline ? "true" : "false");
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (!attributes.ContainsKey(key))
            attributes.Add(key, value);
    }
}

/// <summary>
/// HumanName, MeaningfulTitle ve MeaningfulText attribute'ları için unobtrusive client validation üretir.
/// </summary>
public sealed class InputTextClientModelValidatorProvider : IClientModelValidatorProvider
{
    public void CreateValidators(ClientValidatorProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ValidatorMetadata.OfType<HumanNameAttribute>().FirstOrDefault() is { } humanName)
        {
            if (!context.Results.Any(static r => r.Validator is HumanNameClientValidator))
            {
                context.Results.Add(new ClientValidatorItem
                {
                    Validator = new HumanNameClientValidator(ResolveHumanNameMessage(context, humanName)),
                    IsReusable = false
                });
            }
        }

        if (context.ValidatorMetadata.OfType<MeaningfulTitleAttribute>().FirstOrDefault() is { } title)
        {
            if (!context.Results.Any(static r => r.Validator is MeaningfulTitleClientValidator))
            {
                context.Results.Add(new ClientValidatorItem
                {
                    Validator = new MeaningfulTitleClientValidator(ResolveTitleMessage(context, title)),
                    IsReusable = false
                });
            }
        }

        if (context.ValidatorMetadata.OfType<MeaningfulTextAttribute>().FirstOrDefault() is { } text)
        {
            if (!context.Results.Any(static r => r.Validator is MeaningfulTextClientValidator))
            {
                context.Results.Add(new ClientValidatorItem
                {
                    Validator = new MeaningfulTextClientValidator(
                        ResolveTextMessage(context, text),
                        text.Optional,
                        text.Multiline),
                    IsReusable = false
                });
            }
        }
    }

    private static string ResolveHumanNameMessage(ClientValidatorProviderContext context, HumanNameAttribute attribute)
    {
        if (!string.IsNullOrWhiteSpace(attribute.ErrorMessage))
            return attribute.ErrorMessage!;

        var label = context.ModelMetadata.GetDisplayName();
        return InputTextRules.FormatHumanNameMessage(string.IsNullOrWhiteSpace(label) ? "Alan" : label);
    }

    private static string ResolveTitleMessage(ClientValidatorProviderContext context, MeaningfulTitleAttribute attribute)
    {
        if (!string.IsNullOrWhiteSpace(attribute.ErrorMessage))
            return attribute.ErrorMessage!;

        var label = context.ModelMetadata.GetDisplayName();
        return InputTextRules.FormatMeaningfulTitleMessage(string.IsNullOrWhiteSpace(label) ? "Alan" : label);
    }

    private static string ResolveTextMessage(ClientValidatorProviderContext context, MeaningfulTextAttribute attribute)
    {
        if (!string.IsNullOrWhiteSpace(attribute.ErrorMessage))
            return attribute.ErrorMessage!;

        var label = context.ModelMetadata.GetDisplayName();
        return InputTextRules.FormatMeaningfulTextMessage(string.IsNullOrWhiteSpace(label) ? "Alan" : label);
    }
}

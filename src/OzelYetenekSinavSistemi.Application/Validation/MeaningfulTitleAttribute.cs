using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Başlık ve kurum adı alanları için anlamlı metin doğrulaması.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MeaningfulTitleAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string text)
            return new ValidationResult(ResolveMessage(validationContext), new[] { validationContext.MemberName! });

        if (string.IsNullOrWhiteSpace(text))
            return ValidationResult.Success;

        return InputTextRules.IsValidMeaningfulTitle(text, out _)
            ? ValidationResult.Success
            : new ValidationResult(ResolveMessage(validationContext), new[] { validationContext.MemberName! });
    }

    private string ResolveMessage(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
            return ErrorMessage!;

        var label = validationContext.DisplayName;
        if (string.IsNullOrWhiteSpace(label))
            label = validationContext.MemberName ?? "Alan";

        return InputTextRules.FormatMeaningfulTitleMessage(label);
    }
}

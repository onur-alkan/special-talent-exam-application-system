using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Açıklama, adres ve serbest metin alanları için anlamlı içerik doğrulaması.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MeaningfulTextAttribute : ValidationAttribute
{
    public bool Optional { get; set; } = true;

    public bool Multiline { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string text)
            return new ValidationResult(ResolveMessage(validationContext), new[] { validationContext.MemberName! });

        if (Optional && string.IsNullOrWhiteSpace(text))
            return ValidationResult.Success;

        var isValid = Optional
            ? InputTextRules.IsValidOptionalMeaningfulText(text, Multiline, out _)
            : InputTextRules.IsValidRequiredMeaningfulText(text, Multiline, out _);

        return isValid
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

        return InputTextRules.FormatMeaningfulTextMessage(label);
    }
}

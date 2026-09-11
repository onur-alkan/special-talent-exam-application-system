using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Unicode insan adı doğrulaması. Boş değerler için <see cref="RequiredAttribute"/> kullanılır.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HumanNameAttribute : ValidationAttribute
{
    public HumanNameAttribute()
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string text)
            return new ValidationResult(ResolveMessage(validationContext), new[] { validationContext.MemberName! });

        return InputTextRules.IsValidHumanName(text, out _)
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

        return InputTextRules.FormatHumanNameMessage(label);
    }
}

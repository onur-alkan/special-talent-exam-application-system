using System.ComponentModel.DataAnnotations;
using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Kayıt formundaki cep telefonunu seçilen ülke ile birlikte doğrular.
/// Boş değerler için <see cref="RequiredAttribute"/> kullanılır.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MobilePhoneNumberAttribute : ValidationAttribute
{
    public const string CountryCodePropertyName = "PhoneCountryCode";

    public MobilePhoneNumberAttribute()
        : base(() => MobilePhoneNumber.InvalidMessage)
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string text)
            return new ValidationResult(MobilePhoneNumber.InvalidMessage);

        if (string.IsNullOrWhiteSpace(text))
            return ValidationResult.Success;

        var countryCode = GetCountryCode(validationContext);
        if (!MobilePhoneNumber.TryValidateAndNormalize(countryCode, text, out _, out var errorMessage))
        {
            return new ValidationResult(
                errorMessage ?? MobilePhoneNumber.InvalidMessage,
                validationContext.MemberName is { } member
                    ? new[] { member }
                    : null);
        }

        return ValidationResult.Success;
    }

    private static string? GetCountryCode(ValidationContext validationContext)
    {
        var property = validationContext.ObjectType.GetProperty(CountryCodePropertyName);
        if (property is null)
            return null;

        return property.GetValue(validationContext.ObjectInstance) as string;
    }
}

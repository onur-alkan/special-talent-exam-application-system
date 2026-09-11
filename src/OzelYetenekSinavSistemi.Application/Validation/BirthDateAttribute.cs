using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Doğum tarihinin gelecekte olmamasını doğrular. Boş değerler için <see cref="RequiredAttribute"/> kullanılır.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class BirthDateAttribute : ValidationAttribute
{
    public BirthDateAttribute()
        : base(() => BirthDateRules.FutureMessage)
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        if (value is not DateOnly date)
            return new ValidationResult(BirthDateRules.InvalidMessage);

        var timeProvider = (TimeProvider?)validationContext.GetService(typeof(TimeProvider))
                           ?? TimeProvider.System;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        if (!BirthDateRules.IsNotFuture(date, today))
            return new ValidationResult(BirthDateRules.FutureMessage);

        return ValidationResult.Success;
    }
}

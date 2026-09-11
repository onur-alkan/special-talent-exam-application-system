using System.ComponentModel.DataAnnotations;

namespace OzelYetenekSinavSistemi.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class BirthYearAttribute : ValidationAttribute
{
    public BirthYearAttribute()
        : base(BirthYearRules.InvalidMessage)
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;

        var timeProvider = validationContext.GetService(typeof(TimeProvider)) as TimeProvider
                           ?? TimeProvider.System;
        var currentYear = timeProvider.GetUtcNow().Year;

        return value is int year && BirthYearRules.IsValid(year, currentYear)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage, new[] { validationContext.MemberName! });
    }
}

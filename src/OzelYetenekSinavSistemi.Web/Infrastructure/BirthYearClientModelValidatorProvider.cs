using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// <see cref="BirthYearAttribute"/> için unobtrusive range validation ve HTML min/max üretir.
/// </summary>
public sealed class BirthYearClientModelValidatorProvider : IClientModelValidatorProvider
{
    private readonly TimeProvider _timeProvider;

    public BirthYearClientModelValidatorProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public void CreateValidators(ClientValidatorProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ValidatorMetadata.OfType<BirthYearAttribute>().FirstOrDefault() is null)
            return;

        if (context.Results.Any(static result => result.Validator is BirthYearClientValidator))
            return;

        context.Results.Add(new ClientValidatorItem
        {
            Validator = new BirthYearClientValidator(_timeProvider),
            IsReusable = false
        });
    }
}

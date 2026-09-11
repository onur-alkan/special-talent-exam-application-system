using Microsoft.AspNetCore.Mvc.ModelBinding;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Application.ViewModels.Account;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Kayıt formundaki gün/ay/yıl seçimlerinden BirthDate üretir.
/// İstemciden gelen gizli BirthDate değerine güvenilmez.
/// </summary>
public static class BirthDateFormBinding
{
    public static void Apply(RegisterViewModel model, ModelStateDictionary modelState, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(modelState);

        modelState.Remove(nameof(RegisterViewModel.BirthDate));

        var hasParts = model.BirthDateDay.HasValue
                       || model.BirthDateMonth.HasValue
                       || model.BirthDateYear.HasValue;

        if (!hasParts)
        {
            // Forma gün/ay/yıl gelmediyse (ör. birim testleri) mevcut BirthDate doğrulanır.
            if (model.BirthDate is null)
            {
                modelState.AddModelError(nameof(RegisterViewModel.BirthDate), BirthDateRules.RequiredMessage);
                return;
            }

            if (!BirthDateRules.IsNotFuture(model.BirthDate.Value, today))
            {
                modelState.AddModelError(nameof(RegisterViewModel.BirthDate), BirthDateRules.FutureMessage);
            }

            return;
        }

        // Parçalar varsa istemci BirthDate'ine güvenilmez; yeniden üretilir.
        model.BirthDate = null;

        if (!BirthDateRules.TryCompose(
                model.BirthDateDay,
                model.BirthDateMonth,
                model.BirthDateYear,
                today,
                out var composed,
                out var error))
        {
            modelState.AddModelError(nameof(RegisterViewModel.BirthDate), error ?? BirthDateRules.InvalidMessage);
            return;
        }

        model.BirthDate = composed;
    }

    public static void SyncPartsFromComposedDate(RegisterViewModel model)
    {
        if (model.BirthDate is not { } date)
            return;

        model.BirthDateDay ??= date.Day;
        model.BirthDateMonth ??= date.Month;
        model.BirthDateYear ??= date.Year;
    }

    public static void PopulateYearOptions(RegisterViewModel model, DateOnly today)
    {
        model.BirthDateYearOptions = BirthDateRules.BuildYearOptions(today);
    }
}

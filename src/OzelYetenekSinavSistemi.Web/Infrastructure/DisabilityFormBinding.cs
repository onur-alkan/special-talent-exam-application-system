using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Form bağlama sonrası engel açıklamasını ModelState ile birlikte temizler.
/// </summary>
public static class DisabilityFormBinding
{
    public static void Normalize(
        bool hasDisability,
        Action<string?> setDisabilityDetails,
        ModelStateDictionary modelState,
        string modelStateKey = "DisabilityDetails")
    {
        if (hasDisability)
            return;

        setDisabilityDetails(null);
        modelState.Remove(modelStateKey);
    }
}

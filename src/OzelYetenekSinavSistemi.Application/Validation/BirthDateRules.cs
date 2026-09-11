namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// Aday doğum tarihi kuralları: geçerli takvim tarihi ve gelecekte olmama.
/// Yaş üst/alt sınırı uygulanmaz (yalnız kayıt formu için).
/// </summary>
public static class BirthDateRules
{
    public const string RequiredMessage = "Doğum tarihinizi gün, ay ve yıl olarak seçiniz.";
    public const string InvalidMessage = "Geçerli bir doğum tarihi seçiniz.";
    public const string FutureMessage = "Doğum tarihi gelecekte olamaz.";
    public const string UnspecifiedDisplay = "Belirtilmemiş";
    public const string FullDateLabel = "Doğum Tarihi";
    public const string LegacyYearLabel = "Doğum Yılı (eski kayıt)";

    /// <summary>Takvim yılı listesi alt sınırı (UI); doğrulama kuralı değildir.</summary>
    public const int EarliestSelectableYear = 1900;

    public static readonly string[] TurkishMonthNames =
    [
        string.Empty,
        "Ocak",
        "Şubat",
        "Mart",
        "Nisan",
        "Mayıs",
        "Haziran",
        "Temmuz",
        "Ağustos",
        "Eylül",
        "Ekim",
        "Kasım",
        "Aralık"
    ];

    public static bool IsNotFuture(DateOnly birthDate, DateOnly today) => birthDate <= today;

    public static bool IsValid(DateOnly? birthDate, DateOnly today)
        => birthDate is not null && IsNotFuture(birthDate.Value, today);

    public static bool TryCompose(
        int? day,
        int? month,
        int? year,
        DateOnly today,
        out DateOnly? birthDate,
        out string? errorMessage)
    {
        birthDate = null;
        errorMessage = null;

        var any = day.HasValue || month.HasValue || year.HasValue;
        var all = day.HasValue && month.HasValue && year.HasValue;
        if (!any || !all)
        {
            errorMessage = RequiredMessage;
            return false;
        }

        try
        {
            var composed = new DateOnly(year!.Value, month!.Value, day!.Value);
            if (!IsNotFuture(composed, today))
            {
                errorMessage = FutureMessage;
                return false;
            }

            birthDate = composed;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            errorMessage = InvalidMessage;
            return false;
        }
    }

    public static IReadOnlyList<int> BuildYearOptions(DateOnly today)
    {
        var years = new List<int>(capacity: Math.Max(1, today.Year - EarliestSelectableYear + 1));
        for (var y = today.Year; y >= EarliestSelectableYear; y--)
            years.Add(y);
        return years;
    }

    public static string ResolveProfileLabel(DateOnly? birthDate, short? birthYear)
    {
        if (birthDate is not null)
            return FullDateLabel;
        if (birthYear is not null)
            return LegacyYearLabel;
        return FullDateLabel;
    }

    public static string ResolveProfileDisplay(DateOnly? birthDate, short? birthYear)
    {
        if (birthDate is not null)
            return birthDate.Value.ToString("dd.MM.yyyy");
        if (birthYear is not null)
            return birthYear.Value.ToString();
        return UnspecifiedDisplay;
    }
}

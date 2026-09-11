namespace OzelYetenekSinavSistemi.Application.Validation;

/// <summary>
/// ASP.NET Core model-binding ve sayısal client doğrulama mesajları (yalnız metin; kural değişmez).
/// </summary>
public static class TurkishModelBindingMessages
{
    public const string GenericNumber = "Geçerli bir sayı giriniz.";
    public const string GenericValue = "Girilen değeri kontrol ediniz.";
    public const string GenericDate = "Tarih bilgisini kontrol ediniz.";
    public const string GenericRequired = "Bu bilgiyi giriniz.";

    private static readonly Dictionary<string, string> NumberByDisplayName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["YGS Puanı"] = "YGS puanını sayı olarak giriniz.",
            ["Görünüm Sırası"] = "Görünüm sırasını sayı olarak giriniz.",
            ["Maksimum Tercih Sayısı"] = "Maksimum tercih sayısını sayı olarak giriniz.",
            ["Sınav Puanı"] = "Sınav puanını sayı olarak giriniz.",
            ["Değer"] = "Değeri sayı olarak giriniz."
        };

    private static readonly Dictionary<string, string> NumberByPropertyName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["YgsScore"] = "YGS puanını sayı olarak giriniz.",
            ["DisplayOrder"] = "Görünüm sırasını sayı olarak giriniz.",
            ["MaxPreferences"] = "Maksimum tercih sayısını sayı olarak giriniz.",
            ["ExamScore"] = "Sınav puanını sayı olarak giriniz.",
            ["SettingValue"] = "Değeri sayı olarak giriniz."
        };

    private static readonly Dictionary<string, string> InvalidByDisplayName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Başlangıç Tarihi"] = "Başlangıç tarihini kontrol ediniz.",
            ["Bitiş Tarihi"] = "Bitiş tarihini kontrol ediniz.",
            ["Pasaport Son Geçerlilik Tarihi"] = "Pasaport son geçerlilik tarihini kontrol ediniz.",
            ["Doğum Tarihi"] = "Geçerli bir doğum tarihi giriniz.",
            ["Kimlik Belgesi Türü"] = "Kimlik belgesi türünü seçiniz.",
            ["Sınava Girme Durumu"] = "Sınava girme durumunu seçiniz.",
            ["YGS Yılı"] = "YGS yılını seçiniz.",
            ["YGS Puanı"] = "YGS puanını sayı olarak giriniz.",
            ["Görünüm Sırası"] = "Görünüm sırasını sayı olarak giriniz.",
            ["Maksimum Tercih Sayısı"] = "Maksimum tercih sayısını sayı olarak giriniz.",
            ["Sınav Puanı"] = "Sınav puanını sayı olarak giriniz."
        };

    private static readonly Dictionary<string, string> InvalidByPropertyName =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["StartDate"] = "Başlangıç tarihini kontrol ediniz.",
            ["EndDate"] = "Bitiş tarihini kontrol ediniz.",
            ["PassportExpiryDate"] = "Pasaport son geçerlilik tarihini kontrol ediniz.",
            ["BirthDate"] = "Geçerli bir doğum tarihi giriniz.",
            ["IdentityDocumentType"] = "Kimlik belgesi türünü seçiniz.",
            ["AttendanceStatus"] = "Sınava girme durumunu seçiniz.",
            ["YgsYearId"] = "YGS yılını seçiniz.",
            ["YgsScore"] = "YGS puanını sayı olarak giriniz.",
            ["DisplayOrder"] = "Görünüm sırasını sayı olarak giriniz.",
            ["MaxPreferences"] = "Maksimum tercih sayısını sayı olarak giriniz.",
            ["ExamScore"] = "Sınav puanını sayı olarak giriniz."
        };

    private static readonly HashSet<string> DateKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Başlangıç Tarihi",
        "Bitiş Tarihi",
        "Pasaport Son Geçerlilik Tarihi",
        "Doğum Tarihi",
        "StartDate",
        "EndDate",
        "PassportExpiryDate",
        "BirthDate",
        "ExpectedUpdatedDate"
    };

    public static string ValueMustBeANumber(string fieldName)
        => Resolve(fieldName, NumberByDisplayName, NumberByPropertyName, GenericNumber);

    public static string NonPropertyValueMustBeANumber()
        => GenericNumber;

    public static string AttemptedValueIsInvalid(string value, string fieldName)
        => ResolveInvalid(fieldName);

    public static string UnknownValueIsInvalid(string fieldName)
        => ResolveInvalid(fieldName);

    public static string ValueIsInvalid(string fieldName)
        => ResolveInvalid(fieldName);

    public static string ValueMustNotBeNull(string fieldName)
        => GenericRequired;

    public static string MissingBindRequiredValue(string parameterName)
        => GenericRequired;

    public static string MissingKeyOrValue()
        => GenericRequired;

    public static string NonPropertyAttemptedValueIsInvalid(string value)
        => GenericValue;

    public static string NonPropertyUnknownValueIsInvalid()
        => GenericValue;

    public static string MissingRequestBodyRequiredValue()
        => GenericRequired;

    private static string ResolveInvalid(string fieldName)
    {
        if (IsDateField(fieldName))
            return Resolve(fieldName, InvalidByDisplayName, InvalidByPropertyName, GenericDate);

        return Resolve(fieldName, InvalidByDisplayName, InvalidByPropertyName, GenericValue);
    }

    private static string Resolve(
        string fieldName,
        IReadOnlyDictionary<string, string> byDisplay,
        IReadOnlyDictionary<string, string> byProperty,
        string generic)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return generic;

        var key = fieldName.Trim();

        if (byDisplay.TryGetValue(key, out var byDisplayMessage))
            return byDisplayMessage;

        if (byProperty.TryGetValue(key, out var byPropertyMessage))
            return byPropertyMessage;

        return generic;
    }

    private static bool IsDateField(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return false;

        var key = fieldName.Trim();
        return DateKeys.Contains(key)
               || key.Contains("tarih", StringComparison.OrdinalIgnoreCase);
    }
}

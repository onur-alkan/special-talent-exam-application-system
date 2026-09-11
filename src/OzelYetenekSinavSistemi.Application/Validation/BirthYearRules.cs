namespace OzelYetenekSinavSistemi.Application.Validation;

public static class BirthYearRules
{
    public const int MaximumAge = 120;
    public const string InvalidMessage = "Geçerli bir doğum yılı giriniz.";

    public static int MinimumYear(int currentYear) => currentYear - MaximumAge;

    public static bool IsValid(int? birthYear, int currentYear)
        => birthYear is >= 1000 and <= 9999
           && birthYear >= MinimumYear(currentYear)
           && birthYear <= currentYear;
}

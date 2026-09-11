namespace OzelYetenekSinavSistemi.Application.Common;

/// <summary>
/// T.C. Kimlik Numarası algoritmik doğrulaması (statik, DI gerektirmez).
/// </summary>
public static class TurkishIdentityNumber
{
    public const int Length = 11;
    public const string InvalidMessage = "Geçerli bir T.C. Kimlik Numarası giriniz.";

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var tcNo = value.Trim();
        if (tcNo.Length != Length)
            return false;

        if (tcNo[0] == '0')
            return false;

        Span<int> digits = stackalloc int[Length];
        for (var i = 0; i < Length; i++)
        {
            var ch = tcNo[i];
            if (ch is < '0' or > '9')
                return false;
            digits[i] = ch - '0';
        }

        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        var digit10 = ((oddSum * 7) - evenSum) % 10;
        if (digit10 < 0)
            digit10 += 10;

        if (digit10 != digits[9])
            return false;

        var sum10 = 0;
        for (var i = 0; i < 10; i++)
            sum10 += digits[i];

        return sum10 % 10 == digits[10];
    }
}

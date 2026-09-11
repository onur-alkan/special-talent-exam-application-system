namespace OzelYetenekSinavSistemi.Application.Common;

public sealed record PhoneCountryOption(string Code, string DisplayName, int DialCode)
{
    public string Label => $"{DisplayName} (+{DialCode})";
}

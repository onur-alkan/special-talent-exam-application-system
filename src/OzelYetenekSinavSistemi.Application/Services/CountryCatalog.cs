using System.Globalization;
using System.Reflection;
using System.Text.Json;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Domain.Common;
using PhoneNumbers;

namespace OzelYetenekSinavSistemi.Application.Services;

public sealed class CountryCatalog : ICountryCatalog
{
    private const string TurkeyCode = "TR";
    private const string ResourceName = "OzelYetenekSinavSistemi.Application.Data.countries.tr.json";

    private static readonly StringComparer TurkishNameComparer =
        StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: false);

    private readonly IReadOnlyList<CountryOption> _all;
    private readonly IReadOnlyList<CountryOption> _foreignNationalities;
    private readonly IReadOnlyList<PhoneCountryOption> _phoneCountries;
    private readonly IReadOnlyDictionary<string, CountryOption> _byCode;

    public CountryCatalog()
    {
        var countries = LoadCountries();
        _all = countries;
        _foreignNationalities = countries.Where(c => c.Code != TurkeyCode).ToList();
        _byCode = countries.ToDictionary(c => c.Code, StringComparer.Ordinal);
        _phoneCountries = BuildPhoneCountries(countries);
    }

    public IReadOnlyList<CountryOption> GetAll() => _all;

    public IReadOnlyList<CountryOption> GetForeignNationalities() => _foreignNationalities;

    public IReadOnlyList<PhoneCountryOption> GetPhoneCountries() => _phoneCountries;

    public CountryOption? GetByCode(string? code)
    {
        if (TryGetByCode(code, out var option))
            return option;

        return null;
    }

    public bool TryGetByCode(string? code, out CountryOption? option)
    {
        option = null;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().ToUpperInvariant();
        if (_byCode.TryGetValue(normalized, out var found))
        {
            option = found;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<CountryOption> LoadCountries()
    {
        var assembly = typeof(CountryCatalog).Assembly;
        using var stream = OpenCountryDataStream(assembly)
            ?? throw new InvalidOperationException($"Gömülü ülke kataloğu bulunamadı: {ResourceName}");

        var entries = JsonSerializer.Deserialize<List<CountryJsonEntry>>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Ülke kataloğu okunamadı.");

        var options = new List<CountryOption>(entries.Count);
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Code) || string.IsNullOrWhiteSpace(entry.Name))
                throw new InvalidOperationException("Ülke kataloğunda eksik kayıt bulundu.");

            var code = entry.Code.Trim().ToUpperInvariant();
            if (code.Length != DomainConstants.CountryCodeLength)
                throw new InvalidOperationException($"Geçersiz ülke kodu: {entry.Code}");

            foreach (var ch in code)
            {
                if (ch is < 'A' or > 'Z')
                    throw new InvalidOperationException($"Geçersiz ülke kodu: {entry.Code}");
            }

            if (!seenCodes.Add(code))
                throw new InvalidOperationException($"Yinelenen ülke kodu: {code}");

            options.Add(new CountryOption(code, entry.Name.Trim()));
        }

        if (!seenCodes.Contains(TurkeyCode))
            throw new InvalidOperationException("Ülke kataloğunda TR bulunamadı.");

        options.Sort((left, right) => TurkishNameComparer.Compare(left.DisplayName, right.DisplayName));
        return options;
    }

    private static IReadOnlyList<PhoneCountryOption> BuildPhoneCountries(IReadOnlyList<CountryOption> countries)
    {
        var util = PhoneNumberUtil.GetInstance();
        var phoneCountries = new List<PhoneCountryOption>(countries.Count);

        foreach (var country in countries)
        {
            var dialCode = util.GetCountryCodeForRegion(country.Code);
            if (dialCode <= 0)
                continue;

            phoneCountries.Add(new PhoneCountryOption(country.Code, country.DisplayName, dialCode));
        }

        phoneCountries.Sort((left, right) =>
        {
            if (left.Code == TurkeyCode && right.Code != TurkeyCode)
                return -1;
            if (right.Code == TurkeyCode && left.Code != TurkeyCode)
                return 1;
            return TurkishNameComparer.Compare(left.DisplayName, right.DisplayName);
        });

        if (phoneCountries.All(c => c.Code != TurkeyCode))
            throw new InvalidOperationException("Telefon ülke listesinde TR bulunamadı.");

        return phoneCountries;
    }

    private static Stream? OpenCountryDataStream(Assembly assembly)
    {
        var embedded = assembly.GetManifestResourceStream(ResourceName);
        if (embedded is not null)
            return embedded;

        var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "countries.tr.json");
        if (File.Exists(filePath))
            return File.OpenRead(filePath);

        return assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("countries.tr.json", StringComparison.OrdinalIgnoreCase)) is { } resourceName
            ? assembly.GetManifestResourceStream(resourceName)
            : null;
    }

    private sealed class CountryJsonEntry
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}

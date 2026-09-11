using OzelYetenekSinavSistemi.Application.Common;

namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

public interface ICountryCatalog
{
    IReadOnlyList<CountryOption> GetAll();

    IReadOnlyList<CountryOption> GetForeignNationalities();

    /// <summary>
    /// Telefon ülke seçimi: mevcut ülke adları + libphonenumber arama kodları.
    /// Desteklenmeyen bölgeler (arama kodu olmayanlar) elenir; Türkiye listenin başındadır.
    /// </summary>
    IReadOnlyList<PhoneCountryOption> GetPhoneCountries();

    CountryOption? GetByCode(string? code);

    bool TryGetByCode(string? code, out CountryOption? option);
}

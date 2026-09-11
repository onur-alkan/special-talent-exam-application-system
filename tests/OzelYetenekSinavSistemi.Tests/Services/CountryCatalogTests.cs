using Microsoft.Extensions.DependencyInjection;
using OzelYetenekSinavSistemi.Application;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;
using OzelYetenekSinavSistemi.Application.Services;

namespace OzelYetenekSinavSistemi.Tests.Services;

public sealed class CountryCatalogTests
{
    [Fact]
    public void GetAll_CodesAreTwoUpperAsciiLetters()
    {
        var sut = CreateCatalog();
        Assert.All(sut.GetAll(), option =>
        {
            Assert.Equal(2, option.Code.Length);
            Assert.True(option.Code.All(ch => ch is >= 'A' and <= 'Z'));
        });
    }

    [Fact]
    public void GetAll_CodesAreUnique()
    {
        var sut = CreateCatalog();
        var codes = sut.GetAll().Select(c => c.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GetAll_ContainsTurkeyWithTurkishName()
    {
        var sut = CreateCatalog();
        var turkey = sut.GetByCode("TR");

        Assert.NotNull(turkey);
        Assert.Equal("Türkiye", turkey!.DisplayName);
    }

    [Fact]
    public void GetAll_IsSortedByTurkishDisplayName()
    {
        var sut = CreateCatalog();
        var names = sut.GetAll().Select(c => c.DisplayName).ToList();
        var sorted = names.OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false)).ToList();

        Assert.Equal(sorted, names);
    }

    [Fact]
    public void GetAll_HasComprehensiveIsoCoverage()
    {
        var sut = CreateCatalog();
        Assert.InRange(sut.GetAll().Count, 240, 260);
    }

    [Fact]
    public void GetForeignNationalities_ExcludesTurkey()
    {
        var sut = CreateCatalog();
        Assert.DoesNotContain(sut.GetForeignNationalities(), c => c.Code == "TR");
        Assert.Equal(sut.GetAll().Count - 1, sut.GetForeignNationalities().Count);
    }

    [Fact]
    public void ResolveFromDi_ReturnsSameCatalogInstanceType()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<ICountryCatalog>();
        Assert.IsType<CountryCatalog>(catalog);
        Assert.True(catalog.GetAll().Count >= 240);
    }

    [Fact]
    public void CountryCatalog_TryGetByCode_ResolvesKnownCountry()
    {
        var sut = CreateCatalog();
        Assert.True(sut.TryGetByCode("TR", out var turkey));
        Assert.NotNull(turkey);
        Assert.Equal("Türkiye", turkey!.DisplayName);
        Assert.False(sut.TryGetByCode("ZZ", out _));
    }

    private static CountryCatalog CreateCatalog() => new();
}

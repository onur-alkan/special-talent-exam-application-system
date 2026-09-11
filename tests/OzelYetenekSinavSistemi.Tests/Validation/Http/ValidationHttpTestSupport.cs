using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

internal static class ValidationHttpTestSupport
{
    internal static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        bool allowAutoRedirect = true)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true,
            MaxAutomaticRedirections = 7
        });
    }

    internal static string? ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            match = Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static async Task<(HttpResponseMessage Response, string Body, string Token)> GetFormAsync(
        HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return (response, body, ExtractAntiforgeryToken(body) ?? string.Empty);
    }

    internal static FormUrlEncodedContent BuildForm(string token, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token)
        };
        values.AddRange(fields);
        return new FormUrlEncodedContent(values);
    }

    internal static void CopyCookies(HttpResponseMessage from, HttpRequestMessage to)
    {
        if (!from.Headers.TryGetValues("Set-Cookie", out var cookies))
            return;

        to.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(c => c.Split(';')[0])));
    }

    internal static MultipartFormDataContent BuildRegisterForm(
        string token,
        string firstName,
        string lastName,
        string? email = null,
        string? identityNumber = null,
        string? birthDate = "2000-06-15",
        string? phoneCountryCode = "TR")
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(firstName), "FirstName" },
            { new StringContent(lastName), "LastName" },
            { new StringContent("1"), "IdentityDocumentType" },
            { new StringContent(identityNumber ?? "52345678903"), "IdentityNumber" },
            { new StringContent("TR"), "NationalityCountryCode" },
            { new StringContent(email ?? $"register-{unique}@test.local"), "Email" },
            { new StringContent(phoneCountryCode ?? string.Empty), "PhoneCountryCode" },
            { new StringContent("5321234567"), "Phone" },
            { new StringContent("Passw0rd!Aa"), "Password" },
            { new StringContent("Passw0rd!Aa"), "ConfirmPassword" },
            { new StringContent("250"), "YgsScore" }
        };

        AddBirthDateParts(content, birthDate);
        return content;
    }

    internal static void AddBirthDateParts(MultipartFormDataContent content, string? birthDate)
    {
        if (string.IsNullOrWhiteSpace(birthDate))
        {
            content.Add(new StringContent(""), "BirthDateDay");
            content.Add(new StringContent(""), "BirthDateMonth");
            content.Add(new StringContent(""), "BirthDateYear");
            return;
        }

        if (DateOnly.TryParse(birthDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            content.Add(new StringContent(parsed.Day.ToString(CultureInfo.InvariantCulture)), "BirthDateDay");
            content.Add(new StringContent(parsed.Month.ToString(CultureInfo.InvariantCulture)), "BirthDateMonth");
            content.Add(new StringContent(parsed.Year.ToString(CultureInfo.InvariantCulture)), "BirthDateYear");
            return;
        }

        var match = Regex.Match(birthDate, @"^(\d{4})-(\d{2})-(\d{2})$");
        if (match.Success)
        {
            content.Add(new StringContent(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture)), "BirthDateDay");
            content.Add(new StringContent(int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture)), "BirthDateMonth");
            content.Add(new StringContent(match.Groups[1].Value), "BirthDateYear");
            return;
        }

        // Ayrıştırılamayan girdi → geçersiz takvim kombinasyonu (31 Şubat)
        content.Add(new StringContent("31"), "BirthDateDay");
        content.Add(new StringContent("2"), "BirthDateMonth");
        content.Add(new StringContent("2001"), "BirthDateYear");
    }
}

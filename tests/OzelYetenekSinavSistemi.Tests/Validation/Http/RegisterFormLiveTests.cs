using System.Net;
using Dapper;
using Microsoft.Data.SqlClient;
using OzelYetenekSinavSistemi.Application.Common;
using OzelYetenekSinavSistemi.Application.Validation;

namespace OzelYetenekSinavSistemi.Tests.Validation.Http;

public sealed class RegisterFormLiveTests(RegisterFormLiveFixture fixture) : RegisterFormLiveTestBase(fixture)
{
    [Fact]
    public void Fixture_UsesIsolatedTestDatabasePrefix()
    {
        TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(Fixture.ConnectionString);
        var builder = new SqlConnectionStringBuilder(Fixture.ConnectionString);
        Assert.StartsWith(TestDatabaseSafetyGuard.AllowedPrefix, builder.InitialCatalog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_Get_IncludesClientValidationAttributes()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-val=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("data-val-humanname", html, StringComparison.Ordinal);
        Assert.Contains("input-text-validation.js", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"FirstName\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-val-length-max=\"50\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jquery.validate", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jquery.validate.unobtrusive", html, StringComparison.OrdinalIgnoreCase);

        var decoded = WebUtility.HtmlDecode(html);
        Assert.Contains("Doğum Tarihi", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("mm/dd/yyyy", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"BirthDateDay\"", decoded, StringComparison.Ordinal);
        Assert.Contains("name=\"BirthDateMonth\"", decoded, StringComparison.Ordinal);
        Assert.Contains("name=\"BirthDateYear\"", decoded, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"BirthDate\"", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"BirthYear\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("data-oys-birth-date\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("field-validation-error", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_Get_PhoneCountrySelect_HasTurkeySelectedWithoutRequiredErrorOrLengthMin()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");
        var decoded = WebUtility.HtmlDecode(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"PhoneCountryCode\"", decoded, StringComparison.Ordinal);
        Assert.Contains("data-oys-phone-country", decoded, StringComparison.Ordinal);
        Assert.Contains("data-val-required=\"Telefon için ülke seçiniz.\"", decoded, StringComparison.Ordinal);
        Assert.Contains("Türkiye (+90)", decoded, StringComparison.Ordinal);
        Assert.Matches(
            @"<option[^>]*value=""TR""[^>]*selected(?:=""selected"")?|<option[^>]*selected(?:=""selected"")?[^>]*value=""TR""",
            decoded);
        // PhoneCountryCode select'te StringLength MinLength olmamalı (jQuery option sayısı ölçer).
        Assert.DoesNotMatch(
            @"name=""PhoneCountryCode""[^>]*(data-val-length-min|data-val-length=)",
            decoded);
        Assert.DoesNotContain(
            "data-valmsg-for=\"PhoneCountryCode\" class=\"text-danger small field-validation-error\"",
            decoded,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Telefon için ülke seçiniz.<", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPost_EmptyPhoneCountry_ReturnsTurkishRequiredMessage()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"phone-country-empty-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(
            token, "Ali", "Yılmaz", email, phoneCountryCode: "");

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(MobilePhoneNumber.CountryRequiredMessage, decoded, StringComparison.Ordinal);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task RegisterPost_InvalidFirstName_PreservesSelectedPhoneCountry()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"phone-country-keep-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(
            token, "--------", "Yılmaz", email, phoneCountryCode: "DE");

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches(
            @"<option[^>]*value=""DE""[^>]*selected(?:=""selected"")?|<option[^>]*selected(?:=""selected"")?[^>]*value=""DE""",
            decoded);
        Assert.DoesNotContain(">Telefon için ülke seçiniz.<", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPost_InvalidFirstName_ReturnsViewWithTurkishErrorWithoutCreatingUser()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, html, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var email = $"invalid-first-{Guid.NewGuid():N}@test.local";
        var before = await CountUsersByEmailAsync(email);

        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, "--------", "Yılmaz", email);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("harf kullanarak", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-valmsg-for=\"FirstName\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kaydınız başarıyla oluşturuldu", body, StringComparison.Ordinal);
        Assert.Equal(before, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task RegisterPost_InvalidLastName_ReturnsViewWithTurkishErrorWithoutCreatingUser()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, html, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"invalid-last-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, "Ali", "........", email);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("harf kullanarak", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Theory]
    [InlineData("Jean-Pierre", "O\u2019Connor")]
    [InlineData("Ömer Faruk", "Yıldız")]
    public async Task RegisterPost_ValidUnicodeNames_DoNotFailNameValidation(string firstName, string lastName)
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, html, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, firstName, lastName);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(InputTextRules.FormatHumanNameMessage("Ad"), body, StringComparison.Ordinal);
        Assert.DoesNotContain(InputTextRules.FormatHumanNameMessage("Soyad"), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPost_ZeroWidthFirstName_IsRejectedWithoutCreatingUser()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, html, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"zero-width-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, "Ali\u200B", "Veli", email);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("harf kullanarak", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task RegisterPost_BidiFirstName_IsRejectedWithoutCreatingUser()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, html, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"bidi-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, "\u202EAli", "Veli", email);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task Register_Get_YgsScore_HasTurkishNumberClientMessage()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (response, html, _) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decoded = WebUtility.HtmlDecode(html);
        Assert.Contains("name=\"YgsScore\"", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-val-number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("YGS puanını sayı olarak giriniz.", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The field", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value=\"--\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            """(?is)name\s*=\s*["']YgsScore["'][^>]*value\s*=\s*["']--["']""",
            decoded);
    }

    [Fact]
    public async Task RegisterPost_EmptyRequiredYgsScore_ReturnsTurkishRequiredMessage()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"ygs-empty-{Guid.NewGuid():N}@test.local";
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Ali"), "FirstName" },
            { new StringContent("Yılmaz"), "LastName" },
            { new StringContent("1"), "IdentityDocumentType" },
            { new StringContent("52345678903"), "IdentityNumber" },
            { new StringContent("TR"), "NationalityCountryCode" },
            { new StringContent("2000-06-15"), "BirthDate" },
            { new StringContent(email), "Email" },
            { new StringContent("TR"), "PhoneCountryCode" },
            { new StringContent("5321234567"), "Phone" },
            { new StringContent("Passw0rd!Aa"), "Password" },
            { new StringContent("Passw0rd!Aa"), "ConfirmPassword" },
            { new StringContent(""), "YgsScore" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("YGS puanınızı giriniz.", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The field", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("--")]
    public async Task RegisterPost_NonNumericYgsScore_ReturnsTurkishNumberMessageWithoutCreatingUser(string invalidScore)
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"ygs-invalid-{Guid.NewGuid():N}@test.local";
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Ali"), "FirstName" },
            { new StringContent("Yılmaz"), "LastName" },
            { new StringContent("1"), "IdentityDocumentType" },
            { new StringContent("52345678903"), "IdentityNumber" },
            { new StringContent("TR"), "NationalityCountryCode" },
            { new StringContent("2000-06-15"), "BirthDate" },
            { new StringContent(email), "Email" },
            { new StringContent("TR"), "PhoneCountryCode" },
            { new StringContent("5321234567"), "Phone" },
            { new StringContent("Passw0rd!Aa"), "Password" },
            { new StringContent("Passw0rd!Aa"), "ConfirmPassword" },
            { new StringContent(invalidScore), "YgsScore" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var decoded = WebUtility.HtmlDecode(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("YGS puanını sayı olarak giriniz.", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The field", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The value", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("01234567890")]
    [InlineData("11111111111")]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public async Task RegisterPost_InvalidTurkishIdentity_ReturnsExactFieldMessageWithoutCreatingUser(string invalidTc)
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"invalid-tc-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, "Ali", "Yılmaz", email, invalidTc);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(TurkishIdentityNumber.InvalidMessage, decoded, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"IdentityNumber\"", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kaydınız başarıyla oluşturuldu", decoded, StringComparison.Ordinal);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task RegisterPost_ValidSyntheticTurkishIdentity_DoesNotShowChecksumError()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        using var content = ValidationHttpTestSupport.BuildRegisterForm(
            token, "Ali", "Yılmaz", identityNumber: "10000000146");
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(TurkishIdentityNumber.InvalidMessage, decoded, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPost_EmptyBirthDate_ReturnsRequiredTurkishMessage()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"birth-empty-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(
            token, "Ali", "Yılmaz", email, birthDate: "");

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(BirthDateRules.RequiredMessage, decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("The value", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task RegisterPost_FutureBirthDate_ReturnsFutureTurkishMessage()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"birth-future-{Guid.NewGuid():N}@test.local";
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1))
            .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var content = ValidationHttpTestSupport.BuildRegisterForm(
            token, "Ali", "Yılmaz", email, birthDate: tomorrow);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(BirthDateRules.FutureMessage, decoded, StringComparison.Ordinal);
        var tomorrowDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1));
        Assert.Contains($"value=\"{tomorrowDate.Day}\"", decoded, StringComparison.Ordinal);
        Assert.Contains($"value=\"{tomorrowDate.Month}\"", decoded, StringComparison.Ordinal);
        Assert.Contains($"value=\"{tomorrowDate.Year}\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("is not valid", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Theory]
    [InlineData("2001-02-29")]
    [InlineData("2000-02-31")]
    [InlineData("not-a-date")]
    public async Task RegisterPost_InvalidBirthDate_ReturnsInvalidTurkishMessage(string invalidDate)
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"birth-invalid-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(
            token, "Ali", "Yılmaz", email, birthDate: invalidDate);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(BirthDateRules.InvalidMessage, decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("The value", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is not valid", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountUsersByEmailAsync(email));
    }

    [Fact]
    public async Task RegisterPost_ValidBirthDatePreserved_WhenOtherFieldInvalid()
    {
        var client = ValidationHttpTestSupport.CreateClient(Fixture.Factory, allowAutoRedirect: false);
        var (get, _, token) = await ValidationHttpTestSupport.GetFormAsync(client, "/Account/Register");

        var email = $"birth-keep-{Guid.NewGuid():N}@test.local";
        using var content = ValidationHttpTestSupport.BuildRegisterForm(token, "--------", "Yılmaz", email);
        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register") { Content = content };
        ValidationHttpTestSupport.CopyCookies(get, request);
        var response = await client.SendAsync(request);
        var decoded = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("value=\"15\"", decoded, StringComparison.Ordinal);
        Assert.Contains("value=\"6\"", decoded, StringComparison.Ordinal);
        Assert.Contains("value=\"2000\"", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "data-valmsg-for=\"BirthDate\" class=\"text-danger field-validation-error\"",
            decoded,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> CountUsersByEmailAsync(string email)
    {
        await using var connection = new SqlConnection(Fixture.ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Users WHERE Email = @Email",
            new { Email = email }).ConfigureAwait(false);
    }
}

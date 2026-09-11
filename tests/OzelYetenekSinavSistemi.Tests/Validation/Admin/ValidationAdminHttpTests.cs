using System.Net;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OzelYetenekSinavSistemi.Application.Interfaces.Infrastructure;
using OzelYetenekSinavSistemi.Application.Validation;
using OzelYetenekSinavSistemi.Tests.Validation.Http;

namespace OzelYetenekSinavSistemi.Tests.Validation.Admin;

public sealed class ValidationAdminHttpTests(ValidationAdminFixture fixture) : ValidationAdminTestBase(fixture)
{
    [Fact]
    public void Fixture_UsesIsolatedTestDatabasePrefix()
    {
        var connectionString = Fixture.Factory.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("DefaultConnection")!;
        TestDatabaseSafetyGuard.EnsureIsolatedTestDatabase(connectionString);
    }

    [Fact]
    public async Task StaffCreate_Get_IncludesHumanNameClientValidation()
    {
        var client = Fixture.SuperAdminClient;

        var (response, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/UserManagement/Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("input-text-validation.js", html, StringComparison.Ordinal);
        Assert.Contains("FirstName", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input-text-validation.js", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"FirstName\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaffCreatePost_InvalidFirstName_ReturnsViewWithoutCreatingUser()
    {
        var client = Fixture.SuperAdminClient;
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/UserManagement/Create");

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["TcNo"] = "52345678903",
            ["Email"] = "invalid-staff@test.local",
            ["FirstName"] = "--------",
            ["LastName"] = "Yılmaz",
            ["RoleId"] = "22222222-2222-2222-2222-222222222222",
            ["Password"] = "Passw0rd!Aa",
            ["ConfirmPassword"] = "Passw0rd!Aa",
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/UserManagement/Create", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("harf kullanarak", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Personel hesabı oluşturuldu", body, StringComparison.Ordinal);

        using var connection = await OpenConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Users WHERE Email = @Email",
            new { Email = "invalid-staff@test.local" });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task StaffCreatePost_ValidUnicodeFirstName_DoesNotFailNameValidation()
    {
        var client = Fixture.SuperAdminClient;
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/UserManagement/Create");

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["TcNo"] = "52345678904",
            ["Email"] = "unicode-staff@test.local",
            ["FirstName"] = "Jean-Pierre",
            ["LastName"] = "O\u2019Connor",
            ["RoleId"] = "22222222-2222-2222-2222-222222222222",
            ["Password"] = "Passw0rd!Aa",
            ["ConfirmPassword"] = "Passw0rd!Aa",
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/UserManagement/Create", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("en az bir harf içermeli", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExamPeriodCreate_Get_IncludesMeaningfulTitleClientValidation()
    {
        var client = Fixture.SuperAdminClient;

        var (_, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/ExamPeriod/Create");

        Assert.Contains("data-val-meaningfultitle", html, StringComparison.Ordinal);
        Assert.Contains("data-valmsg-for=\"Title\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExamPeriodCreatePost_InvalidTitle_ReturnsViewWithoutPersistence()
    {
        var client = Fixture.SuperAdminClient;
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/ExamPeriod/Create");

        var before = await CountExamPeriodsAsync();

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["Title"] = "--------",
            ["Description"] = "",
            ["StartDate"] = DateTime.Today.ToString("yyyy-MM-ddTHH:mm"),
            ["EndDate"] = DateTime.Today.AddDays(10).ToString("yyyy-MM-ddTHH:mm"),
            ["MaxPreferences"] = "2",
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/ExamPeriod/Create", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decoded = System.Net.WebUtility.HtmlDecode(body);
        Assert.Contains("anlamlı bir başlık", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountExamPeriodsAsync());
    }

    [Fact]
    public async Task PreferenceManage_Get_IncludesMeaningfulTitleClientValidation()
    {
        var client = Fixture.SuperAdminClient;

        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        Assert.Contains("data-val-meaningfultitle", html, StringComparison.Ordinal);
        Assert.Contains("name=\"PreferenceName\"", html, StringComparison.Ordinal);
        Assert.Contains("input-text-validation.js", html, StringComparison.Ordinal);
        Assert.Contains("Yeni Tercih Seçeneği Ekle", html, StringComparison.Ordinal);
        Assert.Contains("Tüm Tercih Seçenekleri", html, StringComparison.Ordinal);
        Assert.Contains("js-datatable", html, StringComparison.Ordinal);
        Assert.Contains("Tabloda ara", html, StringComparison.Ordinal);
        Assert.Contains("data-oys-server-side=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("/ExamPreferenceOption/Data", html, StringComparison.Ordinal);
        Assert.DoesNotContain("@option.PreferenceName", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreferenceAddPost_InvalidPreferenceName_DoesNotInsertOption()
    {
        var client = Fixture.SuperAdminClient;
        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        var before = await CountPreferenceOptionsAsync();

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ExamPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D"),
            ["PreferenceName"] = "--------",
            ["DisplayOrder"] = "99",
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/ExamPreferenceOption/Add", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await CountPreferenceOptionsAsync());
        Assert.Contains("Tüm Tercih Seçenekleri", body, StringComparison.Ordinal);
        Assert.Contains("Yeni Tercih Seçeneği Ekle", body, StringComparison.Ordinal);
        var decoded = System.Net.WebUtility.HtmlDecode(body);
        Assert.Contains("anlamlı bir başlık", decoded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreferenceAddPost_ValidFormFields_InsertsOptionForExamPeriod()
    {
        // Add formu PreferenceOptionFormViewModel alan adlarıyla post edilmeli; aksi halde ExamPeriodId Empty kalır.
        var client = Fixture.SuperAdminClient;
        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        var before = await CountPreferenceOptionsAsync();
        var uniqueName = $"HTTP Tercih {Guid.NewGuid():N}"[..28];
        var displayOrder = 800 + Random.Shared.Next(1, 99);

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ExamPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D"),
            ["PreferenceName"] = uniqueName,
            ["DisplayOrder"] = displayOrder.ToString(),
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/ExamPreferenceOption/Add", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 1, await CountPreferenceOptionsAsync());
        Assert.Contains("data-success=", body, StringComparison.Ordinal);
        Assert.Contains("data-oys-server-side=\"true\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sınav dönemi bulunamadı.", body, StringComparison.Ordinal);

        var dataJson = await client.GetStringAsync(
            $"/ExamPreferenceOption/Data?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}&draw=1&start=0&length=100&search[value]={Uri.EscapeDataString(uniqueName)}");
        Assert.Contains(uniqueName, dataJson, StringComparison.Ordinal);
        Assert.Contains("\"isActive\":true", dataJson, StringComparison.OrdinalIgnoreCase);

        using var connection = await OpenConnectionAsync();
        var isActive = await connection.ExecuteScalarAsync<bool>(
            "SELECT IsActive FROM dbo.ExamPreferenceOptions WHERE PreferenceName = @Name AND ExamPeriodId = @ExamPeriodId",
            new
            {
                Name = uniqueName,
                ExamPeriodId = ValidationAdminFixture.TestExamPeriodId
            });
        Assert.True(isActive);
    }

    [Fact]
    public async Task PreferenceAddPost_InactiveCheckbox_PersistsPassiveBadge()
    {
        var client = Fixture.SuperAdminClient;
        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        var uniqueName = $"Pasif Tercih {Guid.NewGuid():N}"[..28];
        var displayOrder = 650 + Random.Shared.Next(1, 99);

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ExamPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D"),
            ["PreferenceName"] = uniqueName,
            ["DisplayOrder"] = displayOrder.ToString(),
            ["IsActive"] = "false"
        });

        var response = await client.PostAsync("/ExamPreferenceOption/Add", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-oys-server-side=\"true\"", body, StringComparison.Ordinal);

        var dataJson = await client.GetStringAsync(
            $"/ExamPreferenceOption/Data?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}&draw=1&start=0&length=100&search[value]={Uri.EscapeDataString(uniqueName)}");
        Assert.Contains(uniqueName, dataJson, StringComparison.Ordinal);
        Assert.Contains("\"isActive\":false", dataJson, StringComparison.OrdinalIgnoreCase);

        using var connection = await OpenConnectionAsync();
        var isActive = await connection.ExecuteScalarAsync<bool>(
            "SELECT IsActive FROM dbo.ExamPreferenceOptions WHERE PreferenceName = @Name AND ExamPeriodId = @ExamPeriodId",
            new
            {
                Name = uniqueName,
                ExamPeriodId = ValidationAdminFixture.TestExamPeriodId
            });
        Assert.False(isActive);
    }

    [Fact]
    public async Task PreferenceDelete_Get_IsNotAllowed()
    {
        var response = await Fixture.SuperAdminClient.GetAsync(
            $"/ExamPreferenceOption/Delete?optionId={Guid.NewGuid():D}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task PreferenceAddPost_RedirectsWithPrg_WhenUsingNoAutoRedirectClient()
    {
        var client = ValidationAdminHttpTestSupport.CreateClientForRole(Fixture.Factory, "SuperAdmin", allowAutoRedirect: false);
        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        var uniqueName = $"PRG Tercih {Guid.NewGuid():N}"[..28];
        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ExamPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D"),
            ["PreferenceName"] = uniqueName,
            ["DisplayOrder"] = (900 + Random.Shared.Next(1, 50)).ToString(),
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/ExamPreferenceOption/Add", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/ExamPreferenceOption/Manage",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ValidationAdminFixture.TestExamPeriodId.ToString("D"),
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreferenceAddPost_CheckedActiveWithCheckboxValueOrder_PersistsActive()
    {
        // CheckboxTagHelper: önce true (checkbox), sonra false (hidden). Tersi false bağlar.
        var client = Fixture.SuperAdminClient;
        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        var uniqueName = $"Aktif Tercih {Guid.NewGuid():N}"[..28];
        var displayOrder = 700 + Random.Shared.Next(1, 99);

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new[]
        {
            new KeyValuePair<string, string>("ExamPeriodId", ValidationAdminFixture.TestExamPeriodId.ToString("D")),
            new KeyValuePair<string, string>("PreferenceName", uniqueName),
            new KeyValuePair<string, string>("DisplayOrder", displayOrder.ToString()),
            new KeyValuePair<string, string>("IsActive", "true"),
            new KeyValuePair<string, string>("IsActive", "false")
        });

        var response = await client.PostAsync("/ExamPreferenceOption/Add", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var connection = await OpenConnectionAsync();
        var isActive = await connection.ExecuteScalarAsync<bool>(
            "SELECT IsActive FROM dbo.ExamPreferenceOptions WHERE PreferenceName = @Name AND ExamPeriodId = @ExamPeriodId",
            new
            {
                Name = uniqueName,
                ExamPeriodId = ValidationAdminFixture.TestExamPeriodId
            });
        Assert.True(isActive);
    }

    [Fact]
    public async Task ExamResultEvaluate_Get_IncludesMeaningfulTextClientValidation()
    {
        var client = Fixture.SuperAdminClient;

        var url = $"/ExamResult/Evaluate?id={ValidationAdminFixture.TestApplicationId:D}";
        var (response, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-val-meaningfultext", html, StringComparison.Ordinal);
        Assert.Contains("data-val-meaningfultext-optional", html, StringComparison.Ordinal);
        Assert.Contains("data-val-meaningfultext-multiline", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExamResultSavePost_InvalidAdminDescription_ReturnsViewWithoutPersistence()
    {
        var client = ValidationAdminHttpTestSupport.CreateClientForRole(Fixture.Factory, "SuperAdmin", allowAutoRedirect: false);
        var url = $"/ExamResult/Evaluate?id={ValidationAdminFixture.TestApplicationId:D}";
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ApplicationId"] = ValidationAdminFixture.TestApplicationId.ToString("D"),
            ["AttendanceStatus"] = "1",
            ["ExamScore"] = "75",
            ["AdminDescription"] = "--------",
            ["examPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D")
        });

        var response = await client.PostAsync("/ExamResult/Save", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect,
            $"Unexpected status {(int)response.StatusCode}.");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Contains("anlaml", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AdminDescription", body, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("/ExamResult/Evaluate", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        }

        using var connection = await OpenConnectionAsync();
        var storedDescription = await connection.ExecuteScalarAsync<string>(
            "SELECT AdminDescription FROM dbo.CandidateExamResults WHERE ApplicationId = @ApplicationId",
            new { ApplicationId = ValidationAdminFixture.TestApplicationId });
        Assert.True(string.IsNullOrEmpty(storedDescription) || storedDescription != "--------");
    }

    [Fact]
    public async Task ExamResultSavePost_EmptyAdminDescription_IsAcceptedByValidation()
    {
        var client = Fixture.SuperAdminClient;
        var url = $"/ExamResult/Evaluate?id={ValidationAdminFixture.TestApplicationId:D}";
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ApplicationId"] = ValidationAdminFixture.TestApplicationId.ToString("D"),
            ["AttendanceStatus"] = "1",
            ["AdminDescription"] = "",
            ["examPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D")
        });

        var response = await client.PostAsync("/ExamResult/Save", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("anlamlı metin", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SystemSetting_Get_IncludesRangeClientValidation()
    {
        var client = Fixture.SuperAdminClient;

        var (_, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/SystemSetting");

        Assert.Contains("data-val-range", html, StringComparison.Ordinal);
        Assert.Contains("data-val-range-max=\"10240\"", html, StringComparison.Ordinal);
        Assert.Contains("data-val-range-min=\"100\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemSettingPost_UnknownKey_IsRejectedWithoutInsert()
    {
        var client = Fixture.SuperAdminClient;
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/SystemSetting");

        var before = await CountSystemSettingsAsync();

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["SettingKey"] = "UnknownSettingKey",
            ["SettingValue"] = "100"
        });

        var response = await client.PostAsync("/SystemSetting/Update", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await CountSystemSettingsAsync());
    }

    [Fact]
    public async Task SystemSettingPost_OutOfRangeValue_IsRejected()
    {
        var client = Fixture.SuperAdminClient;
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/SystemSetting");

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["SettingKey"] = SystemSettingValidator.MaxPhotoSizeKbKey,
            ["SettingValue"] = "99"
        });

        var response = await client.PostAsync("/SystemSetting/Update", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var connection = await OpenConnectionAsync();
        var value = await connection.ExecuteScalarAsync<string>(
            "SELECT SettingValue FROM dbo.SystemSettings WHERE SettingKey = @Key",
            new { Key = SystemSettingValidator.MaxPhotoSizeKbKey });
        Assert.NotEqual("99", value);
    }

    [Fact]
    public async Task Profile_Get_IncludesHumanNameClientValidation()
    {
        var client = Fixture.CandidateClient;

        var (_, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/CandidateProfile");

        Assert.Contains("input-text-validation.js", html, StringComparison.Ordinal);
        Assert.Contains("FirstName", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-valmsg-for=\"FirstName\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfilePost_InvalidFirstName_ReturnsViewWithoutUpdatingName()
    {
        var client = Fixture.CandidateClient;
        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/CandidateProfile");

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["FirstName"] = "--------",
            ["LastName"] = "Aday",
            ["Email"] = "candidate@test.local",
            ["Phone"] = "5321234567",
            ["HasDisability"] = "false"
        });

        var response = await client.PostAsync("/CandidateProfile", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("harf kullanarak", body, StringComparison.OrdinalIgnoreCase);

        using var connection = await OpenConnectionAsync();
        var firstName = await connection.ExecuteScalarAsync<string>(
            "SELECT FirstName FROM dbo.Users WHERE Id = @Id",
            new { Id = ValidationAdminFixture.CandidateUserId });
        Assert.Equal("Test", firstName);
    }

    [Fact]
    public async Task Profile_Get_YgsScore_IsEmptyWithoutDashDashAndHasTurkishNumberMessage()
    {
        var client = Fixture.CandidateClient;
        var (_, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/CandidateProfile");
        var decoded = WebUtility.HtmlDecode(html);

        Assert.Contains("name=\"YgsScore\"", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-val-number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("YGS puanını sayı olarak giriniz.", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The field", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(
            """(?is)<input[^>]*name\s*=\s*["']YgsScore["'][^>]*value\s*=\s*["']--["']""",
            decoded);
        Assert.DoesNotMatch(
            """(?is)<input[^>]*value\s*=\s*["']--["'][^>]*name\s*=\s*["']YgsScore["']""",
            decoded);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("--")]
    public async Task ProfilePost_NonNumericYgsScore_ReturnsTurkishNumberMessage(string invalidScore)
    {
        var client = Fixture.CandidateClient;
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/CandidateProfile");

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["FirstName"] = "Test",
            ["LastName"] = "Aday",
            ["Email"] = "candidate@test.local",
            ["Phone"] = "5321234567",
            ["HasDisability"] = "false",
            ["YgsScore"] = invalidScore
        });

        var response = await client.PostAsync("/CandidateProfile", form);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("YGS puanını sayı olarak giriniz.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The field", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The value", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfilePost_EmptyOptionalYgsScore_DoesNotReturnNumberBindingError()
    {
        var client = Fixture.CandidateClient;
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/CandidateProfile");

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["FirstName"] = "Test",
            ["LastName"] = "Aday",
            ["Email"] = "candidate@test.local",
            ["Phone"] = "5321234567",
            ["HasDisability"] = "false",
            ["YgsScore"] = ""
        });

        var response = await client.PostAsync("/CandidateProfile", form);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.DoesNotMatch(
            """(?is)field-validation-error[^>]*data-valmsg-for\s*=\s*["']YgsScore["']""",
            body);
        Assert.DoesNotMatch(
            """(?is)data-valmsg-for\s*=\s*["']YgsScore["'][^>]*field-validation-error""",
            body);
        Assert.DoesNotContain("must be a number", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The field", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("YGS puanınızı giriniz.", body, StringComparison.Ordinal);
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task ExamPeriodCreatePost_NonNumericMaxPreferences_ReturnsTurkishNumberMessage()
    {
        var client = Fixture.SuperAdminClient;
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/ExamPeriod/Create");
        var before = await CountExamPeriodsAsync();

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["Title"] = "Sayısal Doğrulama Dönemi",
            ["Description"] = "",
            ["StartDate"] = DateTime.Today.ToString("yyyy-MM-ddTHH:mm"),
            ["EndDate"] = DateTime.Today.AddDays(10).ToString("yyyy-MM-ddTHH:mm"),
            ["MaxPreferences"] = "abc",
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/ExamPeriod/Create", form);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Maksimum tercih sayısını sayı olarak giriniz.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The value", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountExamPeriodsAsync());
    }

    [Fact]
    public async Task PreferenceAddPost_NonNumericDisplayOrder_ReturnsTurkishNumberMessage()
    {
        var client = Fixture.SuperAdminClient;
        var url = $"/ExamPreferenceOption/Manage?examPeriodId={ValidationAdminFixture.TestExamPeriodId:D}";
        var (_, _, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, url);
        var before = await CountPreferenceOptionsAsync();

        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["ExamPeriodId"] = ValidationAdminFixture.TestExamPeriodId.ToString("D"),
            ["PreferenceName"] = "Sayısal Doğrulama Tercihi",
            ["DisplayOrder"] = "abc",
            ["IsActive"] = "true"
        });

        var response = await client.PostAsync("/ExamPreferenceOption/Add", form);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Görünüm sırasını sayı olarak giriniz.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The value", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await CountPreferenceOptionsAsync());
    }

    [Fact]
    public async Task ExamPeriodCreate_Get_IncludesTurkishNumberClientMessageForMaxPreferences()
    {
        var client = Fixture.SuperAdminClient;
        var (_, html, _) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/ExamPeriod/Create");
        var decoded = WebUtility.HtmlDecode(html);

        Assert.Contains("name=\"MaxPreferences\"", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-val-number", decoded, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maksimum tercih sayısını sayı olarak giriniz.", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("must be a number", decoded, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<System.Data.IDbConnection> OpenConnectionAsync()
    {
        var factory = Fixture.Factory.Services.GetRequiredService<IDbConnectionFactory>();
        return await factory.CreateOpenConnectionAsync();
    }

    private async Task<int> CountExamPeriodsAsync()
    {
        using var connection = await OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.ExamPeriods");
    }

    private async Task<int> CountPreferenceOptionsAsync()
    {
        using var connection = await OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.ExamPreferenceOptions");
    }

    private async Task<int> CountSystemSettingsAsync()
    {
        using var connection = await OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.SystemSettings");
    }
}

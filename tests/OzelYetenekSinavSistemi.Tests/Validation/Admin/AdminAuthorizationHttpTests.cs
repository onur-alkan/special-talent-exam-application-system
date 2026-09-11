using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OzelYetenekSinavSistemi.Tests.Validation.Admin;

public sealed class AdminAuthorizationHttpTests(ValidationAdminFixture fixture) : ValidationAdminTestBase(fixture)
{
    [Fact]
    public async Task Candidate_CannotAccessStaffCreate()
    {
        var response = await Fixture.CandidateClient.GetAsync("/UserManagement/Create");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Personel Oluştur", body, StringComparison.Ordinal);
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect or HttpStatusCode.Found,
            $"Unexpected status {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task Candidate_CannotAccessExamPeriodCreate()
    {
        var response = await Fixture.CandidateClient.GetAsync("/ExamPeriod/Create");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Yeni Sınav Dönemi", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplicationManager_CanAccessAssignedExamPeriodList()
    {
        var response = await Fixture.ApplicationManagerClient.GetAsync("/ExamPeriod/Index");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationManager_CannotAccessSystemSettings()
    {
        var response = await Fixture.ApplicationManagerClient.GetAsync("/SystemSetting/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("MaxPhotoSizeKb", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuperAdmin_CanAccessSystemSettings()
    {
        var response = await Fixture.SuperAdminClient.GetAsync("/SystemSetting/Index");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("MaxPhotoSizeKb", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_CannotPostStaffCreate()
    {
        var client = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var (_, html, token) = await ValidationAdminHttpTestSupport.GetFormAsync(client, "/Account/Login");
        using var form = ValidationAdminHttpTestSupport.BuildForm(token, new Dictionary<string, string>
        {
            ["TcNo"] = "52345678903",
            ["Email"] = "anon-staff@test.local",
            ["FirstName"] = "Anon",
            ["LastName"] = "Deneme",
            ["RoleId"] = "22222222-2222-2222-2222-222222222222",
            ["Password"] = "Passw0rd!Aa",
            ["ConfirmPassword"] = "Passw0rd!Aa"
        });

        var response = await client.PostAsync("/UserManagement/Create", form);
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Unexpected status {(int)response.StatusCode}.");
    }
}

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using OzelYetenekSinavSistemi.Tests.Validation.Http;

namespace OzelYetenekSinavSistemi.Tests.Validation.Admin;

internal static class ValidationAdminHttpTestSupport
{
    internal static HttpClient CreateClientForRole(WebApplicationFactory<Program> factory, string role, bool allowAutoRedirect = true)
    {
        var client = ValidationHttpTestSupport.CreateClient(factory, allowAutoRedirect);
        client.DefaultRequestHeaders.Add(ValidationAdminTestAuthHandler.RoleHeader, role);
        return client;
    }

    internal static string? ExtractAntiforgeryToken(string html) =>
        ValidationHttpTestSupport.ExtractAntiforgeryToken(html);

    internal static async Task<(HttpResponseMessage Response, string Body, string Token)> GetFormAsync(
        HttpClient client,
        string url) => await ValidationHttpTestSupport.GetFormAsync(client, url).ConfigureAwait(false);

    internal static FormUrlEncodedContent BuildForm(string token, IEnumerable<KeyValuePair<string, string>> fields) =>
        ValidationHttpTestSupport.BuildForm(token, fields);
}

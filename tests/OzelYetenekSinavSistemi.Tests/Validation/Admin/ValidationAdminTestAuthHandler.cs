using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Tests.Validation.Admin;

internal sealed class ValidationAdminTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ValidationAdminTest";
    internal const string RoleHeader = "X-Test-Role";

    public ValidationAdminTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var role = roleValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role))
            return Task.FromResult(AuthenticateResult.NoResult());

        var (userId, roleName) = role switch
        {
            "SuperAdmin" => (ValidationAdminFixture.SuperAdminUserId, DomainConstants.RoleNames.SuperAdmin),
            "ApplicationManager" => (ValidationAdminFixture.ApplicationManagerUserId, DomainConstants.RoleNames.ApplicationManager),
            "Candidate" => (ValidationAdminFixture.CandidateUserId, DomainConstants.RoleNames.Candidate),
            _ => (Guid.Empty, string.Empty)
        };

        if (userId == Guid.Empty)
            return Task.FromResult(AuthenticateResult.Fail("Unknown test role."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new Claim(ClaimTypes.Role, roleName),
            new Claim(DomainConstants.ClaimTypesCustom.UserId, userId.ToString("D")),
            new Claim(DomainConstants.ClaimTypesCustom.SecurityStamp, Guid.Empty.ToString("D")),
            new Claim("MustChangePassword", "false")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

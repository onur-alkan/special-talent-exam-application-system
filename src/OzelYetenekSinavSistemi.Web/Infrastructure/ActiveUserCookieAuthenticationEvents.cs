using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OzelYetenekSinavSistemi.Application.Interfaces.Repositories;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

/// <summary>
/// Her istekte cookie claim'lerini veritabanı durumu ile doğrular (fail-closed).
/// </summary>
public sealed class ActiveUserCookieAuthenticationEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
            return;

        try
        {
            var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            {
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }

            var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var state = await userRepository.GetAuthenticationStateAsync(userId, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);

            if (state is null || !state.IsActive)
            {
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }

            var expectedRole = DomainConstants.GetRoleName(state.RoleId);
            var roleClaim = principal.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrEmpty(expectedRole)
                || !string.Equals(expectedRole, roleClaim, StringComparison.Ordinal))
            {
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }

            var stampClaim = principal.FindFirstValue(DomainConstants.ClaimTypesCustom.SecurityStamp);
            if (!Guid.TryParse(stampClaim, out var stamp)
                || stamp != state.SecurityStamp)
            {
                await RejectAsync(context).ConfigureAwait(false);
                return;
            }

            var mustChangeClaim = principal.FindFirstValue("MustChangePassword");
            var expectedMustChange = state.MustChangePassword ? "true" : "false";
            if (!string.Equals(mustChangeClaim, expectedMustChange, StringComparison.OrdinalIgnoreCase))
            {
                await RejectAsync(context).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ActiveUserCookieAuthenticationEvents>>();
            logger?.LogError(ex, "Cookie oturum doğrulaması başarısız; oturum reddedildi.");
            await RejectAsync(context).ConfigureAwait(false);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        try
        {
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
        }
        catch
        {
            // SignOut DI eksik olsa bile principal reddedilmiş olur (fail-closed).
        }
    }
}

using System.Security.Claims;
using OzelYetenekSinavSistemi.Domain.Common;

namespace OzelYetenekSinavSistemi.Web.Infrastructure;

public static class CurrentUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static string GetRole(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public static string GetFullName(this ClaimsPrincipal principal)
        => principal.FindFirstValue(DomainConstants.ClaimTypesCustom.FullName) ?? string.Empty;

    public static bool MustChangePassword(this ClaimsPrincipal principal)
        => string.Equals(principal.FindFirstValue("MustChangePassword"), "true", StringComparison.OrdinalIgnoreCase);
}

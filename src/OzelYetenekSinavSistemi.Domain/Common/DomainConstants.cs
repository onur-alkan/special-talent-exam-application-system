namespace OzelYetenekSinavSistemi.Domain.Common;

/// <summary>
/// Domain genelinde kullanılan sabit değerler ve tekrar kullanılabilir seed Guid'leri.
/// </summary>
public static class DomainConstants
{
    /// <summary>
    /// Rol tablosu için sabit ve tekrar kullanılabilir Guid değerleri.
    /// Bu değerler SQL scripti ile birebir uyumludur.
    /// </summary>
    public static class RoleIds
    {
        public static readonly Guid SuperAdmin = new("11111111-1111-1111-1111-111111111111");
        public static readonly Guid ApplicationManager = new("22222222-2222-2222-2222-222222222222");
        public static readonly Guid Candidate = new("33333333-3333-3333-3333-333333333333");
    }

    public static class RoleNames
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string ApplicationManager = "ApplicationManager";
        public const string Candidate = "Candidate";
    }

    /// <summary>
    /// Cookie / claim anahtarları.
    /// </summary>
    public static class ClaimTypesCustom
    {
        public const string UserId = "app:user_id";
        public const string FullName = "app:full_name";
        public const string SecurityStamp = "SecurityStamp";
    }

    /// <summary>Personel (SuperAdmin / ApplicationManager) rol Id whitelist.</summary>
    public static bool IsStaffRoleId(Guid roleId) =>
        roleId == RoleIds.SuperAdmin || roleId == RoleIds.ApplicationManager;

    /// <summary>Sabit RoleId değerinden rol adını döner.</summary>
    public static string? GetRoleName(Guid roleId)
    {
        if (roleId == RoleIds.SuperAdmin) return RoleNames.SuperAdmin;
        if (roleId == RoleIds.ApplicationManager) return RoleNames.ApplicationManager;
        if (roleId == RoleIds.Candidate) return RoleNames.Candidate;
        return null;
    }

    public const int TcNoLength = 11;
    public const int IdentityNumberMaxLength = 32;
    public const int PassportNumberMinLength = 5;
    public const int PassportNumberMaxLength = 20;
    public const int CountryCodeLength = 2;
    public const decimal MinYgsScore = 0m;
    public const decimal MaxYgsScore = 560m;
    public const decimal MinExamScore = 0m;
    public const decimal MaxExamScore = 100m;
}

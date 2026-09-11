using OzelYetenekSinavSistemi.Domain.Enums;

namespace OzelYetenekSinavSistemi.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string? TcNo { get; set; }
    public IdentityDocumentType? IdentityDocumentType { get; set; }
    public string? IdentityNumber { get; set; }
    public string? NormalizedIdentityNumber { get; set; }
    public string? NationalityCountryCode { get; set; }
    public string? IssuingCountryCode { get; set; }
    public DateOnly? PassportExpiryDate { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>
    /// Legacy alan: yalnızca yıl bilgisi. Yeni kayıtlarda <see cref="BirthDate"/> ile birlikte
    /// yıl türetilerek doldurulur; ileride kaldırılabilecek geçiş dönemi sütunudur.
    /// </summary>
    public short? BirthYear { get; set; }
    /// <summary>Aday doğum tarihi (gün/ay/yıl). Saat tutulmaz (SQL DATE).</summary>
    public DateOnly? BirthDate { get; set; }
    public string? Nationality { get; set; }
    public string? HighSchool { get; set; }
    public string? DepartmentField { get; set; }
    public decimal? YgsScore { get; set; }
    public Guid? YgsYearId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool HasDisability { get; set; }
    public string? DisabilityDetails { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public bool MustChangePassword { get; set; }
    public Guid SecurityStamp { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

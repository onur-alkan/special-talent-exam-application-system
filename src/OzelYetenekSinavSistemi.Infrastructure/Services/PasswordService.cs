using Microsoft.AspNetCore.Identity;
using OzelYetenekSinavSistemi.Application.Interfaces.Services;

namespace OzelYetenekSinavSistemi.Infrastructure.Services;

/// <summary>
/// ASP.NET Core PasswordHasher tabanlı parola servisi.
/// </summary>
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object DummyUser = new();

    public string Hash(string password) => _hasher.HashPassword(DummyUser, password);

    public bool Verify(string passwordHash, string providedPassword, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(passwordHash))
            return false;

        var result = _hasher.VerifyHashedPassword(DummyUser, passwordHash, providedPassword);
        needsRehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }
}

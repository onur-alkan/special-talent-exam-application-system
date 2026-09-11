namespace OzelYetenekSinavSistemi.Application.Interfaces.Services;

/// <summary>
/// ASP.NET Core PasswordHasher tabanlı parola hash'leme / doğrulama.
/// </summary>
public interface IPasswordService
{
    string Hash(string password);

    /// <summary>Parolayı doğrular; gerekiyorsa yeniden hash gerekliliğini de bildirir.</summary>
    bool Verify(string passwordHash, string providedPassword, out bool needsRehash);
}

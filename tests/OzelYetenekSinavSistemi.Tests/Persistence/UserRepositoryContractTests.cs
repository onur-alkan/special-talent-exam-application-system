using OzelYetenekSinavSistemi.Infrastructure.Persistence.Repositories;

namespace OzelYetenekSinavSistemi.Tests.Persistence;

public sealed class UserRepositoryContractTests
{
    [Fact]
    public void SelectColumns_IncludesIdentityDocumentFields()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "UserRepository.cs");

        Assert.Contains("IdentityDocumentType", source, StringComparison.Ordinal);
        Assert.Contains("IdentityNumber", source, StringComparison.Ordinal);
        Assert.Contains("NormalizedIdentityNumber", source, StringComparison.Ordinal);
        Assert.Contains("NationalityCountryCode", source, StringComparison.Ordinal);
        Assert.Contains("IssuingCountryCode", source, StringComparison.Ordinal);
        Assert.Contains("PassportExpiryDate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GetByEmailOrTurkishIdentity_UsesParameterizedQueries_AndDoesNotSearchForeignOrPassportForLogin()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "UserRepository.cs");

        Assert.Contains("GetByEmailOrTurkishIdentityAsync", source, StringComparison.Ordinal);
        Assert.Contains("LoginIdentifierHelper.LooksLikeEmail", source, StringComparison.Ordinal);
        Assert.Contains("GetByTurkishIdentityNumberAsync", source, StringComparison.Ordinal);
        Assert.Contains("IdentityDocumentType = @TurkishType AND NormalizedIdentityNumber = @TcNo", source, StringComparison.Ordinal);
        Assert.Contains("@Email", source, StringComparison.Ordinal);
        Assert.Contains("@TcNo", source, StringComparison.Ordinal);
        Assert.Contains("@TurkishType", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Format", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StringBuilder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityExistsAsync_PassportQuery_IncludesIssuingCountryCode()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "UserRepository.cs");

        Assert.Contains("IdentityExistsAsync", source, StringComparison.Ordinal);
        Assert.Contains("IssuingCountryCode = @Issuing", source, StringComparison.Ordinal);
        Assert.Contains("@Issuing", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffInsert_WritesDualWriteIdentityFields()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "UserRepository.cs");

        Assert.Contains("IdentityDocumentType, IdentityNumber, NormalizedIdentityNumber, NationalityCountryCode", source, StringComparison.Ordinal);
        Assert.Contains("@TurkishIdentityType, @TcNo, @TcNo, 'TR'", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericRepository_AddAsync_UsesParameterizedInsert_ForUserEntity()
    {
        var source = ReadProjectFile(
            "src", "OzelYetenekSinavSistemi.Infrastructure", "Persistence", "Repositories", "GenericRepository.cs");

        Assert.Contains("INSERT INTO {TableName} ({columnList}) OUTPUT INSERTED.Id VALUES ({valueList})", source, StringComparison.Ordinal);
        Assert.Contains("@\" + c", source, StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}

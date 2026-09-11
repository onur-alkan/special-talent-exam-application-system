namespace OzelYetenekSinavSistemi.Tests.Persistence;

public sealed class UserIdentityDocumentMigrationTests
{
    [Fact]
    public void Migration_AddsNullableIdentityColumns_WithGuards()
    {
        var migration = ReadProjectFile("database", "migrations", "004_AddUserIdentityDocument.sql");

        Assert.Contains("COL_LENGTH(N'dbo.Users', N'IdentityDocumentType') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD IdentityDocumentType TINYINT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Users', N'IdentityNumber') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD IdentityNumber NVARCHAR(32) NULL", migration, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Users', N'NormalizedIdentityNumber') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD NormalizedIdentityNumber VARCHAR(32) NULL", migration, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Users', N'NationalityCountryCode') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD NationalityCountryCode CHAR(2) NULL", migration, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Users', N'IssuingCountryCode') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD IssuingCountryCode CHAR(2) NULL", migration, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH(N'dbo.Users', N'PassportExpiryDate') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD PassportExpiryDate DATE NULL", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_BackfillsExistingTcNoAsTurkishIdentity()
    {
        var migration = ReadProjectFile("database", "migrations", "004_AddUserIdentityDocument.sql");

        Assert.Contains("IdentityDocumentType = 1", migration, StringComparison.Ordinal);
        Assert.Contains("IdentityNumber = TcNo", migration, StringComparison.Ordinal);
        Assert.Contains("NormalizedIdentityNumber = TcNo", migration, StringComparison.Ordinal);
        Assert.Contains("NationalityCountryCode = ''TR''", migration, StringComparison.Ordinal);
        Assert.Contains("IdentityDocumentType IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("LTRIM(RTRIM(TcNo)) <> ''", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_IsTransactionalAndIdempotent()
    {
        var migration = ReadProjectFile("database", "migrations", "004_AddUserIdentityDocument.sql");

        Assert.Contains("SET XACT_ABORT ON", migration, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRY", migration, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION", migration, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION", migration, StringComparison.Ordinal);
        Assert.Contains("BEGIN CATCH", migration, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK TRANSACTION", migration, StringComparison.Ordinal);
        Assert.Contains("THROW", migration, StringComparison.Ordinal);
        Assert.Contains("IF COL_LENGTH", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_DoesNotDropTcNoOrExistingUniqueConstraint()
    {
        var migration = ReadProjectFile("database", "migrations", "004_AddUserIdentityDocument.sql");

        Assert.DoesNotContain("DROP CONSTRAINT", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UQ_Users_TcNo", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER COLUMN", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE UNIQUE INDEX", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" SET NOT NULL", migration, StringComparison.OrdinalIgnoreCase);
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

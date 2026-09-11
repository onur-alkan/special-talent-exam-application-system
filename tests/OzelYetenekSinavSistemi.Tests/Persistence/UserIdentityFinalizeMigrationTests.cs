namespace OzelYetenekSinavSistemi.Tests.Persistence;

public sealed class UserIdentityFinalizeMigrationTests
{
    [Fact]
    public void Migration_Requires004Columns()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("COL_LENGTH(N'dbo.Users', N'IdentityDocumentType') IS NULL", migration, StringComparison.Ordinal);
        Assert.Contains("Migration 004_AddUserIdentityDocument.sql must be applied before 005.", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_BackfillsAndAuditsDuplicates()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("IdentityDocumentType = 1", migration, StringComparison.Ordinal);
        Assert.Contains("IdentityNumber = TcNo", migration, StringComparison.Ordinal);
        Assert.Contains("NormalizedIdentityNumber = TcNo", migration, StringComparison.Ordinal);
        Assert.Contains("NationalityCountryCode = 'TR'", migration, StringComparison.Ordinal);
        Assert.Contains("Duplicate NormalizedIdentityNumber", migration, StringComparison.Ordinal);
        Assert.Contains("Duplicate passport identity", migration, StringComparison.Ordinal);
        Assert.Contains("Duplicate TcNo values", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_TransitionsTcNoToNullableFilteredUnique()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("DROP CONSTRAINT UQ_Users_TcNo", migration, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN TcNo VARCHAR(11) NULL", migration, StringComparison.Ordinal);
        Assert.Contains("UQ_Users_TcNo_NotNull", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE TcNo IS NOT NULL", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_SetsRequiredIdentityColumnsAndChecks()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("ALTER COLUMN IdentityDocumentType TINYINT NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN IdentityNumber NVARCHAR(32) NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN NormalizedIdentityNumber VARCHAR(32) NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ALTER COLUMN NationalityCountryCode CHAR(2) NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("CK_Users_IdentityDocumentType", migration, StringComparison.Ordinal);
        Assert.Contains("IdentityDocumentType IN (1, 2, 3)", migration, StringComparison.Ordinal);
        Assert.Contains("CK_Users_IdentityDocumentConsistency", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_CreatesSeparateFilteredUniqueIndexes()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("UQ_Users_Identity_Turkish", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE IdentityDocumentType = 1", migration, StringComparison.Ordinal);
        Assert.Contains("UQ_Users_Identity_Foreign", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE IdentityDocumentType = 2", migration, StringComparison.Ordinal);
        Assert.Contains("UQ_Users_Identity_Passport", migration, StringComparison.Ordinal);
        Assert.Contains("IssuingCountryCode, NormalizedIdentityNumber", migration, StringComparison.Ordinal);
        Assert.Contains("WHERE IdentityDocumentType = 3", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_IsTransactionalIdempotentAndDoesNotDropLegacyColumns()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("SET XACT_ABORT ON", migration, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRY", migration, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION", migration, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION", migration, StringComparison.Ordinal);
        Assert.Contains("BEGIN CATCH", migration, StringComparison.Ordinal);
        Assert.Contains("ROLLBACK TRANSACTION", migration, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", migration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP COLUMN [Nationality]", migration, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_SetsRequiredSessionOptionsBeforeFilteredIndexes()
    {
        var migration = ReadProjectFile("database", "migrations", "005_FinalizeUserIdentityStorage.sql");

        Assert.Contains("SET QUOTED_IDENTIFIER ON", migration, StringComparison.Ordinal);
        Assert.Contains("SET ANSI_NULLS ON", migration, StringComparison.Ordinal);
        Assert.Contains("SET ANSI_PADDING ON", migration, StringComparison.Ordinal);
        Assert.Contains("SET ANSI_WARNINGS ON", migration, StringComparison.Ordinal);
        Assert.Contains("SET ARITHABORT ON", migration, StringComparison.Ordinal);
        Assert.Contains("SET CONCAT_NULL_YIELDS_NULL ON", migration, StringComparison.Ordinal);
        Assert.Contains("SET NUMERIC_ROUNDABORT OFF", migration, StringComparison.Ordinal);

        var firstIndex = migration.IndexOf("CREATE UNIQUE INDEX", StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, "Migration should create filtered unique indexes.");

        var quotedIdentifier = migration.IndexOf("SET QUOTED_IDENTIFIER ON", StringComparison.Ordinal);
        var ansiNulls = migration.IndexOf("SET ANSI_NULLS ON", StringComparison.Ordinal);
        var ansiPadding = migration.IndexOf("SET ANSI_PADDING ON", StringComparison.Ordinal);
        var ansiWarnings = migration.IndexOf("SET ANSI_WARNINGS ON", StringComparison.Ordinal);
        var arithAbort = migration.IndexOf("SET ARITHABORT ON", StringComparison.Ordinal);
        var concatNull = migration.IndexOf("SET CONCAT_NULL_YIELDS_NULL ON", StringComparison.Ordinal);
        var numericRoundAbort = migration.IndexOf("SET NUMERIC_ROUNDABORT OFF", StringComparison.Ordinal);

        Assert.True(quotedIdentifier >= 0 && quotedIdentifier < firstIndex);
        Assert.True(ansiNulls >= 0 && ansiNulls < firstIndex);
        Assert.True(ansiPadding >= 0 && ansiPadding < firstIndex);
        Assert.True(ansiWarnings >= 0 && ansiWarnings < firstIndex);
        Assert.True(arithAbort >= 0 && arithAbort < firstIndex);
        Assert.True(concatNull >= 0 && concatNull < firstIndex);
        Assert.True(numericRoundAbort >= 0 && numericRoundAbort < firstIndex);
    }
    [Fact]
    public void BaseSchema_CreatesFinalIdentityStorage_ForFreshInstall()
    {
        var schema = ReadProjectFile("database", "OzelYetenekSinavSistemi.sql");

        Assert.Contains(
            "TcNo                     VARCHAR(11)  NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "IdentityDocumentType     TINYINT      NOT NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "IdentityNumber           NVARCHAR(32) NOT NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "NormalizedIdentityNumber VARCHAR(32)  NOT NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "NationalityCountryCode   CHAR(2)      NOT NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "IssuingCountryCode       CHAR(2)      NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "PassportExpiryDate       DATE         NULL",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "CK_Users_IdentityDocumentType",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "CK_Users_IdentityDocumentConsistency",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "UQ_Users_TcNo_NotNull",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "UQ_Users_Identity_Turkish",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "UQ_Users_Identity_Foreign",
            schema,
            StringComparison.Ordinal);

        Assert.Contains(
            "UQ_Users_Identity_Passport",
            schema,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "CONSTRAINT UQ_Users_TcNo UNIQUE",
            schema,
            StringComparison.Ordinal);

        var firstFilteredIndex = schema.IndexOf(
            "CREATE UNIQUE INDEX UQ_Users_TcNo_NotNull",
            StringComparison.Ordinal);

        var sessionOptions = schema.IndexOf(
            "SET QUOTED_IDENTIFIER ON",
            StringComparison.Ordinal);

        Assert.True(
            sessionOptions >= 0 && sessionOptions < firstFilteredIndex,
            "Gerekli SQL oturum seçenekleri filtreli indekslerden önce ayarlanmalıdır.");
    }

    [Fact]
    public void Migration_DoesNotModify004File()
    {
        var migration004 = ReadProjectFile("database", "migrations", "004_AddUserIdentityDocument.sql");
        Assert.DoesNotContain("UQ_Users_TcNo_NotNull", migration004, StringComparison.Ordinal);
        Assert.DoesNotContain("CK_Users_IdentityDocumentConsistency", migration004, StringComparison.Ordinal);
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

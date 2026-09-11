SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
    BEGIN
        THROW 50000,
            N'Migration must run against the target application database, not a system database. Use sqlcmd -d TargetDatabase (or select the database in SSMS).',
            1;
    END;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    BEGIN
        THROW 50001, N'Prerequisite missing: dbo.Users. Apply the base schema script first.', 1;
    END;

    -- A. Ön koşul: 004 sütunları
    IF COL_LENGTH(N'dbo.Users', N'IdentityDocumentType') IS NULL
        THROW 50001, N'Migration 004_AddUserIdentityDocument.sql must be applied before 005.', 1;

    IF COL_LENGTH(N'dbo.Users', N'IdentityNumber') IS NULL
        THROW 50001, N'Migration 004_AddUserIdentityDocument.sql must be applied before 005.', 1;

    IF COL_LENGTH(N'dbo.Users', N'NormalizedIdentityNumber') IS NULL
        THROW 50001, N'Migration 004_AddUserIdentityDocument.sql must be applied before 005.', 1;

    IF COL_LENGTH(N'dbo.Users', N'NationalityCountryCode') IS NULL
        THROW 50001, N'Migration 004_AddUserIdentityDocument.sql must be applied before 005.', 1;

    IF COL_LENGTH(N'dbo.Users', N'IssuingCountryCode') IS NULL
        THROW 50001, N'Migration 004_AddUserIdentityDocument.sql must be applied before 005.', 1;

    IF COL_LENGTH(N'dbo.Users', N'PassportExpiryDate') IS NULL
        THROW 50001, N'Migration 004_AddUserIdentityDocument.sql must be applied before 005.', 1;

    -- Mevcut TcNo kayıtlarını güvenli biçimde backfill et
    UPDATE dbo.Users
    SET IdentityDocumentType = 1,
        IdentityNumber = TcNo,
        NormalizedIdentityNumber = TcNo,
        NationalityCountryCode = 'TR'
    WHERE TcNo IS NOT NULL
      AND LTRIM(RTRIM(TcNo)) <> ''
      AND IdentityDocumentType IS NULL;

    -- Duplicate audit
    IF EXISTS (
        SELECT 1
        FROM dbo.Users
        WHERE IdentityDocumentType IN (1, 2)
          AND NormalizedIdentityNumber IS NOT NULL
        GROUP BY IdentityDocumentType, NormalizedIdentityNumber
        HAVING COUNT(*) > 1
    )
        THROW 50002, N'Duplicate NormalizedIdentityNumber found for Turkish or foreign identity.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.Users
        WHERE IdentityDocumentType = 3
          AND IssuingCountryCode IS NOT NULL
          AND NormalizedIdentityNumber IS NOT NULL
        GROUP BY IssuingCountryCode, NormalizedIdentityNumber
        HAVING COUNT(*) > 1
    )
        THROW 50003, N'Duplicate passport identity found.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.Users
        WHERE TcNo IS NOT NULL
        GROUP BY TcNo
        HAVING COUNT(*) > 1
    )
        THROW 50004, N'Duplicate TcNo values found.', 1;

    -- B. TcNo geçişi
    IF EXISTS (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_Users_TcNo'
          AND parent_object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        ALTER TABLE dbo.Users DROP CONSTRAINT UQ_Users_TcNo;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Users')
          AND c.name = N'TcNo'
          AND (t.name <> N'varchar' OR c.max_length <> 11 OR c.is_nullable = 0)
    )
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN TcNo VARCHAR(11) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_Users_TcNo_NotNull'
          AND object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        CREATE UNIQUE INDEX UQ_Users_TcNo_NotNull
        ON dbo.Users (TcNo)
        WHERE TcNo IS NOT NULL;
    END;

    -- C. Yeni alanların zorunluluğu
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE IdentityDocumentType IS NULL)
        THROW 50005, N'IdentityDocumentType contains NULL values; backfill required before NOT NULL.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE IdentityNumber IS NULL)
        THROW 50005, N'IdentityNumber contains NULL values; backfill required before NOT NULL.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE NormalizedIdentityNumber IS NULL)
        THROW 50005, N'NormalizedIdentityNumber contains NULL values; backfill required before NOT NULL.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE NationalityCountryCode IS NULL)
        THROW 50005, N'NationalityCountryCode contains NULL values; backfill required before NOT NULL.', 1;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Users')
          AND name = N'IdentityDocumentType'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN IdentityDocumentType TINYINT NOT NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Users')
          AND name = N'IdentityNumber'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN IdentityNumber NVARCHAR(32) NOT NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Users')
          AND name = N'NormalizedIdentityNumber'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN NormalizedIdentityNumber VARCHAR(32) NOT NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Users')
          AND name = N'NationalityCountryCode'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.Users ALTER COLUMN NationalityCountryCode CHAR(2) NOT NULL;
    END;

    -- D. CHECK constraint'ler
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_Users_IdentityDocumentType'
          AND parent_object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        ALTER TABLE dbo.Users
        ADD CONSTRAINT CK_Users_IdentityDocumentType
        CHECK (IdentityDocumentType IN (1, 2, 3));
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = N'CK_Users_IdentityDocumentConsistency'
          AND parent_object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        ALTER TABLE dbo.Users
        ADD CONSTRAINT CK_Users_IdentityDocumentConsistency
        CHECK (
            (IdentityDocumentType = 1
                AND TcNo IS NOT NULL
                AND NationalityCountryCode = 'TR'
                AND IssuingCountryCode IS NULL
                AND PassportExpiryDate IS NULL)
            OR (IdentityDocumentType = 2
                AND TcNo IS NULL
                AND NationalityCountryCode <> 'TR'
                AND IssuingCountryCode IS NULL
                AND PassportExpiryDate IS NULL)
            OR (IdentityDocumentType = 3
                AND TcNo IS NULL
                AND NationalityCountryCode <> 'TR'
                AND IssuingCountryCode IS NOT NULL
                AND PassportExpiryDate IS NOT NULL)
        );
    END;

    -- E. UNIQUE index'ler
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_Users_Identity_Turkish'
          AND object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        CREATE UNIQUE INDEX UQ_Users_Identity_Turkish
        ON dbo.Users (NormalizedIdentityNumber)
        WHERE IdentityDocumentType = 1;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_Users_Identity_Foreign'
          AND object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        CREATE UNIQUE INDEX UQ_Users_Identity_Foreign
        ON dbo.Users (NormalizedIdentityNumber)
        WHERE IdentityDocumentType = 2;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'UQ_Users_Identity_Passport'
          AND object_id = OBJECT_ID(N'dbo.Users')
    )
    BEGIN
        CREATE UNIQUE INDEX UQ_Users_Identity_Passport
        ON dbo.Users (IssuingCountryCode, NormalizedIdentityNumber)
        WHERE IdentityDocumentType = 3;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

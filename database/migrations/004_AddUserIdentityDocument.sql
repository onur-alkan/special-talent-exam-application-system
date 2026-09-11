SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

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

    IF COL_LENGTH(N'dbo.Users', N'IdentityDocumentType') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD IdentityDocumentType TINYINT NULL;';
    END;

    IF COL_LENGTH(N'dbo.Users', N'IdentityNumber') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD IdentityNumber NVARCHAR(32) NULL;';
    END;

    IF COL_LENGTH(N'dbo.Users', N'NormalizedIdentityNumber') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD NormalizedIdentityNumber VARCHAR(32) NULL;';
    END;

    IF COL_LENGTH(N'dbo.Users', N'NationalityCountryCode') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD NationalityCountryCode CHAR(2) NULL;';
    END;

    IF COL_LENGTH(N'dbo.Users', N'IssuingCountryCode') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD IssuingCountryCode CHAR(2) NULL;';
    END;

    IF COL_LENGTH(N'dbo.Users', N'PassportExpiryDate') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD PassportExpiryDate DATE NULL;';
    END;

    EXEC sys.sp_executesql
        N'UPDATE dbo.Users
          SET IdentityDocumentType = 1,
              IdentityNumber = TcNo,
              NormalizedIdentityNumber = TcNo,
              NationalityCountryCode = ''TR''
          WHERE TcNo IS NOT NULL
            AND LTRIM(RTRIM(TcNo)) <> ''''
            AND IdentityDocumentType IS NULL;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

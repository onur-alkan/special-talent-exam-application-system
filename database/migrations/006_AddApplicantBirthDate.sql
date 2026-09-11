SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Aday doğum tarihi (nullable DATE). Mevcut BirthYear verisi korunur; geriye dönük
-- doldurma yapılmaz (gün/ay bilinmediği için uydurma tarih yazılmaz).
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

    IF COL_LENGTH(N'dbo.Users', N'BirthDate') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD BirthDate DATE NULL;';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

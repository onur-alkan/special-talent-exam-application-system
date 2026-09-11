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

    IF COL_LENGTH(N'dbo.Users', N'BirthYear') IS NULL
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              ADD BirthYear SMALLINT NULL;';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Users')
          AND name = N'CK_Users_BirthYear'
    )
    BEGIN
        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users WITH CHECK
              ADD CONSTRAINT CK_Users_BirthYear
              CHECK
              (
                  BirthYear IS NULL
                  OR BirthYear BETWEEN 1900 AND 2100
              );';

        EXEC sys.sp_executesql
            N'ALTER TABLE dbo.Users
              CHECK CONSTRAINT CK_Users_BirthYear;';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

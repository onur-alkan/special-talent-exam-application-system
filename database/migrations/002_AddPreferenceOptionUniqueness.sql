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

    IF OBJECT_ID(N'dbo.ExamPreferenceOptions', N'U') IS NULL
    BEGIN
        THROW 50001, N'Prerequisite missing: dbo.ExamPreferenceOptions. Apply the base schema script first.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ExamPreferenceOptions
        GROUP BY ExamPeriodId, PreferenceName
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51001, N'ExamPreferenceOptions içinde aynı sınav dönemine ait yinelenen tercih adları var. Migration uygulanmadan önce veriyi düzeltin.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ExamPreferenceOptions
        GROUP BY ExamPeriodId, DisplayOrder
        HAVING COUNT(*) > 1
    )
    BEGIN
        THROW 51002, N'ExamPreferenceOptions içinde aynı sınav dönemine ait yinelenen görünüm sıraları var. Migration uygulanmadan önce veriyi düzeltin.', 1;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE [name] = N'UQ_ExamPreferenceOptions_Period_Name'
          AND parent_object_id = OBJECT_ID(N'dbo.ExamPreferenceOptions')
    )
    BEGIN
        ALTER TABLE dbo.ExamPreferenceOptions
            ADD CONSTRAINT UQ_ExamPreferenceOptions_Period_Name
            UNIQUE (ExamPeriodId, PreferenceName);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE [name] = N'UQ_ExamPreferenceOptions_Period_Order'
          AND parent_object_id = OBJECT_ID(N'dbo.ExamPreferenceOptions')
    )
    BEGIN
        ALTER TABLE dbo.ExamPreferenceOptions
            ADD CONSTRAINT UQ_ExamPreferenceOptions_Period_Order
            UNIQUE (ExamPeriodId, DisplayOrder);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

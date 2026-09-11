SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    THROW 50000,
        N'Migration must run against the target application database, not a system database. Use sqlcmd -d TargetDatabase (or select the database in SSMS).',
        1;
END;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    THROW 50001, N'Prerequisite missing: dbo.Users. Apply the base schema script first.', 1;
END;
GO

-- Sütunu önce ayrı bir batch içinde oluştur.
IF COL_LENGTH(N'dbo.Users', N'SecurityStamp') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD SecurityStamp UNIQUEIDENTIFIER NULL;
END;
GO

-- Mevcut kullanıcıların her birine ayrı değer ata.
UPDATE dbo.Users
SET SecurityStamp = NEWID()
WHERE SecurityStamp IS NULL;
GO

-- Veri doldurulduktan sonra NOT NULL yap.
IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Users')
      AND name = N'SecurityStamp'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.Users
        ALTER COLUMN SecurityStamp UNIQUEIDENTIFIER NOT NULL;
END;
GO

-- Yeni kullanıcılar için varsayılan değer.
IF NOT EXISTS
(
    SELECT 1
    FROM sys.default_constraints AS dc
    INNER JOIN sys.columns AS c
        ON c.default_object_id = dc.object_id
    WHERE c.object_id = OBJECT_ID(N'dbo.Users')
      AND c.name = N'SecurityStamp'
)
BEGIN
    ALTER TABLE dbo.Users
        ADD CONSTRAINT DF_Users_SecurityStamp
        DEFAULT NEWID() FOR SecurityStamp;
END;
GO

PRINT 'Users.SecurityStamp migration completed.';
GO

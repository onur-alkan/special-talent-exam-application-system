/* =====================================================================================
   Özel Yetenek Sınavları Başvuru Sistemi (Public Portfolio Edition)
   Microsoft SQL Server veritabanı kurulum scripti
   -------------------------------------------------------------------------------------
   KURALLAR:
   - Tüm Primary Key alanları UNIQUEIDENTIFIER (NEWSEQUENTIALID() default) kullanır.
   - Tüm Foreign Key alanları UNIQUEIDENTIFIER'dir.
   - CandidateNo, PreferenceOrder, DisplayOrder gibi sıra alanları INT'tir.
   - Script tekrar çalıştırılabilir (idempotent) olacak şekilde yazılmıştır.
   - Mevcut veritabanı veya kullanıcı verileri DROP EDİLMEZ.
   - Parola düz metin olarak yazılmaz. SuperAdmin parolası uygulama startup seeder'ı
     tarafından ASP.NET Core PasswordHasher ile hash'lenerek oluşturulur.
   - Bu script USE / CREATE DATABASE içermez. Hedef veritabanını sqlcmd -d veya
     SSMS bağlantısı ile seçin. İsteğe bağlı yerel DB oluşturma:
     database/bootstrap/CreateDatabase.OzelYetenekSinavSistemi.sql
   ===================================================================================== */
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

/* ---------------------------------------------------------------------------
   0. Hedef veritabanı koruması (sabit DB adı zorunlu değildir)
   --------------------------------------------------------------------------- */
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    THROW 50000,
        N'Schema script must run against the target application database, not a system database. Create the database first, then run with sqlcmd -d TargetDatabase (or select the database in SSMS).',
        1;
END;
GO

/* ---------------------------------------------------------------------------
   1. Roller
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        Id          UNIQUEIDENTIFIER NOT NULL
                    CONSTRAINT DF_Roles_Id DEFAULT NEWSEQUENTIALID(),
        Name        NVARCHAR(50)  NOT NULL,
        Description NVARCHAR(200) NULL,
        CreatedDate DATETIME2(3)  NOT NULL
                    CONSTRAINT DF_Roles_CreatedDate DEFAULT SYSDATETIME(),
        CONSTRAINT PK_Roles PRIMARY KEY (Id),
        CONSTRAINT UQ_Roles_Name UNIQUE (Name)
    );
END
GO

/* ---------------------------------------------------------------------------
   3. YGS Yılları
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.YgsYears', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.YgsYears
    (
        Id          UNIQUEIDENTIFIER NOT NULL
                    CONSTRAINT DF_YgsYears_Id DEFAULT NEWSEQUENTIALID(),
        [Year]      INT          NOT NULL,
        IsActive    BIT          NOT NULL CONSTRAINT DF_YgsYears_IsActive DEFAULT 1,
        CreatedDate DATETIME2(3) NOT NULL CONSTRAINT DF_YgsYears_CreatedDate DEFAULT SYSDATETIME(),
        CONSTRAINT PK_YgsYears PRIMARY KEY (Id),
        CONSTRAINT UQ_YgsYears_Year UNIQUE ([Year]),
        CONSTRAINT CK_YgsYears_Year CHECK ([Year] BETWEEN 1990 AND 2100)
    );
END
GO

/* ---------------------------------------------------------------------------
   4. Kullanıcılar
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id                UNIQUEIDENTIFIER NOT NULL
                          CONSTRAINT DF_Users_Id DEFAULT NEWSEQUENTIALID(),
        RoleId            UNIQUEIDENTIFIER NOT NULL,
        TcNo                     VARCHAR(11)  NULL,
        IdentityDocumentType     TINYINT      NOT NULL,
        IdentityNumber           NVARCHAR(32) NOT NULL,
        NormalizedIdentityNumber VARCHAR(32)  NOT NULL,
        NationalityCountryCode   CHAR(2)      NOT NULL,
        IssuingCountryCode       CHAR(2)      NULL,
        PassportExpiryDate       DATE         NULL,
        PasswordHash      VARCHAR(512)  NOT NULL,
        Email             NVARCHAR(150) NOT NULL,
        FirstName         NVARCHAR(50)  NOT NULL,
        LastName          NVARCHAR(50)  NOT NULL,
        BirthYear         SMALLINT      NULL,
        BirthDate         DATE          NULL,
        Nationality       NVARCHAR(50)  NULL,
        HighSchool        NVARCHAR(150) NULL,
        DepartmentField   NVARCHAR(100) NULL,
        YgsScore          DECIMAL(6,2)  NULL,
        YgsYearId         UNIQUEIDENTIFIER NULL,
        [Address]         NVARCHAR(500) NULL,
        Phone             NVARCHAR(20)  NULL,
        HasDisability     BIT           NOT NULL CONSTRAINT DF_Users_HasDisability DEFAULT 0,
        DisabilityDetails NVARCHAR(MAX) NULL,
        PhotoPath         NVARCHAR(256) NULL,
        IsActive          BIT           NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,
        FailedLoginCount  INT           NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT 0,
        LockoutEnd        DATETIME2(3)  NULL,
        MustChangePassword BIT          NOT NULL CONSTRAINT DF_Users_MustChangePassword DEFAULT 0,
        SecurityStamp     UNIQUEIDENTIFIER NOT NULL
                          CONSTRAINT DF_Users_SecurityStamp DEFAULT NEWID(),
        CreatedDate       DATETIME2(3)  NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT SYSDATETIME(),
        UpdatedDate       DATETIME2(3)  NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id),
        CONSTRAINT UQ_Users_Email UNIQUE (Email),
        CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id),
        CONSTRAINT FK_Users_YgsYears FOREIGN KEY (YgsYearId) REFERENCES dbo.YgsYears(Id),
        CONSTRAINT CK_Users_BirthYear
            CHECK (BirthYear IS NULL OR BirthYear BETWEEN 1900 AND 2100),
        CONSTRAINT CK_Users_YgsScore
            CHECK (YgsScore IS NULL OR (YgsScore >= 0 AND YgsScore <= 560)),
        CONSTRAINT CK_Users_FailedLoginCount
            CHECK (FailedLoginCount >= 0),
        CONSTRAINT CK_Users_IdentityDocumentType
            CHECK (IdentityDocumentType IN (1, 2, 3)),
        CONSTRAINT CK_Users_IdentityDocumentConsistency
            CHECK
            (
                (
                    IdentityDocumentType = 1
                    AND TcNo IS NOT NULL
                    AND NationalityCountryCode = 'TR'
                    AND IssuingCountryCode IS NULL
                    AND PassportExpiryDate IS NULL
                )
                OR
                (
                    IdentityDocumentType = 2
                    AND TcNo IS NULL
                    AND NationalityCountryCode <> 'TR'
                    AND IssuingCountryCode IS NULL
                    AND PassportExpiryDate IS NULL
                )
                OR
                (
                    IdentityDocumentType = 3
                    AND TcNo IS NULL
                    AND NationalityCountryCode <> 'TR'
                    AND IssuingCountryCode IS NOT NULL
                    AND PassportExpiryDate IS NOT NULL
                )
            )
    );

    CREATE INDEX IX_Users_RoleId
        ON dbo.Users(RoleId);

    CREATE INDEX IX_Users_YgsYearId
        ON dbo.Users(YgsYearId);

    CREATE UNIQUE INDEX UQ_Users_TcNo_NotNull
        ON dbo.Users(TcNo)
        WHERE TcNo IS NOT NULL;

    CREATE UNIQUE INDEX UQ_Users_Identity_Turkish
        ON dbo.Users(NormalizedIdentityNumber)
        WHERE IdentityDocumentType = 1;

    CREATE UNIQUE INDEX UQ_Users_Identity_Foreign
        ON dbo.Users(NormalizedIdentityNumber)
        WHERE IdentityDocumentType = 2;

    CREATE UNIQUE INDEX UQ_Users_Identity_Passport
        ON dbo.Users(IssuingCountryCode, NormalizedIdentityNumber)
        WHERE IdentityDocumentType = 3;
END
GO

/* ---------------------------------------------------------------------------
   5. Sınav Dönemleri
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ExamPeriods', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExamPeriods
    (
        Id              UNIQUEIDENTIFIER NOT NULL
                        CONSTRAINT DF_ExamPeriods_Id DEFAULT NEWSEQUENTIALID(),
        Title           NVARCHAR(200) NOT NULL,
        [Description]   NVARCHAR(1000) NULL,
        StartDate       DATETIME2(3)  NOT NULL,
        EndDate         DATETIME2(3)  NOT NULL,
        MaxPreferences  INT           NOT NULL CONSTRAINT DF_ExamPeriods_MaxPreferences DEFAULT 1,
        IsActive        BIT           NOT NULL CONSTRAINT DF_ExamPeriods_IsActive DEFAULT 1,
        IsClosed        BIT           NOT NULL CONSTRAINT DF_ExamPeriods_IsClosed DEFAULT 0,
        IsDeleted       BIT           NOT NULL CONSTRAINT DF_ExamPeriods_IsDeleted DEFAULT 0,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedDate     DATETIME2(3)  NOT NULL CONSTRAINT DF_ExamPeriods_CreatedDate DEFAULT SYSDATETIME(),
        UpdatedDate     DATETIME2(3)  NULL,
        CONSTRAINT PK_ExamPeriods PRIMARY KEY (Id),
        CONSTRAINT FK_ExamPeriods_Users FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_ExamPeriods_MaxPreferences CHECK (MaxPreferences >= 1),
        CONSTRAINT CK_ExamPeriods_Dates CHECK (EndDate > StartDate)
    );

    CREATE INDEX IX_ExamPeriods_CreatedByUserId ON dbo.ExamPeriods(CreatedByUserId);
    CREATE INDEX IX_ExamPeriods_Active ON dbo.ExamPeriods(IsActive, IsClosed, IsDeleted, StartDate, EndDate);
END
GO

/* ---------------------------------------------------------------------------
   6. Tercih Seçenekleri
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ExamPreferenceOptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExamPreferenceOptions
    (
        Id             UNIQUEIDENTIFIER NOT NULL
                       CONSTRAINT DF_ExamPreferenceOptions_Id DEFAULT NEWSEQUENTIALID(),
        ExamPeriodId   UNIQUEIDENTIFIER NOT NULL,
        PreferenceName NVARCHAR(100) NOT NULL,
        DisplayOrder   INT           NOT NULL CONSTRAINT DF_ExamPreferenceOptions_DisplayOrder DEFAULT 1,
        IsActive       BIT           NOT NULL CONSTRAINT DF_ExamPreferenceOptions_IsActive DEFAULT 1,
        CONSTRAINT PK_ExamPreferenceOptions PRIMARY KEY (Id),
        CONSTRAINT FK_ExamPreferenceOptions_ExamPeriods FOREIGN KEY (ExamPeriodId)
                   REFERENCES dbo.ExamPeriods(Id),
        CONSTRAINT CK_ExamPreferenceOptions_DisplayOrder CHECK (DisplayOrder >= 1),
        CONSTRAINT UQ_ExamPreferenceOptions_Period_Name UNIQUE (ExamPeriodId, PreferenceName),
        CONSTRAINT UQ_ExamPreferenceOptions_Period_Order UNIQUE (ExamPeriodId, DisplayOrder)
    );

    CREATE INDEX IX_ExamPreferenceOptions_ExamPeriodId ON dbo.ExamPreferenceOptions(ExamPeriodId);
END
GO

/* ---------------------------------------------------------------------------
   7. Sınav Dönemi Yöneticileri (atama)
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ExamPeriodManagers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExamPeriodManagers
    (
        Id               UNIQUEIDENTIFIER NOT NULL
                         CONSTRAINT DF_ExamPeriodManagers_Id DEFAULT NEWSEQUENTIALID(),
        ExamPeriodId     UNIQUEIDENTIFIER NOT NULL,
        ManagerUserId    UNIQUEIDENTIFIER NOT NULL,
        AssignedByUserId UNIQUEIDENTIFIER NOT NULL,
        AssignedDate     DATETIME2(3) NOT NULL CONSTRAINT DF_ExamPeriodManagers_AssignedDate DEFAULT SYSDATETIME(),
        CONSTRAINT PK_ExamPeriodManagers PRIMARY KEY (Id),
        CONSTRAINT UQ_ExamPeriodManagers UNIQUE (ExamPeriodId, ManagerUserId),
        CONSTRAINT FK_ExamPeriodManagers_ExamPeriods FOREIGN KEY (ExamPeriodId) REFERENCES dbo.ExamPeriods(Id),
        CONSTRAINT FK_ExamPeriodManagers_Manager FOREIGN KEY (ManagerUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_ExamPeriodManagers_AssignedBy FOREIGN KEY (AssignedByUserId) REFERENCES dbo.Users(Id)
    );

    CREATE INDEX IX_ExamPeriodManagers_ManagerUserId ON dbo.ExamPeriodManagers(ManagerUserId);
    CREATE INDEX IX_ExamPeriodManagers_ExamPeriodId  ON dbo.ExamPeriodManagers(ExamPeriodId);
END
GO

/* ---------------------------------------------------------------------------
   8. Sınav Dönemi Sayaçları (atomik CandidateNo üretimi için)
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ExamPeriodCounters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExamPeriodCounters
    (
        ExamPeriodId    UNIQUEIDENTIFIER NOT NULL,
        LastCandidateNo INT          NOT NULL CONSTRAINT DF_ExamPeriodCounters_LastCandidateNo DEFAULT 0,
        UpdatedDate     DATETIME2(3) NOT NULL CONSTRAINT DF_ExamPeriodCounters_UpdatedDate DEFAULT SYSDATETIME(),
        CONSTRAINT PK_ExamPeriodCounters PRIMARY KEY (ExamPeriodId),
        CONSTRAINT FK_ExamPeriodCounters_ExamPeriods FOREIGN KEY (ExamPeriodId) REFERENCES dbo.ExamPeriods(Id),
        CONSTRAINT CK_ExamPeriodCounters_LastCandidateNo CHECK (LastCandidateNo >= 0)
    );
END
GO

/* ---------------------------------------------------------------------------
   9. Aday Başvuruları
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.CandidateApplications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateApplications
    (
        Id               UNIQUEIDENTIFIER NOT NULL
                         CONSTRAINT DF_CandidateApplications_Id DEFAULT NEWSEQUENTIALID(),
        UserId           UNIQUEIDENTIFIER NOT NULL,
        ExamPeriodId     UNIQUEIDENTIFIER NOT NULL,
        CandidateNo      INT          NOT NULL,
        RegistrationDate DATETIME2(3) NOT NULL CONSTRAINT DF_CandidateApplications_RegistrationDate DEFAULT SYSDATETIME(),
        UpdatedDate      DATETIME2(3) NULL,
        VerificationCode VARCHAR(40)  NOT NULL,
        [Status]         INT          NOT NULL CONSTRAINT DF_CandidateApplications_Status DEFAULT 1,
        CONSTRAINT PK_CandidateApplications PRIMARY KEY (Id),
        CONSTRAINT UQ_CandidateApplications_User_Exam UNIQUE (UserId, ExamPeriodId),
        CONSTRAINT UQ_CandidateApplications_Exam_No UNIQUE (ExamPeriodId, CandidateNo),
        CONSTRAINT UQ_CandidateApplications_VerificationCode UNIQUE (VerificationCode),
        CONSTRAINT FK_CandidateApplications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_CandidateApplications_ExamPeriods FOREIGN KEY (ExamPeriodId) REFERENCES dbo.ExamPeriods(Id),
        CONSTRAINT CK_CandidateApplications_CandidateNo CHECK (CandidateNo > 0)
    );

    CREATE INDEX IX_CandidateApplications_ExamPeriodId ON dbo.CandidateApplications(ExamPeriodId);
    CREATE INDEX IX_CandidateApplications_UserId       ON dbo.CandidateApplications(UserId);
END
GO

/* ---------------------------------------------------------------------------
   10. Aday Seçilen Tercihler
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.CandidateSelectedPreferences', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateSelectedPreferences
    (
        Id                 UNIQUEIDENTIFIER NOT NULL
                           CONSTRAINT DF_CandidateSelectedPreferences_Id DEFAULT NEWSEQUENTIALID(),
        ApplicationId      UNIQUEIDENTIFIER NOT NULL,
        PreferenceOptionId UNIQUEIDENTIFIER NOT NULL,
        PreferenceOrder    INT NOT NULL,
        CONSTRAINT PK_CandidateSelectedPreferences PRIMARY KEY (Id),
        CONSTRAINT UQ_CandidateSelectedPreferences_App_Option UNIQUE (ApplicationId, PreferenceOptionId),
        CONSTRAINT UQ_CandidateSelectedPreferences_App_Order  UNIQUE (ApplicationId, PreferenceOrder),
        CONSTRAINT FK_CandidateSelectedPreferences_Applications FOREIGN KEY (ApplicationId)
                   REFERENCES dbo.CandidateApplications(Id),
        CONSTRAINT FK_CandidateSelectedPreferences_Options FOREIGN KEY (PreferenceOptionId)
                   REFERENCES dbo.ExamPreferenceOptions(Id),
        CONSTRAINT CK_CandidateSelectedPreferences_Order CHECK (PreferenceOrder >= 1)
    );

    CREATE INDEX IX_CandidateSelectedPreferences_ApplicationId ON dbo.CandidateSelectedPreferences(ApplicationId);
    CREATE INDEX IX_CandidateSelectedPreferences_OptionId ON dbo.CandidateSelectedPreferences(PreferenceOptionId);
END
GO

/* ---------------------------------------------------------------------------
   11. Aday Sınav Sonuçları
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.CandidateExamResults', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CandidateExamResults
    (
        Id                             UNIQUEIDENTIFIER NOT NULL
                                       CONSTRAINT DF_CandidateExamResults_Id DEFAULT NEWSEQUENTIALID(),
        ApplicationId                  UNIQUEIDENTIFIER NOT NULL,
        AttendanceStatus               INT           NULL,
        ExamScore                      DECIMAL(5,2)  NULL,
        AdminDescription               NVARCHAR(MAX) NULL,
        IsDescriptionVisibleToCandidate BIT          NOT NULL CONSTRAINT DF_CandidateExamResults_DescVisible DEFAULT 0,
        EvaluatedByUserId              UNIQUEIDENTIFIER NULL,
        UpdatedDate                    DATETIME2(3)  NULL,
        CONSTRAINT PK_CandidateExamResults PRIMARY KEY (Id),
        CONSTRAINT UQ_CandidateExamResults_ApplicationId UNIQUE (ApplicationId),
        CONSTRAINT FK_CandidateExamResults_Applications FOREIGN KEY (ApplicationId)
                   REFERENCES dbo.CandidateApplications(Id),
        CONSTRAINT FK_CandidateExamResults_EvaluatedBy FOREIGN KEY (EvaluatedByUserId)
                   REFERENCES dbo.Users(Id),
        CONSTRAINT CK_CandidateExamResults_ExamScore CHECK (ExamScore IS NULL OR (ExamScore >= 0 AND ExamScore <= 100)),
        CONSTRAINT CK_CandidateExamResults_AttendanceStatus
                   CHECK (AttendanceStatus IS NULL OR AttendanceStatus IN (1, 2, 3, 4))
    );

    CREATE INDEX IX_CandidateExamResults_EvaluatedByUserId ON dbo.CandidateExamResults(EvaluatedByUserId);
END
GO

/* ---------------------------------------------------------------------------
   12. Parola Sıfırlama Token'ları
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens
    (
        Id                UNIQUEIDENTIFIER NOT NULL
                          CONSTRAINT DF_PasswordResetTokens_Id DEFAULT NEWSEQUENTIALID(),
        UserId            UNIQUEIDENTIFIER NOT NULL,
        TokenHash         VARCHAR(128) NOT NULL,
        ExpiresAt         DATETIME2(3) NOT NULL,
        UsedAt            DATETIME2(3) NULL,
        CreatedDate       DATETIME2(3) NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedDate DEFAULT SYSDATETIME(),
        CreatedIpAddress  NVARCHAR(64) NULL,
        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY (Id),
        CONSTRAINT UQ_PasswordResetTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );

    CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId);
END
GO

/* ---------------------------------------------------------------------------
   13. Audit Log
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        Id            UNIQUEIDENTIFIER NOT NULL
                      CONSTRAINT DF_AuditLogs_Id DEFAULT NEWSEQUENTIALID(),
        UserId        UNIQUEIDENTIFIER NULL,
        EventType     NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        IpAddress     NVARCHAR(64)  NULL,
        RequestPath   NVARCHAR(400) NULL,
        CorrelationId NVARCHAR(64)  NULL,
        CreatedDate   DATETIME2(3)  NOT NULL CONSTRAINT DF_AuditLogs_CreatedDate DEFAULT SYSDATETIME(),
        CONSTRAINT PK_AuditLogs PRIMARY KEY (Id)
    );

    CREATE INDEX IX_AuditLogs_UserId ON dbo.AuditLogs(UserId);
    CREATE INDEX IX_AuditLogs_CreatedDate ON dbo.AuditLogs(CreatedDate);
    CREATE INDEX IX_AuditLogs_EventType ON dbo.AuditLogs(EventType);
END
GO

/* ---------------------------------------------------------------------------
   14. Sistem Ayarları
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.SystemSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemSettings
    (
        Id              UNIQUEIDENTIFIER NOT NULL
                        CONSTRAINT DF_SystemSettings_Id DEFAULT NEWSEQUENTIALID(),
        SettingKey      NVARCHAR(100) NOT NULL,
        SettingValue    NVARCHAR(MAX) NULL,
        [Description]   NVARCHAR(400) NULL,
        UpdatedDate     DATETIME2(3)  NULL,
        UpdatedByUserId UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_SystemSettings PRIMARY KEY (Id),
        CONSTRAINT UQ_SystemSettings_SettingKey UNIQUE (SettingKey),
        CONSTRAINT FK_SystemSettings_Users FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.Users(Id)
    );
END
GO

/* ---------------------------------------------------------------------------
   15. Belge Doğrulama
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.DocumentVerifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentVerifications
    (
        Id               UNIQUEIDENTIFIER NOT NULL
                         CONSTRAINT DF_DocumentVerifications_Id DEFAULT NEWSEQUENTIALID(),
        ApplicationId    UNIQUEIDENTIFIER NOT NULL,
        VerificationCode VARCHAR(40)  NOT NULL,
        IsValid          BIT          NOT NULL CONSTRAINT DF_DocumentVerifications_IsValid DEFAULT 1,
        CreatedDate      DATETIME2(3) NOT NULL CONSTRAINT DF_DocumentVerifications_CreatedDate DEFAULT SYSDATETIME(),
        CONSTRAINT PK_DocumentVerifications PRIMARY KEY (Id),
        CONSTRAINT UQ_DocumentVerifications_VerificationCode UNIQUE (VerificationCode),
        CONSTRAINT UQ_DocumentVerifications_ApplicationId UNIQUE (ApplicationId),
        CONSTRAINT FK_DocumentVerifications_Applications FOREIGN KEY (ApplicationId)
                   REFERENCES dbo.CandidateApplications(Id)
    );
END
GO

/* ---------------------------------------------------------------------------
   16. Serilog Log Tablosu (özel UNIQUEIDENTIFIER Primary Key şeması)
   --------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Logs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Logs
    (
        -- Serilog sink teknik tablosu; Primary Key UNIQUEIDENTIFIER'dir.
        -- Id değerini veritabanı DEFAULT (NEWSEQUENTIALID()) üretir; sink Id kolonunu INSERT etmez.
        Id              UNIQUEIDENTIFIER NOT NULL
                        CONSTRAINT DF_SerilogLogs_Id DEFAULT NEWSEQUENTIALID(),
        Message         NVARCHAR(MAX) NULL,
        MessageTemplate NVARCHAR(MAX) NULL,
        [Level]         NVARCHAR(128) NULL,
        TimeStamp       DATETIMEOFFSET(7) NULL,
        [Exception]     NVARCHAR(MAX) NULL,
        Properties      NVARCHAR(MAX) NULL,
        LogEvent        NVARCHAR(MAX) NULL,
        UserId          NVARCHAR(64)  NULL,
        IpAddress       NVARCHAR(64)  NULL,
        RequestPath     NVARCHAR(400) NULL,
        RequestMethod   NVARCHAR(16)  NULL,
        CorrelationId   NVARCHAR(64)  NULL,
        EventType       NVARCHAR(100) NULL,
        CONSTRAINT PK_SerilogLogs PRIMARY KEY (Id)
    );
END
GO

/* =====================================================================================
   BAŞLANGIÇ VERİLERİ (SEED)
   ===================================================================================== */

/* Roller - sabit Guid değerleriyle */
MERGE dbo.Roles AS target
USING (VALUES
    (CONVERT(UNIQUEIDENTIFIER, '11111111-1111-1111-1111-111111111111'), N'SuperAdmin',         N'Sistemin tüm modüllerine tam erişim'),
    (CONVERT(UNIQUEIDENTIFIER, '22222222-2222-2222-2222-222222222222'), N'ApplicationManager', N'Sadece atanan sınav dönemlerini yönetir'),
    (CONVERT(UNIQUEIDENTIFIER, '33333333-3333-3333-3333-333333333333'), N'Candidate',          N'Aday - kendi başvurularını yönetir')
) AS source (Id, Name, [Description])
ON target.Id = source.Id
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, [Description]) VALUES (source.Id, source.Name, source.[Description]);
GO

/* YGS Yılları - başlangıç kayıtları */
DECLARE @y INT = 2018;
WHILE @y <= 2026
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.YgsYears WHERE [Year] = @y)
        INSERT INTO dbo.YgsYears ([Year], IsActive) VALUES (@y, 1);
    SET @y = @y + 1;
END
GO

/* Sistem ayarları - başlangıç */
IF NOT EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE SettingKey = N'MaxPhotoSizeKb')
    INSERT INTO dbo.SystemSettings (SettingKey, SettingValue, [Description])
    VALUES (N'MaxPhotoSizeKb', N'2048', N'Vesikalık fotoğraf için maksimum boyut (KB)');
GO

/* -------------------------------------------------------------------------------------
   Test amaçlı sınav dönemi ve tercih seçenekleri.
   NOT: ExamPeriods.CreatedByUserId FK'si nedeniyle bu kayıtlar yalnızca varsayılan
   SuperAdmin kullanıcısı mevcutsa eklenir. SuperAdmin kullanıcısı uygulama startup
   seeder'ı tarafından (parolası PasswordHasher ile hash'lenmiş şekilde) oluşturulur.
   Uygulama ilk kez çalıştıktan sonra bu script yeniden çalıştırılırsa test dönemi
   otomatik olarak eklenir. Startup seeder de aynı kayıtları idempotent oluşturur.
   ------------------------------------------------------------------------------------- */
DECLARE @superAdminUserId UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, 'AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA');
DECLARE @testExamId       UNIQUEIDENTIFIER = CONVERT(UNIQUEIDENTIFIER, 'BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB');

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @superAdminUserId)
   AND NOT EXISTS (SELECT 1 FROM dbo.ExamPeriods WHERE Id = @testExamId)
BEGIN
    INSERT INTO dbo.ExamPeriods (Id, Title, [Description], StartDate, EndDate, MaxPreferences, IsActive, IsClosed, IsDeleted, CreatedByUserId)
    VALUES (@testExamId,
            N'2026 Resim ve Heykel Bölümü Özel Yetenek Sınavı',
            N'Test amaçlı örnek sınav dönemi.',
            DATEADD(DAY, -1, SYSDATETIME()),
            DATEADD(DAY, 30, SYSDATETIME()),
            2, 1, 0, 0, @superAdminUserId);

    INSERT INTO dbo.ExamPeriodCounters (ExamPeriodId, LastCandidateNo) VALUES (@testExamId, 0);

    INSERT INTO dbo.ExamPreferenceOptions (ExamPeriodId, PreferenceName, DisplayOrder, IsActive)
    VALUES (@testExamId, N'Resim', 1, 1),
           (@testExamId, N'Heykel', 2, 1);
END
GO

PRINT N'Schema script completed successfully for database: ' + DB_NAME();
GO

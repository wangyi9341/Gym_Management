-- GYM力量健身管理系统 - SQL Server Schema
-- 建议数据库排序规则使用中文友好（可选），并开启 READ_COMMITTED_SNAPSHOT（可选）。

-- 教练表（工号为主键）
IF OBJECT_ID(N'dbo.Coaches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Coaches (
        EmployeeNo NVARCHAR(32) NOT NULL PRIMARY KEY,
        Name       NVARCHAR(50) NOT NULL,
        CreatedAt  DATETIME2(0) NOT NULL,
        UpdatedAt  DATETIME2(0) NOT NULL
    );
    CREATE INDEX IX_Coaches_Name ON dbo.Coaches (Name);
END
GO

-- 私教课会员
IF OBJECT_ID(N'dbo.PrivateTrainingMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrivateTrainingMembers (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name          NVARCHAR(50) NOT NULL,
        Gender        TINYINT NOT NULL, -- 0:Unknown, 1:Male, 2:Female
        Phone         NVARCHAR(20) NOT NULL,
        PaidAmount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_PrivateTrainingMembers_PaidAmount DEFAULT (0),
        TotalSessions INT NOT NULL CONSTRAINT DF_PrivateTrainingMembers_TotalSessions DEFAULT (0),
        UsedSessions  INT NOT NULL CONSTRAINT DF_PrivateTrainingMembers_UsedSessions DEFAULT (0),
        CreatedAt     DATETIME2(0) NOT NULL,
        UpdatedAt     DATETIME2(0) NOT NULL,
        CONSTRAINT CK_PrivateTrainingMembers_Sessions CHECK (
            TotalSessions >= 0 AND UsedSessions >= 0 AND UsedSessions <= TotalSessions
        ),
        CONSTRAINT CK_PrivateTrainingMembers_PaidAmount CHECK (PaidAmount >= 0)
    );

    CREATE INDEX IX_PrivateTrainingMembers_Phone ON dbo.PrivateTrainingMembers (Phone);
    CREATE INDEX IX_PrivateTrainingMembers_Name ON dbo.PrivateTrainingMembers (Name);
END
GO

-- 私教课费用记录
IF OBJECT_ID(N'dbo.PrivateTrainingFeeRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrivateTrainingFeeRecords (
        Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MemberId  INT NOT NULL,
        Amount    DECIMAL(18,2) NOT NULL,
        PaidAt    DATETIME2(0) NOT NULL,
        Note      NVARCHAR(200) NULL,
        CreatedAt DATETIME2(0) NOT NULL,
        CONSTRAINT FK_PrivateTrainingFeeRecords_MemberId
            FOREIGN KEY (MemberId) REFERENCES dbo.PrivateTrainingMembers (Id) ON DELETE CASCADE,
        CONSTRAINT CK_PrivateTrainingFeeRecords_Amount CHECK (Amount > 0)
    );

    CREATE INDEX IX_PrivateTrainingFeeRecords_MemberId_PaidAt
        ON dbo.PrivateTrainingFeeRecords (MemberId, PaidAt);
END
GO

-- 私教课课程消耗记录
IF OBJECT_ID(N'dbo.PrivateTrainingSessionRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PrivateTrainingSessionRecords (
        Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MemberId     INT NOT NULL,
        SessionsUsed INT NOT NULL CONSTRAINT DF_PrivateTrainingSessionRecords_SessionsUsed DEFAULT (1),
        UsedAt       DATETIME2(0) NOT NULL,
        Note         NVARCHAR(200) NULL,
        CreatedAt    DATETIME2(0) NOT NULL,
        CONSTRAINT FK_PrivateTrainingSessionRecords_MemberId
            FOREIGN KEY (MemberId) REFERENCES dbo.PrivateTrainingMembers (Id) ON DELETE CASCADE,
        CONSTRAINT CK_PrivateTrainingSessionRecords_SessionsUsed CHECK (SessionsUsed >= 1)
    );

    CREATE INDEX IX_PrivateTrainingSessionRecords_MemberId_UsedAt
        ON dbo.PrivateTrainingSessionRecords (MemberId, UsedAt);
END
GO

-- 年卡会员
IF OBJECT_ID(N'dbo.AnnualCardMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnnualCardMembers (
        Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name      NVARCHAR(50) NOT NULL,
        Gender    TINYINT NOT NULL, -- 0:Unknown, 1:Male, 2:Female
        Phone     NVARCHAR(20) NOT NULL,
        StartDate DATE NOT NULL,
        EndDate   DATE NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL,
        UpdatedAt DATETIME2(0) NOT NULL,
        CONSTRAINT CK_AnnualCardMembers_DateRange CHECK (EndDate >= StartDate)
    );

    -- 到期提醒核心索引
    CREATE INDEX IX_AnnualCardMembers_EndDate ON dbo.AnnualCardMembers (EndDate);
    CREATE INDEX IX_AnnualCardMembers_Phone ON dbo.AnnualCardMembers (Phone);
    CREATE INDEX IX_AnnualCardMembers_Name ON dbo.AnnualCardMembers (Name);
END
GO


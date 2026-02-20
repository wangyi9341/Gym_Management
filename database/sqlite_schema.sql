-- GYM力量健身管理系统 - SQLite Schema
-- 说明：
-- 1) 默认以 UTC+本地时间写入（由应用层 DateTime.Now 决定）
-- 2) SQLite 建议开启外键约束：PRAGMA foreign_keys = ON;

PRAGMA foreign_keys = ON;

-- 教练表（工号为主键）
CREATE TABLE IF NOT EXISTS Coaches (
    EmployeeNo   TEXT NOT NULL PRIMARY KEY,
    Name         TEXT NOT NULL,
    CreatedAt    TEXT NOT NULL,
    UpdatedAt    TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_Coaches_Name ON Coaches (Name);

-- 私教课会员
CREATE TABLE IF NOT EXISTS PrivateTrainingMembers (
    Id            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Name          TEXT NOT NULL,
    Gender        INTEGER NOT NULL, -- 0:Unknown, 1:Male, 2:Female
    Phone         TEXT NOT NULL,
    PaidAmount    REAL NOT NULL DEFAULT 0,
    TotalSessions INTEGER NOT NULL DEFAULT 0,
    UsedSessions  INTEGER NOT NULL DEFAULT 0,
    CreatedAt     TEXT NOT NULL,
    UpdatedAt     TEXT NOT NULL,
    CONSTRAINT CK_PrivateTrainingMembers_Sessions CHECK (
        TotalSessions >= 0 AND UsedSessions >= 0 AND UsedSessions <= TotalSessions
    ),
    CONSTRAINT CK_PrivateTrainingMembers_PaidAmount CHECK (PaidAmount >= 0)
);

CREATE INDEX IF NOT EXISTS IX_PrivateTrainingMembers_Phone ON PrivateTrainingMembers (Phone);
CREATE INDEX IF NOT EXISTS IX_PrivateTrainingMembers_Name ON PrivateTrainingMembers (Name);

-- 私教课费用记录（用于“费用记录”列表与统计）
CREATE TABLE IF NOT EXISTS PrivateTrainingFeeRecords (
    Id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    MemberId   INTEGER NOT NULL,
    Amount     REAL NOT NULL,
    PaidAt     TEXT NOT NULL,
    Note       TEXT NULL,
    CreatedAt  TEXT NOT NULL,
    CONSTRAINT FK_PrivateTrainingFeeRecords_MemberId
        FOREIGN KEY (MemberId) REFERENCES PrivateTrainingMembers (Id) ON DELETE CASCADE,
    CONSTRAINT CK_PrivateTrainingFeeRecords_Amount CHECK (Amount > 0)
);

CREATE INDEX IF NOT EXISTS IX_PrivateTrainingFeeRecords_MemberId_PaidAt
    ON PrivateTrainingFeeRecords (MemberId, PaidAt);

-- 私教课课程消耗记录（用于“课程消耗记录”列表与统计）
CREATE TABLE IF NOT EXISTS PrivateTrainingSessionRecords (
    Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    MemberId    INTEGER NOT NULL,
    SessionsUsed INTEGER NOT NULL DEFAULT 1,
    UsedAt      TEXT NOT NULL,
    Note        TEXT NULL,
    CreatedAt   TEXT NOT NULL,
    CONSTRAINT FK_PrivateTrainingSessionRecords_MemberId
        FOREIGN KEY (MemberId) REFERENCES PrivateTrainingMembers (Id) ON DELETE CASCADE,
    CONSTRAINT CK_PrivateTrainingSessionRecords_SessionsUsed CHECK (SessionsUsed >= 1)
);

CREATE INDEX IF NOT EXISTS IX_PrivateTrainingSessionRecords_MemberId_UsedAt
    ON PrivateTrainingSessionRecords (MemberId, UsedAt);

-- 年卡会员
CREATE TABLE IF NOT EXISTS AnnualCardMembers (
    Id         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Name       TEXT NOT NULL,
    Gender     INTEGER NOT NULL, -- 0:Unknown, 1:Male, 2:Female
    Phone      TEXT NOT NULL,
    StartDate  TEXT NOT NULL, -- yyyy-MM-dd
    EndDate    TEXT NOT NULL, -- yyyy-MM-dd
    CreatedAt  TEXT NOT NULL,
    UpdatedAt  TEXT NOT NULL,
    CONSTRAINT CK_AnnualCardMembers_DateRange CHECK (EndDate >= StartDate)
);

CREATE INDEX IF NOT EXISTS IX_AnnualCardMembers_EndDate ON AnnualCardMembers (EndDate);
CREATE INDEX IF NOT EXISTS IX_AnnualCardMembers_Phone ON AnnualCardMembers (Phone);
CREATE INDEX IF NOT EXISTS IX_AnnualCardMembers_Name ON AnnualCardMembers (Name);


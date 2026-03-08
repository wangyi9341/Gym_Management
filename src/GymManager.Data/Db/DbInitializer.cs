using Microsoft.EntityFrameworkCore;

namespace GymManager.Data.Db;

/// <summary>
/// 数据库初始化（建表、SQLite 推荐 PRAGMA）。
/// </summary>
public static class DbInitializer
{
    public static async Task EnsureCreatedAsync(GymDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        // SQLite 常用设置：开启外键约束 & WAL（更适合桌面端并发读写）
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken)
                .ConfigureAwait(false);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken)
                .ConfigureAwait(false);
        }

        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        // 注意：EnsureCreated 只会在“数据库不存在”时创建表；
        // 当我们给现有项目新增表/字段时，需要补一层轻量级“自升级”逻辑，避免老库缺表导致运行时报错。
        await EnsureSchemaAsync(db, cancellationToken).ConfigureAwait(false);
    }

    private static Task EnsureSchemaAsync(GymDbContext db, CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlite())
        {
            return EnsureSqliteSchemaAsync(db, cancellationToken);
        }

        if (db.Database.IsSqlServer())
        {
            return EnsureSqlServerSchemaAsync(db, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private static async Task EnsureSqliteSchemaAsync(GymDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS AnnualCardPauseRecords (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                MemberId INTEGER NOT NULL,
                MemberName TEXT NOT NULL,
                MemberPhone TEXT NOT NULL,
                PauseStartDate TEXT NOT NULL,
                ResumeDate TEXT NOT NULL,
                PauseDays INTEGER NOT NULL,
                EndDateBefore TEXT NOT NULL,
                EndDateAfter TEXT NOT NULL,
                Note TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT FK_AnnualCardPauseRecords_AnnualCardMembers_MemberId
                    FOREIGN KEY (MemberId) REFERENCES AnnualCardMembers (Id) ON DELETE CASCADE,
                CONSTRAINT CK_AnnualCardPauseRecords_PauseDays CHECK (PauseDays >= 1)
            );
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_AnnualCardPauseRecords_MemberId_PauseStartDate
            ON AnnualCardPauseRecords (MemberId, PauseStartDate);
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_AnnualCardPauseRecords_ResumeDate
            ON AnnualCardPauseRecords (ResumeDate);
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS AnnualCardRenewRecords (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                MemberId INTEGER NOT NULL,
                MemberName TEXT NOT NULL,
                MemberPhone TEXT NOT NULL,
                RenewedAt TEXT NOT NULL,
                StartDateBefore TEXT NOT NULL,
                EndDateBefore TEXT NOT NULL,
                StartDateAfter TEXT NOT NULL,
                EndDateAfter TEXT NOT NULL,
                Note TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT FK_AnnualCardRenewRecords_AnnualCardMembers_MemberId
                    FOREIGN KEY (MemberId) REFERENCES AnnualCardMembers (Id) ON DELETE CASCADE
            );
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_AnnualCardRenewRecords_MemberId_RenewedAt
            ON AnnualCardRenewRecords (MemberId, RenewedAt);
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS IX_AnnualCardRenewRecords_RenewedAt
            ON AnnualCardRenewRecords (RenewedAt);
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSqlServerSchemaAsync(GymDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[AnnualCardPauseRecords]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AnnualCardPauseRecords] (
                    [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AnnualCardPauseRecords] PRIMARY KEY,
                    [MemberId] INT NOT NULL,
                    [MemberName] NVARCHAR(50) NOT NULL,
                    [MemberPhone] NVARCHAR(20) NOT NULL,
                    [PauseStartDate] DATE NOT NULL,
                    [ResumeDate] DATE NOT NULL,
                    [PauseDays] INT NOT NULL,
                    [EndDateBefore] DATE NOT NULL,
                    [EndDateAfter] DATE NOT NULL,
                    [Note] NVARCHAR(200) NULL,
                    [CreatedAt] DATETIME2 NOT NULL,
                    [UpdatedAt] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_AnnualCardPauseRecords_AnnualCardMembers_MemberId]
                        FOREIGN KEY ([MemberId]) REFERENCES [dbo].[AnnualCardMembers] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [CK_AnnualCardPauseRecords_PauseDays] CHECK ([PauseDays] >= 1)
                );

                CREATE INDEX [IX_AnnualCardPauseRecords_MemberId_PauseStartDate]
                    ON [dbo].[AnnualCardPauseRecords] ([MemberId], [PauseStartDate]);

                CREATE INDEX [IX_AnnualCardPauseRecords_ResumeDate]
                    ON [dbo].[AnnualCardPauseRecords] ([ResumeDate]);
            END
            """,
            cancellationToken).ConfigureAwait(false);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[AnnualCardRenewRecords]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AnnualCardRenewRecords] (
                    [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_AnnualCardRenewRecords] PRIMARY KEY,
                    [MemberId] INT NOT NULL,
                    [MemberName] NVARCHAR(50) NOT NULL,
                    [MemberPhone] NVARCHAR(20) NOT NULL,
                    [RenewedAt] DATETIME2 NOT NULL,
                    [StartDateBefore] DATE NOT NULL,
                    [EndDateBefore] DATE NOT NULL,
                    [StartDateAfter] DATE NOT NULL,
                    [EndDateAfter] DATE NOT NULL,
                    [Note] NVARCHAR(200) NULL,
                    [CreatedAt] DATETIME2 NOT NULL,
                    [UpdatedAt] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_AnnualCardRenewRecords_AnnualCardMembers_MemberId]
                        FOREIGN KEY ([MemberId]) REFERENCES [dbo].[AnnualCardMembers] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_AnnualCardRenewRecords_MemberId_RenewedAt]
                    ON [dbo].[AnnualCardRenewRecords] ([MemberId], [RenewedAt]);

                CREATE INDEX [IX_AnnualCardRenewRecords_RenewedAt]
                    ON [dbo].[AnnualCardRenewRecords] ([RenewedAt]);
            END
            """,
            cancellationToken).ConfigureAwait(false);
    }
}

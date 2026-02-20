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
    }
}


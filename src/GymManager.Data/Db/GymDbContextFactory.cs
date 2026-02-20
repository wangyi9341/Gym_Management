using Microsoft.EntityFrameworkCore;

namespace GymManager.Data.Db;

/// <summary>
/// DbContext 工厂：用于在没有完整 DI 容器的 WPF 应用中创建 DbContext。
/// </summary>
public static class GymDbContextFactory
{
    public static GymDbContext CreateSqlite(string dbPath)
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new GymDbContext(options);
    }

    public static GymDbContext CreateSqlServer(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new GymDbContext(options);
    }
}


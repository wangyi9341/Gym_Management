using System.IO;
using GymManager.App.Config;
using GymManager.Data.Db;
using Microsoft.EntityFrameworkCore;

namespace GymManager.App.Infrastructure;

/// <summary>
/// DbContext 创建器（复用 DbContextOptions，避免每次创建都重新构建配置）。
/// </summary>
public sealed class DbContextProvider
{
    private readonly DbContextOptions<GymDbContext> _options;

    public DbContextProvider(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var builder = new DbContextOptionsBuilder<GymDbContext>();

        var provider = settings.Database.Provider.Trim();
        ProviderName = provider;

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlServer(settings.Database.SqlServer.ConnectionString);
            ConnectionDisplay = settings.Database.SqlServer.ConnectionString;
        }
        else
        {
            // 默认 SQLite
            var configured = settings.Database.Sqlite.DbPath.Trim();

            // 安装版默认写入用户目录，避免 Program Files 无写权限
            var dbFullPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(AppPaths.UserDataRoot, configured));

            Directory.CreateDirectory(Path.GetDirectoryName(dbFullPath)!);

            builder.UseSqlite($"Data Source={dbFullPath}");
            ConnectionDisplay = dbFullPath;
        }

#if DEBUG
        builder.EnableSensitiveDataLogging();
#endif

        _options = builder.Options;
    }

    public string ProviderName { get; }

    public string ConnectionDisplay { get; }

    public GymDbContext CreateDbContext() => new(_options);
}

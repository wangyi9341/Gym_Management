namespace GymManager.App.Config;

/// <summary>
/// 应用配置（从 appsettings.json 读取）。
/// </summary>
public sealed class AppSettings
{
    public DatabaseSettings Database { get; set; } = new();

    public ReminderSettings Reminder { get; set; } = new();
}

public sealed class DatabaseSettings
{
    /// <summary>
    /// 数据库提供程序：SQLite / SqlServer。
    /// </summary>
    public string Provider { get; set; } = "SQLite";

    public SqliteSettings Sqlite { get; set; } = new();

    public SqlServerSettings SqlServer { get; set; } = new();
}

public sealed class SqliteSettings
{
    /// <summary>
    /// SQLite 数据文件路径（相对于程序运行目录）。
    /// </summary>
    public string DbPath { get; set; } = @"Data\gym.db";
}

public sealed class SqlServerSettings
{
    /// <summary>
    /// SQL Server 连接字符串（仅当 Provider=SqlServer 时使用）。
    /// </summary>
    public string ConnectionString { get; set; } =
        "Server=localhost;Database=GymManager;Trusted_Connection=True;TrustServerCertificate=True";
}

public sealed class ReminderSettings
{
    /// <summary>
    /// 年卡到期提醒天数（默认：3 天）。
    /// </summary>
    public int AnnualCardExpiringDays { get; set; } = 3;

    /// <summary>
    /// 私教课“剩余课程不足”阈值（默认：3 节）。
    /// </summary>
    public int LowRemainingSessionsThreshold { get; set; } = 3;
}


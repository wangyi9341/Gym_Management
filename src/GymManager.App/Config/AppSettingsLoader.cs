using System.IO;
using System.Text.Json;
using GymManager.App.Infrastructure;

namespace GymManager.App.Config;

/// <summary>
/// appsettings.json 读取与默认生成。
/// </summary>
public static class AppSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetBaseSettingsPath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public static string GetUserSettingsPath()
        => AppPaths.GetUserPath("appsettings.json");

    public static AppSettings LoadOrCreateDefault(string? settingsPath = null)
    {
        // 规则：
        // - 开发调试：如果 EXE 同目录存在 appsettings.json，则优先使用（便于调试直接修改）
        // - 安装发布：EXE 同目录通常无写权限/不携带配置，则使用用户目录下的配置文件
        settingsPath ??= File.Exists(GetBaseSettingsPath())
            ? GetBaseSettingsPath()
            : GetUserSettingsPath();

        if (!File.Exists(settingsPath))
        {
            var defaults = new AppSettings();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }

        var json = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();

        Normalize(settings);
        return settings;
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Database.Provider = string.IsNullOrWhiteSpace(settings.Database.Provider)
            ? "SQLite"
            : settings.Database.Provider.Trim();

        if (settings.Reminder.AnnualCardExpiringDays < 0)
        {
            settings.Reminder.AnnualCardExpiringDays = 3;
        }

        if (settings.Reminder.LowRemainingSessionsThreshold < 0)
        {
            settings.Reminder.LowRemainingSessionsThreshold = 3;
        }

        settings.Database.Sqlite.DbPath = string.IsNullOrWhiteSpace(settings.Database.Sqlite.DbPath)
            ? @"Data\gym.db"
            : settings.Database.Sqlite.DbPath.Trim();
    }
}

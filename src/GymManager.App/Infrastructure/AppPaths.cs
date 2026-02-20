using System.IO;

namespace GymManager.App.Infrastructure;

/// <summary>
/// 应用运行时路径（为“安装版”做准备：配置/数据库/日志写入到用户目录，避免 Program Files 无写权限）。
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// 用户数据根目录：%LocalAppData%\GymManager
    /// </summary>
    public static string UserDataRoot
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GymManager");

    public static string GetUserPath(params string[] segments)
    {
        var path = UserDataRoot;
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    public static string EnsureUserDirectory(params string[] segments)
    {
        var path = GetUserPath(segments);
        Directory.CreateDirectory(path);
        return path;
    }
}


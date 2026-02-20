using System.IO;
using System.Text;

namespace GymManager.App.Infrastructure;

/// <summary>
/// 简易文件日志（桌面端项目优先保证可用性与易排查）。
/// </summary>
public static class AppLogger
{
    private static readonly object Sync = new();

    private static string LogFilePath
        => AppPaths.GetUserPath("Logs", "app.log");

    public static void Log(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
                File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // 避免日志失败导致程序崩溃
        }
    }

    public static void Log(Exception exception, string? context = null)
    {
        var prefix = string.IsNullOrWhiteSpace(context) ? "EX" : $"EX({context})";
        Log($"{prefix}: {exception}");
    }
}

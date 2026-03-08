using System.IO;
using System.Windows;
using GymManager.App.Config;
using GymManager.App.Infrastructure;
using GymManager.App.Services;
using GymManager.App.ViewModels;
using GymManager.Data.Db;
using MaterialDesignThemes.Wpf;

namespace GymManager.App;

/// <summary>
/// 应用程序入口（WPF）。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局异常处理（记录到 Logs/app.log）
        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Log(args.Exception, "DispatcherUnhandledException");
            MessageBox.Show(
                $"发生未处理异常，已记录到 Logs\\app.log。\n\n{args.Exception.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLogger.Log(ex, "AppDomain.UnhandledException");
            }
            else
            {
                AppLogger.Log($"AppDomain.UnhandledException: {args.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Log(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };

        // 1) 读取配置（若不存在则生成默认 appsettings.json）
        var loadedSettings = AppSettingsLoader.LoadOrCreateDefaultWithPath();
        var settings = loadedSettings.Settings;

        // 2) 构建基础服务
        var appEvents = new AppEvents();

        TryMigrateUserDatabaseToPortable(loadedSettings.SettingsPath, settings);

        var dbProvider = new DbContextProvider(settings, loadedSettings.SettingsPath);

        // 3) 初始化数据库（自动建表）
        InitializeDatabase(dbProvider);

        var snackbarQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        var toast = new SnackbarToastService(snackbarQueue);
        var dialog = new DialogService();
        var editorDialogs = new EditorDialogService();
        var fileDialogs = new FileDialogService();
        var excel = new ExcelTransferService(dbProvider);

        // 4) 业务服务
        var coachService = new CoachService(dbProvider);
        var privateTrainingService = new PrivateTrainingMemberService(dbProvider);
        var annualCardService = new AnnualCardMemberService(dbProvider);
        var dashboardService = new DashboardService(dbProvider);

        // 5) 页面 ViewModel
        var dashboardVm = new DashboardViewModel(dashboardService, settings, dialog, toast, appEvents);
        var coachesVm = new CoachesViewModel(coachService, editorDialogs, dialog, toast, appEvents);
        var privateTrainingVm = new PrivateTrainingMembersViewModel(
            privateTrainingService,
            excel,
            fileDialogs,
            editorDialogs,
            dialog,
            toast,
            appEvents,
            lowRemainingThreshold: settings.Reminder.LowRemainingSessionsThreshold);
        var annualCardVm = new AnnualCardMembersViewModel(
            annualCardService,
            excel,
            fileDialogs,
            editorDialogs,
            dialog,
            toast,
            appEvents,
            expiringDays: settings.Reminder.AnnualCardExpiringDays);

        // 6) 主窗口
        var mainVm = new MainViewModel(snackbarQueue, dashboardVm, coachesVm, privateTrainingVm, annualCardVm);
        var mainWindow = new MainWindow
        {
            DataContext = mainVm
        };

        MainWindow = mainWindow;
        mainWindow.Show();

        // 7) 异步初始化首页数据（不阻塞首屏渲染）
        _ = Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await dashboardVm.InitializeAsync();
            }
            catch (Exception ex)
            {
                dialog.Error("初始化失败", ex.Message);
                AppLogger.Log(ex, "DashboardInitialize");
            }
        });
    }

    private static void TryMigrateUserDatabaseToPortable(string settingsPath, AppSettings settings)
    {
        try
        {
            if (!settings.Database.Provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 便携模式判定：当前使用的是 EXE 同目录的 appsettings.json
            var loadedPath = Path.GetFullPath(settingsPath);
            var basePath = Path.GetFullPath(AppSettingsLoader.GetBaseSettingsPath());
            var isPortableMode = loadedPath.Equals(basePath, StringComparison.OrdinalIgnoreCase);
            if (!isPortableMode)
            {
                return;
            }

            var configured = (settings.Database.Sqlite.DbPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                return;
            }

            var settingsDir = Path.GetDirectoryName(loadedPath)!;
            var targetDbPath = Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(settingsDir, configured));

            // 程序目录已有数据库，则无需迁移
            if (File.Exists(targetDbPath))
            {
                return;
            }

            // 尝试从用户目录（安装版默认位置）定位旧数据库
            var sourceDbPath = ResolveUserSqliteDbPath();
            if (!File.Exists(sourceDbPath))
            {
                return;
            }

            var message =
                "检测到旧数据数据库（用户目录）：\n" +
                $"{sourceDbPath}\n\n" +
                "当前程序处于“便携模式”，数据库将保存到程序目录：\n" +
                $"{targetDbPath}\n\n" +
                "是否将旧数据复制到程序目录？\n\n" +
                "复制后：你只要复制整个程序文件夹到新电脑，数据就不会丢失。";

            var ok = MessageBox.Show(message, "数据迁移", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetDbPath)!);

            CopyIfExists(sourceDbPath, targetDbPath);
            CopyIfExists($"{sourceDbPath}-wal", $"{targetDbPath}-wal");
            CopyIfExists($"{sourceDbPath}-shm", $"{targetDbPath}-shm");

            MessageBox.Show(
                "数据迁移完成。\n\n你可以继续使用当前程序目录下的数据库了。",
                "数据迁移",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "TryMigrateUserDatabaseToPortable");
            MessageBox.Show(
                $"数据迁移失败：{ex.Message}\n\n你仍然可以继续使用软件（将创建新数据库）。",
                "数据迁移失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        static void CopyIfExists(string source, string destination)
        {
            if (!File.Exists(source))
            {
                return;
            }

            File.Copy(source, destination, overwrite: false);
        }

        static string ResolveUserSqliteDbPath()
        {
            try
            {
                var userSettingsPath = AppSettingsLoader.GetUserSettingsPath();
                if (!File.Exists(userSettingsPath))
                {
                    return Path.GetFullPath(Path.Combine(AppPaths.UserDataRoot, "Data", "gym.db"));
                }

                var userSettings = AppSettingsLoader.LoadOrCreateDefault(userSettingsPath);
                if (!userSettings.Database.Provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(Path.Combine(AppPaths.UserDataRoot, "Data", "gym.db"));
                }

                var configured = (userSettings.Database.Sqlite.DbPath ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(configured))
                {
                    return Path.GetFullPath(Path.Combine(AppPaths.UserDataRoot, "Data", "gym.db"));
                }

                var userDir = Path.GetDirectoryName(Path.GetFullPath(userSettingsPath))!;
                return Path.IsPathRooted(configured)
                    ? configured
                    : Path.GetFullPath(Path.Combine(userDir, configured));
            }
            catch
            {
                return Path.GetFullPath(Path.Combine(AppPaths.UserDataRoot, "Data", "gym.db"));
            }
        }
    }

    private static void InitializeDatabase(DbContextProvider dbProvider)
    {
        try
        {
            using var db = dbProvider.CreateDbContext();
            DbInitializer.EnsureCreatedAsync(db).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "InitializeDatabase");
            var message =
                "数据库初始化失败。\n\n" +
                $"数据库位置：{dbProvider.ConnectionDisplay}\n\n" +
                $"请检查 appsettings.json 数据库配置与权限。\n\n" +
                $"错误：{ex.Message}";

            MessageBox.Show(message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown(-1);
        }
    }
}

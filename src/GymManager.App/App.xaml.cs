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
        var settings = AppSettingsLoader.LoadOrCreateDefault();

        // 2) 构建基础服务
        var appEvents = new AppEvents();
        var dbProvider = new DbContextProvider(settings);

        // 3) 初始化数据库（自动建表）
        InitializeDatabase(dbProvider);

        var snackbarQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
        var toast = new SnackbarToastService(snackbarQueue);
        var dialog = new DialogService();
        var editorDialogs = new EditorDialogService();

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
            editorDialogs,
            dialog,
            toast,
            appEvents,
            lowRemainingThreshold: settings.Reminder.LowRemainingSessionsThreshold);
        var annualCardVm = new AnnualCardMembersViewModel(
            annualCardService,
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
                $"请检查 appsettings.json 数据库配置与权限。\n\n" +
                $"错误：{ex.Message}";

            MessageBox.Show(message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown(-1);
        }
    }
}

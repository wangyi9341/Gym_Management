using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System.Windows.Threading;

namespace GymManager.App.ViewModels;

/// <summary>
/// 主窗口 ViewModel：负责导航、顶部仪表盘摘要、全局提示等。
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly DispatcherTimer _clockTimer;

    public MainViewModel(
        SnackbarMessageQueue snackbarQueue,
        DashboardViewModel dashboard,
        CoachesViewModel coaches,
        PrivateTrainingMembersViewModel privateTrainingMembers,
        AnnualCardMembersViewModel annualCardMembers)
    {
        Dashboard = dashboard;
        Coaches = coaches;
        PrivateTrainingMembers = privateTrainingMembers;
        AnnualCardMembers = annualCardMembers;

        CurrentPage = Dashboard;

        SnackbarQueue = snackbarQueue;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => Now = DateTime.Now;
        _clockTimer.Start();
    }

    public SnackbarMessageQueue SnackbarQueue { get; }

    public DashboardViewModel Dashboard { get; }
    public CoachesViewModel Coaches { get; }
    public PrivateTrainingMembersViewModel PrivateTrainingMembers { get; }
    public AnnualCardMembersViewModel AnnualCardMembers { get; }

    [ObservableProperty]
    private DateTime now = DateTime.Now;

    [ObservableProperty]
    private ViewModelBase? currentPage;

    [RelayCommand]
    private async Task NavigateDashboard()
    {
        CurrentPage = Dashboard;
        await Dashboard.InitializeAsync();
    }

    [RelayCommand]
    private async Task NavigateCoaches()
    {
        CurrentPage = Coaches;
        await Coaches.InitializeAsync();
    }

    [RelayCommand]
    private async Task NavigatePrivateTrainingMembers()
    {
        CurrentPage = PrivateTrainingMembers;
        await PrivateTrainingMembers.InitializeAsync();
    }

    [RelayCommand]
    private async Task NavigateAnnualCardMembers()
    {
        CurrentPage = AnnualCardMembers;
        await AnnualCardMembers.InitializeAsync();
    }

    [RelayCommand]
    private async Task GoToExpiringAnnualCards()
    {
        AnnualCardMembers.SelectedFilter = AnnualCardFilter.ExpiringSoon;
        CurrentPage = AnnualCardMembers;
        await AnnualCardMembers.InitializeAsync();
    }

    public void Notify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        SnackbarQueue.Enqueue(message);
    }
}

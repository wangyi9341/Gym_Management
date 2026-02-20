using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymManager.App.Config;
using GymManager.App.Infrastructure;
using GymManager.App.Services;
using GymManager.Domain.Entities;
using System.Windows.Threading;

namespace GymManager.App.ViewModels;

/// <summary>
/// 首页仪表盘 ViewModel：聚合关键运营数据，并定时刷新到期提醒。
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _service;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly AppEvents _events;
    private readonly DispatcherTimer _timer;

    private DateTime _lastReminderDate = DateTime.MinValue;

    public DashboardViewModel(
        DashboardService service,
        AppSettings settings,
        IDialogService dialog,
        IToastService toast,
        AppEvents events)
    {
        _service = service;
        _settings = settings;
        _dialog = dialog;
        _toast = toast;
        _events = events;

        _events.DataChanged += async (_, _) => await RefreshAsync();

        // 定时刷新（避免到期提醒过期/新增时不更新）
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    public int AnnualCardExpiringDays => _settings.Reminder.AnnualCardExpiringDays;

    public int LowRemainingThreshold => _settings.Reminder.LowRemainingSessionsThreshold;

    public ObservableCollection<AnnualCardMember> ExpiringAnnualCards { get; } = new();
    public ObservableCollection<PrivateTrainingMember> LowRemainingSessionsMembers { get; } = new();

    [ObservableProperty] private int coachCount;
    [ObservableProperty] private int privateTrainingMemberCount;
    [ObservableProperty] private int annualCardMemberCount;
    [ObservableProperty] private int annualCardExpiringCount;
    [ObservableProperty] private int annualCardExpiredCount;
    [ObservableProperty] private int lowRemainingSessionsCount;
    [ObservableProperty] private bool isLoading;

    public Task InitializeAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;

            var snapshot = await _service.GetSnapshotAsync(
                annualCardExpiringDays: _settings.Reminder.AnnualCardExpiringDays,
                lowRemainingThreshold: _settings.Reminder.LowRemainingSessionsThreshold);

            CoachCount = snapshot.CoachCount;
            PrivateTrainingMemberCount = snapshot.PrivateTrainingMemberCount;
            AnnualCardMemberCount = snapshot.AnnualCardMemberCount;
            AnnualCardExpiringCount = snapshot.AnnualCardExpiringCount;
            AnnualCardExpiredCount = snapshot.AnnualCardExpiredCount;
            LowRemainingSessionsCount = snapshot.LowRemainingSessionsCount;

            ExpiringAnnualCards.Clear();
            foreach (var item in snapshot.ExpiringAnnualCards)
            {
                ExpiringAnnualCards.Add(item);
            }

            LowRemainingSessionsMembers.Clear();
            foreach (var item in snapshot.LowRemainingSessionsMembers)
            {
                LowRemainingSessionsMembers.Add(item);
            }

            // 到期提醒：一天提示一次（避免频繁打扰）
            if (AnnualCardExpiringCount > 0 && _lastReminderDate.Date != DateTime.Today)
            {
                _toast.Show($"提醒：有 {AnnualCardExpiringCount} 位年卡会员在 {_settings.Reminder.AnnualCardExpiringDays} 天内到期。");
                _lastReminderDate = DateTime.Today;
            }
        }
        catch (Exception ex)
        {
            _dialog.Error("加载失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

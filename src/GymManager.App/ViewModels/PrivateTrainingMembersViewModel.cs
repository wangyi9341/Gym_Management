using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymManager.App.Infrastructure;
using GymManager.App.Services;
using GymManager.Domain.Entities;
using GymManager.Domain.Exceptions;

namespace GymManager.App.ViewModels;

/// <summary>
/// 私教课会员管理模块 ViewModel（CRUD + 费用记录 + 课程消耗记录）。
/// </summary>
public sealed partial class PrivateTrainingMembersViewModel : ViewModelBase
{
    private readonly PrivateTrainingMemberService _service;
    private readonly IEditorDialogService _editorDialogs;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly AppEvents _events;
    private readonly int _lowRemainingThreshold;

    private CancellationTokenSource? _loadDetailCts;

    public PrivateTrainingMembersViewModel(
        PrivateTrainingMemberService service,
        IEditorDialogService editorDialogs,
        IDialogService dialog,
        IToastService toast,
        AppEvents events,
        int lowRemainingThreshold)
    {
        _service = service;
        _editorDialogs = editorDialogs;
        _dialog = dialog;
        _toast = toast;
        _events = events;
        _lowRemainingThreshold = Math.Max(0, lowRemainingThreshold);

        _events.DataChanged += async (_, _) => await RefreshAsync();
    }

    public int LowRemainingThreshold => _lowRemainingThreshold;

    public ObservableCollection<PrivateTrainingMember> Members { get; } = new();
    public ObservableCollection<PrivateTrainingFeeRecord> FeeRecords { get; } = new();
    public ObservableCollection<PrivateTrainingSessionRecord> SessionRecords { get; } = new();

    [ObservableProperty]
    private string keyword = string.Empty;

    [ObservableProperty]
    private PrivateTrainingMember? selectedMember;

    [ObservableProperty]
    private bool isLoading;

    public Task InitializeAsync() => RefreshAsync();

    private bool HasSelection() => SelectedMember is not null && !IsLoading;

    partial void OnSelectedMemberChanged(PrivateTrainingMember? value)
    {
        AddFeeCommand.NotifyCanExecuteChanged();
        ConsumeSessionsCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();

        _ = LoadSelectedMemberDetailsAsync();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        AddFeeCommand.NotifyCanExecuteChanged();
        ConsumeSessionsCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var keepSelectedId = SelectedMember?.Id;

        try
        {
            IsLoading = true;
            var list = await _service.SearchAsync(Keyword);

            Members.Clear();
            foreach (var item in list)
            {
                Members.Add(item);
            }

            if (keepSelectedId is not null)
            {
                SelectedMember = Members.FirstOrDefault(x => x.Id == keepSelectedId.Value);
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

    private async Task LoadSelectedMemberDetailsAsync()
    {
        _loadDetailCts?.Cancel();
        _loadDetailCts = new CancellationTokenSource();
        var token = _loadDetailCts.Token;

        FeeRecords.Clear();
        SessionRecords.Clear();

        if (SelectedMember is null)
        {
            return;
        }

        try
        {
            var memberId = SelectedMember.Id;

            var feesTask = _service.GetFeeRecordsAsync(memberId, token);
            var sessionsTask = _service.GetSessionRecordsAsync(memberId, token);

            await Task.WhenAll(feesTask, sessionsTask).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var fee in feesTask.Result)
            {
                FeeRecords.Add(fee);
            }

            foreach (var session in sessionsTask.Result)
            {
                SessionRecords.Add(session);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _dialog.Error("加载失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var result = _editorDialogs.ShowPrivateTrainingMemberEditor(existing: null);
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.CreateAsync(
                result.Name,
                result.Gender,
                result.Phone,
                result.InitialPaidAmount,
                result.TotalSessions);

            _toast.Show("已新增私教课会员。");
            _events.RaiseDataChanged();
        }
        catch (DomainValidationException ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync()
    {
        if (SelectedMember is null)
        {
            return;
        }

        var result = _editorDialogs.ShowPrivateTrainingMemberEditor(SelectedMember);
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.UpdateAsync(
                SelectedMember.Id,
                result.Name,
                result.Gender,
                result.Phone,
                result.TotalSessions);

            _toast.Show("已保存会员信息。");
            _events.RaiseDataChanged();
        }
        catch (DomainValidationException ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (SelectedMember is null)
        {
            return;
        }

        var ok = _dialog.Confirm("删除确认", $"确定要删除私教课会员：{SelectedMember.Name} 吗？\n\n注意：该会员的费用记录与消课记录也会被删除。");
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.DeleteAsync(SelectedMember.Id);

            SelectedMember = null;
            _toast.Show("已删除私教课会员。");
            _events.RaiseDataChanged();
        }
        catch (Exception ex)
        {
            _dialog.Error("删除失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task AddFeeAsync()
    {
        if (SelectedMember is null)
        {
            return;
        }

        var result = _editorDialogs.ShowFeeRecordEditor();
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.AddFeeAsync(SelectedMember.Id, result.Amount, result.PaidAt, result.Note);

            _toast.Show("已新增缴费记录。");
            _events.RaiseDataChanged();
        }
        catch (DomainValidationException ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ConsumeSessionsAsync()
    {
        if (SelectedMember is null)
        {
            return;
        }

        var result = _editorDialogs.ShowSessionConsumeEditor();
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.ConsumeSessionsAsync(SelectedMember.Id, result.SessionsUsed, result.UsedAt, result.Note);

            _toast.Show("已新增消课记录。");
            _events.RaiseDataChanged();
        }
        catch (DomainValidationException ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.Error("保存失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}


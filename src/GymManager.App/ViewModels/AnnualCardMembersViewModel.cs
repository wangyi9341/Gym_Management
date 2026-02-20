using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymManager.App.Infrastructure;
using GymManager.App.Services;
using GymManager.Domain.Entities;
using GymManager.Domain.Enums;
using GymManager.Domain.Exceptions;

namespace GymManager.App.ViewModels;

public enum AnnualCardFilter
{
    All = 0,
    Normal = 1,
    ExpiringSoon = 2,
    Expired = 3
}

/// <summary>
/// 年卡会员管理模块 ViewModel（CRUD + 到期提醒 + 续费）。
/// </summary>
public sealed partial class AnnualCardMembersViewModel : ViewModelBase
{
    private readonly AnnualCardMemberService _service;
    private readonly IEditorDialogService _editorDialogs;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly AppEvents _events;
    private readonly int _expiringDays;

    public AnnualCardMembersViewModel(
        AnnualCardMemberService service,
        IEditorDialogService editorDialogs,
        IDialogService dialog,
        IToastService toast,
        AppEvents events,
        int expiringDays)
    {
        _service = service;
        _editorDialogs = editorDialogs;
        _dialog = dialog;
        _toast = toast;
        _events = events;
        _expiringDays = Math.Max(0, expiringDays);

        FilterOptions = new[]
        {
            AnnualCardFilter.All,
            AnnualCardFilter.Normal,
            AnnualCardFilter.ExpiringSoon,
            AnnualCardFilter.Expired
        };

        _events.DataChanged += async (_, _) => await RefreshAsync();
    }

    public int ExpiringDays => _expiringDays;

    public IReadOnlyList<AnnualCardFilter> FilterOptions { get; }

    public ObservableCollection<AnnualCardMember> Members { get; } = new();

    [ObservableProperty]
    private string keyword = string.Empty;

    [ObservableProperty]
    private AnnualCardFilter selectedFilter = AnnualCardFilter.All;

    [ObservableProperty]
    private AnnualCardMember? selectedMember;

    [ObservableProperty]
    private bool isLoading;

    public Task InitializeAsync() => RefreshAsync();

    private bool HasSelection() => SelectedMember is not null && !IsLoading;

    partial void OnSelectedFilterChanged(AnnualCardFilter value)
    {
        _ = RefreshAsync();
    }

    partial void OnSelectedMemberChanged(AnnualCardMember? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RenewCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RenewCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var keepSelectedId = SelectedMember?.Id;

        try
        {
            IsLoading = true;
            var list = await _service.SearchAsync(Keyword);

            var filtered = list.Where(x =>
            {
                var status = x.GetStatus(DateTime.Today, _expiringDays);
                return SelectedFilter switch
                {
                    AnnualCardFilter.All => true,
                    AnnualCardFilter.Normal => status == AnnualCardStatus.Normal,
                    AnnualCardFilter.ExpiringSoon => status == AnnualCardStatus.ExpiringSoon,
                    AnnualCardFilter.Expired => status == AnnualCardStatus.Expired,
                    _ => true
                };
            }).ToList();

            Members.Clear();
            foreach (var item in filtered)
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

    [RelayCommand]
    private async Task AddAsync()
    {
        var result = _editorDialogs.ShowAnnualCardMemberEditor(existing: null);
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.CreateAsync(result.Name, result.Gender, result.Phone, result.StartDate, result.EndDate);

            _toast.Show("已新增年卡会员。");
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

        var result = _editorDialogs.ShowAnnualCardMemberEditor(SelectedMember);
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.UpdateAsync(SelectedMember.Id, result.Name, result.Gender, result.Phone, result.StartDate, result.EndDate);

            _toast.Show("已保存年卡会员信息。");
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
    private async Task RenewAsync()
    {
        if (SelectedMember is null)
        {
            return;
        }

        var ok = _dialog.Confirm("续费确认", $"确定为会员：{SelectedMember.Name} 办理年卡续费吗？\n\n规则：未过期顺延 1 年，已过期从今天重新开通 1 年。");
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.RenewAsync(SelectedMember.Id);

            _toast.Show("续费成功。");
            _events.RaiseDataChanged();
        }
        catch (DomainValidationException ex)
        {
            _dialog.Error("续费失败", ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.Error("续费失败", ex.Message);
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

        var ok = _dialog.Confirm("删除确认", $"确定要删除年卡会员：{SelectedMember.Name} 吗？");
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.DeleteAsync(SelectedMember.Id);

            SelectedMember = null;
            _toast.Show("已删除年卡会员。");
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
}


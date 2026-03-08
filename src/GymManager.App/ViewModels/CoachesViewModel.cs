using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymManager.App.Infrastructure;
using GymManager.App.Services;
using GymManager.Domain.Entities;
using GymManager.Domain.Exceptions;

namespace GymManager.App.ViewModels;

/// <summary>
/// 教练管理模块 ViewModel（CRUD）。
/// </summary>
public sealed partial class CoachesViewModel : ViewModelBase
{
    private readonly CoachService _service;
    private readonly IEditorDialogService _editorDialogs;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly AppEvents _events;

    private IReadOnlyList<Coach> _selectedCoaches = Array.Empty<Coach>();

    public CoachesViewModel(
        CoachService service,
        IEditorDialogService editorDialogs,
        IDialogService dialog,
        IToastService toast,
        AppEvents events)
    {
        _service = service;
        _editorDialogs = editorDialogs;
        _dialog = dialog;
        _toast = toast;
        _events = events;

        _events.DataChanged += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<Coach> Coaches { get; } = new();

    public IReadOnlyList<Coach> SelectedCoaches
    {
        get => _selectedCoaches;
        set
        {
            _selectedCoaches = value ?? Array.Empty<Coach>();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty]
    private string keyword = string.Empty;

    [ObservableProperty]
    private Coach? selectedCoach;

    [ObservableProperty]
    private bool isLoading;

    public Task InitializeAsync() => RefreshAsync();

    private bool CanEdit() => SelectedCoach is not null && SelectedCoaches.Count <= 1 && !IsLoading;

    private bool CanDelete() => (SelectedCoach is not null || SelectedCoaches.Count > 0) && !IsLoading;

    partial void OnSelectedCoachChanged(Coach? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            var list = await _service.SearchAsync(Keyword);

            Coaches.Clear();
            foreach (var item in list)
            {
                Coaches.Add(item);
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
        var result = _editorDialogs.ShowCoachEditor(existing: null);
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.CreateAsync(result.EmployeeNo, result.Name);

            _toast.Show("已新增教练。");
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

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task EditAsync()
    {
        if (SelectedCoach is null)
        {
            return;
        }

        var result = _editorDialogs.ShowCoachEditor(SelectedCoach);
        if (result is null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.UpdateAsync(result.EmployeeNo, result.Name);

            _toast.Show("已保存教练信息。");
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

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var targets = SelectedCoaches.Count > 0
            ? SelectedCoaches
            : SelectedCoach is null
                ? Array.Empty<Coach>()
                : new[] { SelectedCoach };

        if (targets.Count == 0)
        {
            return;
        }

        var confirmText = targets.Count == 1
            ? $"确定要删除教练：{targets[0].Name}（工号 {targets[0].EmployeeNo}）吗？"
            : $"确定要删除选中的 {targets.Count} 个教练吗？\n\n" +
              string.Join("\n", targets.Take(10).Select(x => $"{x.Name}（工号 {x.EmployeeNo}）")) +
              (targets.Count > 10 ? "\n..." : string.Empty);

        var ok = _dialog.Confirm("删除确认", confirmText);
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;

            if (targets.Count == 1)
            {
                await _service.DeleteAsync(targets[0].EmployeeNo);
                _toast.Show("已删除教练。");
            }
            else
            {
                var deleted = await _service.DeleteManyAsync(targets.Select(x => x.EmployeeNo));
                _toast.Show($"已删除 {deleted} 个教练。");
            }

            SelectedCoach = null;
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

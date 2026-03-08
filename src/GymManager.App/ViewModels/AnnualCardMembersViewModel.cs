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
    Paused = 2,
    ExpiringSoon = 3,
    Expired = 4
}

/// <summary>
/// 年卡会员管理模块 ViewModel（CRUD + 到期提醒 + 续费）。
/// </summary>
public sealed partial class AnnualCardMembersViewModel : ViewModelBase
{
    private readonly AnnualCardMemberService _service;
    private readonly ExcelTransferService _excel;
    private readonly IFileDialogService _fileDialogs;
    private readonly IEditorDialogService _editorDialogs;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly AppEvents _events;
    private readonly int _expiringDays;

    private IReadOnlyList<AnnualCardMember> _selectedMembers = Array.Empty<AnnualCardMember>();
    private CancellationTokenSource? _loadDetailCts;

    public AnnualCardMembersViewModel(
        AnnualCardMemberService service,
        ExcelTransferService excel,
        IFileDialogService fileDialogs,
        IEditorDialogService editorDialogs,
        IDialogService dialog,
        IToastService toast,
        AppEvents events,
        int expiringDays)
    {
        _service = service;
        _excel = excel;
        _fileDialogs = fileDialogs;
        _editorDialogs = editorDialogs;
        _dialog = dialog;
        _toast = toast;
        _events = events;
        _expiringDays = Math.Max(0, expiringDays);

        FilterOptions = new[]
        {
            AnnualCardFilter.All,
            AnnualCardFilter.Normal,
            AnnualCardFilter.Paused,
            AnnualCardFilter.ExpiringSoon,
            AnnualCardFilter.Expired
        };

        _events.DataChanged += async (_, _) => await RefreshAsync();
    }

    public int ExpiringDays => _expiringDays;

    public IReadOnlyList<AnnualCardFilter> FilterOptions { get; }

    public ObservableCollection<AnnualCardMember> Members { get; } = new();
    public ObservableCollection<AnnualCardPauseRecordRow> PauseRecords { get; } = new();
    public ObservableCollection<AnnualCardRenewRecordRow> RenewRecords { get; } = new();

    public IReadOnlyList<AnnualCardMember> SelectedMembers
    {
        get => _selectedMembers;
        set
        {
            _selectedMembers = value ?? Array.Empty<AnnualCardMember>();

            PauseCommand.NotifyCanExecuteChanged();
        }
    }

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

    private bool CanPause()
    {
        if (IsLoading || SelectedMember is null)
        {
            return false;
        }

        if (SelectedMembers.Count > 1)
        {
            return false;
        }

        if (SelectedMember.EndDate.Date < DateTime.Today)
        {
            return false;
        }

        var status = SelectedMember.GetStatus(DateTime.Today, _expiringDays);
        return status != AnnualCardStatus.Paused;
    }

    partial void OnSelectedFilterChanged(AnnualCardFilter value)
    {
        _ = RefreshAsync();
    }

    partial void OnSelectedMemberChanged(AnnualCardMember? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RenewCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();

        _ = LoadSelectedMemberDetailsAsync();
    }

    partial void OnIsLoadingChanged(bool value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RenewCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
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
                    AnnualCardFilter.Paused => status == AnnualCardStatus.Paused,
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

    private async Task LoadSelectedMemberDetailsAsync()
    {
        _loadDetailCts?.Cancel();
        _loadDetailCts = new CancellationTokenSource();
        var token = _loadDetailCts.Token;

        PauseRecords.Clear();
        RenewRecords.Clear();

        if (SelectedMember is null)
        {
            return;
        }

        try
        {
            var memberId = SelectedMember.Id;
            var pausesTask = _service.GetPauseRecordsAsync(memberId, token);
            var renewsTask = _service.GetRenewRecordsAsync(memberId, token);

            await Task.WhenAll(pausesTask, renewsTask).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var item in pausesTask.Result)
            {
                PauseRecords.Add(new AnnualCardPauseRecordRow(item, today: DateTime.Today));
            }

            foreach (var item in renewsTask.Result)
            {
                RenewRecords.Add(new AnnualCardRenewRecordRow(item));
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

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        if (SelectedMember is null)
        {
            return;
        }

        if (SelectedMembers.Count > 1)
        {
            _dialog.Info("停卡提示", "停卡一次只能操作 1 个会员，请只选择一个会员。");
            return;
        }

        var result = _editorDialogs.ShowAnnualCardPauseEditor(SelectedMember);
        if (result is null)
        {
            return;
        }

        var ok = _dialog.Confirm(
            "停卡确认",
            $"确定为会员：{SelectedMember.Name} 停卡 {result.PauseDays} 天吗？\n\n" +
            "规则：停卡按约定天数生效，到期后自动恢复。截止日期会顺延相同天数。");
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _service.PauseAsync(SelectedMember.Id, result.PauseDays);

            _toast.Show("停卡成功。");
            _events.RaiseDataChanged();
        }
        catch (DomainValidationException ex)
        {
            _dialog.Error("停卡失败", ex.Message);
        }
        catch (Exception ex)
        {
            _dialog.Error("停卡失败", ex.Message);
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

    private bool CanImportOrExport() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanImportOrExport))]
    private async Task ExportAsync()
    {
        var defaultName = $"年卡会员_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var path = _fileDialogs.ShowSaveExcelFileDialog("导出年卡会员", defaultName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _excel.ExportAnnualCardMembersAsync(path);
            _toast.Show("已导出年卡会员到 Excel。");
        }
        catch (Exception ex)
        {
            _dialog.Error("导出失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanImportOrExport))]
    private async Task ImportAsync()
    {
        var path = _fileDialogs.ShowOpenExcelFileDialog("导入年卡会员（Excel）");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var ok = _dialog.Confirm(
            "导入确认",
            $"将从以下 Excel 导入年卡会员（新增+更新）以及停卡/续费记录：\n\n{path}\n\n是否继续？");
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var result = await _excel.ImportAnnualCardMembersAsync(path);

            var message =
                "导入完成。\n\n" +
                $"[年卡会员]\n新增：{result.Added}\n更新：{result.Updated}\n跳过：{result.Skipped}\n\n" +
                $"[停卡记录]\n新增：{result.PauseRecordsAdded}\n跳过：{result.PauseRecordsSkipped}\n\n" +
                $"[续费记录]\n新增：{result.RenewRecordsAdded}\n跳过：{result.RenewRecordsSkipped}";
            if (result.Errors.Count > 0)
            {
                var preview = string.Join("\n", result.Errors.Take(10));
                message += $"\n\n部分行未导入（最多显示 10 条）：\n{preview}";
            }

            _dialog.Info("导入结果", message);
            _events.RaiseDataChanged();
        }
        catch (Exception ex)
        {
            _dialog.Error("导入失败", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

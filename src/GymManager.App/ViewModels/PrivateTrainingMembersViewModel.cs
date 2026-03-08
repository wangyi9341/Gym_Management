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
    private readonly ExcelTransferService _excel;
    private readonly IFileDialogService _fileDialogs;
    private readonly IEditorDialogService _editorDialogs;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly AppEvents _events;
    private readonly int _lowRemainingThreshold;

    private CancellationTokenSource? _loadDetailCts;

    public PrivateTrainingMembersViewModel(
        PrivateTrainingMemberService service,
        ExcelTransferService excel,
        IFileDialogService fileDialogs,
        IEditorDialogService editorDialogs,
        IDialogService dialog,
        IToastService toast,
        AppEvents events,
        int lowRemainingThreshold)
    {
        _service = service;
        _excel = excel;
        _fileDialogs = fileDialogs;
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

    private bool CanImportOrExport() => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanImportOrExport))]
    private async Task ExportAsync()
    {
        var defaultName = $"私教会员_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        var path = _fileDialogs.ShowSaveExcelFileDialog("导出私教会员", defaultName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            IsLoading = true;
            await _excel.ExportPrivateTrainingMembersAsync(path);
            _toast.Show("已导出私教会员到 Excel。");
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
        var path = _fileDialogs.ShowOpenExcelFileDialog("导入私教会员（Excel）");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var overwrite = _dialog.Confirm(
            "导入模式",
            "请选择导入方式：\n\n是：覆盖导入（清空所有私教会员/缴费记录/消课记录后再导入）\n否：追加导入（会员新增+更新；缴费/消课追加导入，自动跳过重复记录）");

        if (overwrite)
        {
            var okDanger = _dialog.Confirm(
                "覆盖导入二次确认",
                "覆盖导入会清空【所有私教课会员】以及对应的【缴费记录/消课记录】。\n\n强烈建议先导出备份。\n\n是否继续？");
            if (!okDanger)
            {
                return;
            }
        }

        var modeText = overwrite ? "覆盖导入（清空后导入）" : "追加导入（新增+更新+追加明细）";
        var ok = _dialog.Confirm(
            "导入确认",
            $"将从以下 Excel 导入私教数据：\n\n{path}\n\n导入方式：{modeText}\n\n是否继续？");
        if (!ok)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var result = await _excel.ImportPrivateTrainingMembersAsync(path, overwriteExisting: overwrite);

            var message =
                $"导入完成。\n\n会员：新增 {result.MembersAdded}，更新 {result.MembersUpdated}，跳过 {result.MembersSkipped}" +
                $"\n缴费记录：新增 {result.FeeRecordsAdded}，跳过 {result.FeeRecordsSkipped}" +
                $"\n消课记录：新增 {result.SessionRecordsAdded}，跳过 {result.SessionRecordsSkipped}";

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

using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GymManager.App.Dialogs;

public sealed record CoachEditResult(string EmployeeNo, string Name);

/// <summary>
/// 教练新增/编辑弹窗 ViewModel。
/// </summary>
public sealed partial class CoachEditViewModel : DialogViewModelBase
{
    public CoachEditViewModel(string? employeeNo = null, string? name = null)
    {
        EmployeeNo = (employeeNo ?? string.Empty).Trim();
        Name = (name ?? string.Empty).Trim();

        IsEditMode = !string.IsNullOrWhiteSpace(employeeNo);
        ValidateAllProperties();
    }

    public bool IsEditMode { get; }

    public bool IsEmployeeNoReadOnly => IsEditMode;

    public CoachEditResult? Result { get; private set; }

    [ObservableProperty]
    [Required(ErrorMessage = "工号不能为空")]
    [MaxLength(32, ErrorMessage = "工号长度不能超过 32")]
    private string employeeNo = string.Empty;

    [ObservableProperty]
    [Required(ErrorMessage = "姓名不能为空")]
    [MaxLength(50, ErrorMessage = "姓名长度不能超过 50")]
    private string name = string.Empty;

    partial void OnEmployeeNoChanged(string value) => ValidateProperty(value, nameof(EmployeeNo));
    partial void OnNameChanged(string value) => ValidateProperty(value, nameof(Name));

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        Result = new CoachEditResult(EmployeeNo.Trim(), Name.Trim());
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}


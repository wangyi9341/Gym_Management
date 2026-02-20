using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymManager.Domain.Enums;

namespace GymManager.App.Dialogs;

public sealed record AnnualCardMemberEditResult(
    string Name,
    Gender Gender,
    string Phone,
    DateTime StartDate,
    DateTime EndDate);

/// <summary>
/// 年卡会员新增/编辑弹窗 ViewModel。
/// </summary>
public sealed partial class AnnualCardMemberEditViewModel : DialogViewModelBase
{
    public AnnualCardMemberEditViewModel(
        bool isEditMode,
        string? name = null,
        Gender gender = Gender.Unknown,
        string? phone = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        IsEditMode = isEditMode;

        Name = (name ?? string.Empty).Trim();
        Gender = gender;
        Phone = (phone ?? string.Empty).Trim();
        StartDate = (startDate ?? DateTime.Today).Date;
        EndDate = (endDate ?? DateTime.Today.AddYears(1)).Date;

        ValidateAllProperties();
    }

    public bool IsEditMode { get; }

    public IReadOnlyList<Gender> GenderOptions { get; } = new[] { Gender.Unknown, Gender.Male, Gender.Female };

    public AnnualCardMemberEditResult? Result { get; private set; }

    [ObservableProperty]
    [Required(ErrorMessage = "姓名不能为空")]
    [MaxLength(50, ErrorMessage = "姓名长度不能超过 50")]
    private string name = string.Empty;

    [ObservableProperty]
    private Gender gender = Gender.Unknown;

    [ObservableProperty]
    [Required(ErrorMessage = "电话号不能为空")]
    [MaxLength(20, ErrorMessage = "电话号长度不能超过 20")]
    private string phone = string.Empty;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today.AddYears(1);

    partial void OnNameChanged(string value) => ValidateProperty(value, nameof(Name));
    partial void OnPhoneChanged(string value) => ValidateProperty(value, nameof(Phone));

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        if (EndDate.Date < StartDate.Date)
        {
            // 使用属性错误提示会比较重，这里直接阻断保存。
            return;
        }

        Result = new AnnualCardMemberEditResult(
            Name.Trim(),
            Gender,
            Phone.Trim(),
            StartDate.Date,
            EndDate.Date);

        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}


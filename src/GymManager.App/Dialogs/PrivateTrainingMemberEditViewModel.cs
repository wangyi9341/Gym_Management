using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GymManager.Domain.Enums;

namespace GymManager.App.Dialogs;

public sealed record PrivateTrainingMemberEditResult(
    string Name,
    Gender Gender,
    string Phone,
    decimal InitialPaidAmount,
    int TotalSessions);

/// <summary>
/// 私教课会员新增/编辑弹窗 ViewModel。
/// </summary>
public sealed partial class PrivateTrainingMemberEditViewModel : DialogViewModelBase
{
    public PrivateTrainingMemberEditViewModel(
        bool isEditMode,
        string? name = null,
        Gender gender = Gender.Unknown,
        string? phone = null,
        int totalSessions = 0,
        decimal initialPaidAmount = 0)
    {
        IsEditMode = isEditMode;

        Name = (name ?? string.Empty).Trim();
        Gender = gender;
        Phone = (phone ?? string.Empty).Trim();
        TotalSessions = totalSessions;
        InitialPaidAmount = initialPaidAmount;

        ValidateAllProperties();
    }

    public bool IsEditMode { get; }

    public IReadOnlyList<Gender> GenderOptions { get; } = new[] { Gender.Unknown, Gender.Male, Gender.Female };

    public PrivateTrainingMemberEditResult? Result { get; private set; }

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
    [Range(0, int.MaxValue, ErrorMessage = "总课程数不能为负数")]
    private int totalSessions;

    [ObservableProperty]
    [Range(0, double.MaxValue, ErrorMessage = "已交费用不能为负数")]
    private decimal initialPaidAmount;

    partial void OnNameChanged(string value) => ValidateProperty(value, nameof(Name));
    partial void OnPhoneChanged(string value) => ValidateProperty(value, nameof(Phone));
    partial void OnTotalSessionsChanged(int value) => ValidateProperty(value, nameof(TotalSessions));
    partial void OnInitialPaidAmountChanged(decimal value) => ValidateProperty(value, nameof(InitialPaidAmount));

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        Result = new PrivateTrainingMemberEditResult(
            Name.Trim(),
            Gender,
            Phone.Trim(),
            InitialPaidAmount,
            TotalSessions);

        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}


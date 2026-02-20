using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GymManager.App.Dialogs;

public sealed record FeeRecordEditResult(decimal Amount, DateTime PaidAt, string? Note);

/// <summary>
/// 缴费记录弹窗 ViewModel。
/// </summary>
public sealed partial class FeeRecordEditViewModel : DialogViewModelBase
{
    public FeeRecordEditViewModel()
    {
        PaidAt = DateTime.Now;
        ValidateAllProperties();
    }

    public FeeRecordEditResult? Result { get; private set; }

    [ObservableProperty]
    [Range(0.01, double.MaxValue, ErrorMessage = "金额必须大于 0")]
    private decimal amount;

    [ObservableProperty]
    private DateTime paidAt;

    [ObservableProperty]
    [MaxLength(200, ErrorMessage = "备注长度不能超过 200")]
    private string? note;

    partial void OnAmountChanged(decimal value) => ValidateProperty(value, nameof(Amount));
    partial void OnNoteChanged(string? value)
    {
        if (value is not null)
        {
            ValidateProperty(value, nameof(Note));
        }
    }

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        Result = new FeeRecordEditResult(Amount, PaidAt, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}


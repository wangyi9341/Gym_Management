using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GymManager.App.Dialogs;

public sealed record SessionConsumeResult(int SessionsUsed, DateTime UsedAt, string? Note);

/// <summary>
/// 消课弹窗 ViewModel。
/// </summary>
public sealed partial class SessionConsumeViewModel : DialogViewModelBase
{
    public SessionConsumeViewModel()
    {
        SessionsUsed = 1;
        UsedAt = DateTime.Now;
        ValidateAllProperties();
    }

    public SessionConsumeResult? Result { get; private set; }

    [ObservableProperty]
    [Range(1, int.MaxValue, ErrorMessage = "消课次数必须大于等于 1")]
    private int sessionsUsed = 1;

    [ObservableProperty]
    private DateTime usedAt = DateTime.Now;

    [ObservableProperty]
    [MaxLength(200, ErrorMessage = "备注长度不能超过 200")]
    private string? note;

    partial void OnSessionsUsedChanged(int value) => ValidateProperty(value, nameof(SessionsUsed));

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        Result = new SessionConsumeResult(SessionsUsed, UsedAt, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}


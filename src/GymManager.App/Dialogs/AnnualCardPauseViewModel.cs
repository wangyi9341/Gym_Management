using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GymManager.App.Dialogs;

public sealed record AnnualCardPauseResult(int PauseDays);

/// <summary>
/// 年卡停卡弹窗 ViewModel。
/// </summary>
public sealed partial class AnnualCardPauseViewModel : DialogViewModelBase
{
    public AnnualCardPauseViewModel(string memberName, DateTime currentEndDate)
    {
        MemberName = (memberName ?? string.Empty).Trim();
        CurrentEndDate = currentEndDate.Date;

        PauseDays = 1;
        ValidateAllProperties();
    }

    public string MemberName { get; }

    public DateTime CurrentEndDate { get; }

    public DateTime NewEndDate => CurrentEndDate.AddDays(PauseDays);

    public AnnualCardPauseResult? Result { get; private set; }

    [ObservableProperty]
    [Range(1, 3650, ErrorMessage = "停卡天数必须在 1~3650 之间")]
    private int pauseDays = 1;

    partial void OnPauseDaysChanged(int value)
    {
        ValidateProperty(value, nameof(PauseDays));
        OnPropertyChanged(nameof(NewEndDate));
    }

    [RelayCommand]
    private void Save()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        Result = new AnnualCardPauseResult(PauseDays);
        Close(true);
    }

    [RelayCommand]
    private void Cancel() => Close(false);
}

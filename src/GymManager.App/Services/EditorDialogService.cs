using System.Windows;
using GymManager.App.Dialogs;
using GymManager.Domain.Entities;
using GymManager.Domain.Enums;

namespace GymManager.App.Services;

public sealed class EditorDialogService : IEditorDialogService
{
    private static Window? GetOwner() => Application.Current?.MainWindow;

    public CoachEditResult? ShowCoachEditor(Coach? existing)
    {
        var vm = new CoachEditViewModel(existing?.EmployeeNo, existing?.Name);
        var window = new CoachEditWindow
        {
            Owner = GetOwner(),
            DataContext = vm
        };

        var ok = window.ShowDialog() == true;
        return ok ? vm.Result : null;
    }

    public PrivateTrainingMemberEditResult? ShowPrivateTrainingMemberEditor(PrivateTrainingMember? existing)
    {
        var vm = new PrivateTrainingMemberEditViewModel(
            isEditMode: existing is not null,
            name: existing?.Name,
            gender: existing?.Gender ?? Gender.Unknown,
            phone: existing?.Phone,
            totalSessions: existing?.TotalSessions ?? 0,
            initialPaidAmount: 0);

        var window = new PrivateTrainingMemberEditWindow
        {
            Owner = GetOwner(),
            DataContext = vm
        };

        var ok = window.ShowDialog() == true;
        return ok ? vm.Result : null;
    }

    public AnnualCardMemberEditResult? ShowAnnualCardMemberEditor(AnnualCardMember? existing)
    {
        var vm = new AnnualCardMemberEditViewModel(
            isEditMode: existing is not null,
            name: existing?.Name,
            gender: existing?.Gender ?? Gender.Unknown,
            phone: existing?.Phone,
            startDate: existing?.StartDate ?? DateTime.Today,
            endDate: existing?.EndDate ?? DateTime.Today.AddYears(1));

        var window = new AnnualCardMemberEditWindow
        {
            Owner = GetOwner(),
            DataContext = vm
        };

        var ok = window.ShowDialog() == true;
        return ok ? vm.Result : null;
    }

    public FeeRecordEditResult? ShowFeeRecordEditor()
    {
        var vm = new FeeRecordEditViewModel();
        var window = new FeeRecordEditWindow
        {
            Owner = GetOwner(),
            DataContext = vm
        };

        var ok = window.ShowDialog() == true;
        return ok ? vm.Result : null;
    }

    public SessionConsumeResult? ShowSessionConsumeEditor()
    {
        var vm = new SessionConsumeViewModel();
        var window = new SessionConsumeWindow
        {
            Owner = GetOwner(),
            DataContext = vm
        };

        var ok = window.ShowDialog() == true;
        return ok ? vm.Result : null;
    }
}


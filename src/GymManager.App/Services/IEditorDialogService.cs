using GymManager.App.Dialogs;
using GymManager.Domain.Entities;

namespace GymManager.App.Services;

/// <summary>
/// 编辑弹窗服务：统一创建并显示新增/编辑弹窗。
/// </summary>
public interface IEditorDialogService
{
    CoachEditResult? ShowCoachEditor(Coach? existing);

    PrivateTrainingMemberEditResult? ShowPrivateTrainingMemberEditor(PrivateTrainingMember? existing);

    AnnualCardMemberEditResult? ShowAnnualCardMemberEditor(AnnualCardMember? existing);

    FeeRecordEditResult? ShowFeeRecordEditor();

    SessionConsumeResult? ShowSessionConsumeEditor();
}


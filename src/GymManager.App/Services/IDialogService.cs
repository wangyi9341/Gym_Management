namespace GymManager.App.Services;

/// <summary>
/// 对话框服务：用于在 ViewModel 中触发确认/提示（保持 MVVM 结构）。
/// </summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    void Info(string title, string message);

    void Error(string title, string message);
}


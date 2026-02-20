namespace GymManager.App.Services;

/// <summary>
/// 轻提示服务：用于非阻断式通知（底部 Snackbar）。
/// </summary>
public interface IToastService
{
    void Show(string message);
}


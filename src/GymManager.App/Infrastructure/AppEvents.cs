namespace GymManager.App.Infrastructure;

/// <summary>
/// 简单事件总线：用于模块间刷新与提示（避免 ViewModel 互相强依赖）。
/// </summary>
public sealed class AppEvents
{
    public event EventHandler? DataChanged;

    public void RaiseDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
}


using CommunityToolkit.Mvvm.ComponentModel;

namespace GymManager.App.Dialogs;

/// <summary>
/// 弹窗 ViewModel 基类：通过事件请求关闭窗口，避免 ViewModel 直接操作 Window。
/// </summary>
public abstract partial class DialogViewModelBase : ObservableValidator
{
    public event EventHandler<bool?>? RequestClose;

    protected void Close(bool? dialogResult) => RequestClose?.Invoke(this, dialogResult);
}


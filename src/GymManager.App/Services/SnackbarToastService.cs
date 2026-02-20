using MaterialDesignThemes.Wpf;

namespace GymManager.App.Services;

public sealed class SnackbarToastService : IToastService
{
    private readonly SnackbarMessageQueue _queue;

    public SnackbarToastService(SnackbarMessageQueue queue)
    {
        _queue = queue;
    }

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _queue.Enqueue(message);
    }
}


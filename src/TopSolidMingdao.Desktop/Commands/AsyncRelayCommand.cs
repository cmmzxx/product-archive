using System.Windows.Input;

namespace TopSolidMingdao.Desktop.Commands;

public sealed class AsyncRelayCommand(
    Func<Task> execute, Action<Exception> handleError, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        try
        {
            await execute();
        }
        catch (Exception exception)
        {
            // ICommand 必须使用 void 入口；在此收口异常，避免 async void 异常退出进程。
            handleError(exception);
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

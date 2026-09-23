using System.Windows.Input;

namespace PCDoctor.App.Mvvm;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, CancellationToken, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this((_, _) => execute(), canExecute is null ? null : _ => canExecute())
    {
    }

    public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
        : this((_, token) => execute(token), canExecute is null ? null : _ => canExecute())
    {
    }

    public AsyncRelayCommand(Func<object?, CancellationToken, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool IsRunning => _isRunning;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _isRunning = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute(parameter, _cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isRunning = false;
            _cts.Dispose();
            _cts = null;
            RaiseCanExecuteChanged();
        }
    }

    public void Cancel() => _cts?.Cancel();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

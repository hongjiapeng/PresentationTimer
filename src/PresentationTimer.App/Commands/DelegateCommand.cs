using System.Windows.Input;

namespace PresentationTimer.App.Commands;

internal sealed class DelegateCommand : ICommand
{
    private readonly Predicate<object?>? _canExecute;
    private readonly Action<object?> _execute;

    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this._execute = _ => execute();
        this._canExecute = canExecute is null ? null : _ => canExecute();
    }

    public DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this._execute = execute;
        this._canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => this._canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => this._execute(parameter);

    public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

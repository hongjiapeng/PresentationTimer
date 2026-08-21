using System.Windows.Input;

namespace PresentationTimer.App.Commands;

internal sealed class DelegateCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Action _execute;

    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        this._execute = execute;
        this._canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => this._canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => this._execute();

    public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

using System.Windows.Input;

namespace ChatClient.MVVM.Core;

public class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public void Execute(object? parameter)
    {
        execute?.Invoke(parameter);
    }

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke(parameter) ?? true;
    }
}

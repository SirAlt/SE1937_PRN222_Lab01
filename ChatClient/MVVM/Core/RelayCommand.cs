using System.Windows.Input;

namespace ChatClient.MVVM.Core;

public class RelayCommand(Action<object?> execute, Predicate<object?> canExecute)
    : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return canExecute?.Invoke(parameter) ?? false;
    }

    public void Execute(object? parameter)
    {
        execute?.Invoke(parameter);
    }
}

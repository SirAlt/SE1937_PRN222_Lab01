using System.Windows;

namespace ChatClient.MVVM.Utils;

public class ViewUtils
{
    public static void Warn(string message, string caption)
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK);
    }

    public static void Error(string message, string caption)
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
    }
}

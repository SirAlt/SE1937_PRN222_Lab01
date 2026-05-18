using System.Globalization;
using System.Windows.Data;

namespace ChatClient.MVVM.Core;

public class PortConverter : IValueConverter
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;

    // int -> string
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int num && num >= MinPort && num <= MaxPort)
            return num.ToString();
        return string.Empty;
    }

    // string -> int
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            if (int.TryParse(str, out int port))
                return port;
        }
        return 0;
    }
}

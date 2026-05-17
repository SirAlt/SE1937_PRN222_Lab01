using System.Globalization;
using System.Net;
using System.Windows.Data;

namespace ChatClient.MVVM.Core;

public class IPAddressConverter : IValueConverter
{
    // IP -> string
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is IPAddress ip)
            return ip.ToString();
        return string.Empty;
    }

    // string -> IP
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (IPAddress.TryParse((string)value, out IPAddress? ip))
            return ip;
        return IPAddress.None;
    }
}

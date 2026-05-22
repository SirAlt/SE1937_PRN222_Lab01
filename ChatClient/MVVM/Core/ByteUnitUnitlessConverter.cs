using System.Globalization;
using System.Windows.Data;
using Utils.FileSystem;

namespace ChatClient.MVVM.Core;

public class ByteUnitUnitlessConverter : IValueConverter
{
    // long -> string
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long l)
            return string.Empty;

        var str = FileInfoHelper.FormatSizeInBytes(l, FileInfoHelper.ByteUnitSystem.SIPrefixBinaryScale, decimalPlaces: 2);
        return str[..str.IndexOf(' ')];
    }

    // string -> long
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s)
            return -1;
        //return FileInfoHelper.GetSizeInBytesInexact(s, FileInfoHelper.ByteUnitSystem.SIPrefixBinaryScale);
        return Binding.DoNothing;
    }
}

using System.Globalization;
using System.Windows.Data;
using Utils.FileSystem;

namespace ChatClient.MVVM.Core;

public class ByteUnitConverter : IValueConverter
{
    // long -> string
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long l)
            return string.Empty;
        return FileInfoHelper.FormatSizeInBytes(l, FileInfoHelper.ByteUnitSystem.SIPrefixBinaryScale, decimalPlaces: 2);
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

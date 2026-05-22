using System.Text.RegularExpressions;

namespace Utils.FileSystem;

public static class FileInfoHelper
{
    private static readonly string[] ReservedWin32Names =
        ["CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"];

    public static string SanitizeFilenameWin32(string filename)
    {
        var filenameWoExt = Path.GetFileNameWithoutExtension(filename);
        if (ReservedWin32Names.Contains(filenameWoExt))
            filename = "_" + filename;

        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidCharRegex = string.Format(@"([{0}]*\.+$)|([{0}]+)|( +$)", invalidChars);

        return Regex.Replace(filename, invalidCharRegex, "_");
    }

    public static string FormatSizeInBytes(long sizeInBytes, ByteUnitSystem unitSystem = ByteUnitSystem.SIPrefixBinaryScale, int decimalPlaces = 2)
    {
        if (sizeInBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size cannot be less than 0.");

        if (sizeInBytes == 0) return "0 bytes";
        if (sizeInBytes == 1) return "1 byte";

        string[] units = unitSystem == ByteUnitSystem.IECPrefixBinaryScale
            ? ["bytes", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB", "RiB", "QiB"]
            : ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB", "RB", "QB"];

        var scale = unitSystem == ByteUnitSystem.SIPrefixDecimalScale ? 1000 : 1024;

        var orderOfMag = unitSystem == ByteUnitSystem.SIPrefixDecimalScale
            ? (int)Math.Floor(Math.Log10(sizeInBytes) / Math.Log10(scale))
            : (int)Math.Floor(Math.Log2(sizeInBytes) / Math.Log2(scale));
        return $"{Math.Round(sizeInBytes / Math.Pow(scale, orderOfMag), decimalPlaces)} {units[orderOfMag]}";
    }

    public static long GetSizeInBytesInexact(string sizeString, ByteUnitSystem unitSystem = ByteUnitSystem.SIPrefixBinaryScale)
    {
        if (string.IsNullOrEmpty(sizeString))
            throw new ArgumentNullException(nameof(sizeString), "Size cannot be empty.");

        string[] units = unitSystem == ByteUnitSystem.IECPrefixBinaryScale
            ? ["byte", "bytes", "byte(s)", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB", "ZiB", "YiB", "RiB", "QiB"]
            : ["byte", "bytes", "byte(s)", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB", "RB", "QB"];

        var sizeRegex = string.Format(@"^(\d+|\d+\.\d+) ({0})$", string.Join('|', units));
        var match = Regex.Match(sizeString, sizeRegex, RegexOptions.IgnoreCase);

        if (!match.Success)
            throw new FormatException("Could not parse size-in-bytes string.");

        var size = double.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value;

        var scale = unitSystem == ByteUnitSystem.SIPrefixDecimalScale ? 1000 : 1024;
        var orderOfMagnitude = Math.Max(Array.FindIndex(units, e => e == unit) - 2, 0);
        var sizeInBytes = size * Math.Pow(scale, orderOfMagnitude);
        return (long)sizeInBytes;
    }

    public enum ByteUnitSystem
    {
        SIPrefixBinaryScale,
        SIPrefixDecimalScale,
        IECPrefixBinaryScale,
    }
}

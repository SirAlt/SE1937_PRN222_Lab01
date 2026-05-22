namespace Utils.FileSystem;

public class FileTypeHelper
{
    private static readonly Dictionary<FileClass, List<string>> FileClassFormats = new()
    {
        [FileClass.Text] = ["TXT"],
        [FileClass.Doc] = ["MSWord", "RTF"],
        [FileClass.Code] = ["C", "C++", "C#"],
        [FileClass.Image] = ["JPEG", "PNG", "WebP"],
        [FileClass.Audio] = ["WAV", "MP3"],
        [FileClass.Video] = ["WebM", "MP4"],
        [FileClass.Archive] = ["ZIP", "7z"],
    };

    private static readonly Dictionary<string, FormatSignature> FormatSignatures = new()
    {
        ["JPEG"] = new()
        {
            Name = "JPEG",
            Extensions = [".jpeg", ".jpg", ".jpe"],
            MagicBytes =
            [
                (new byte[] { 0xFF, 0xD8 }, 0),
            ]
        },
        ["PNG"] = new()
        {
            Name = "PNG",
            Extensions = [".png"],
            MagicBytes =
            [
                (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0),
            ]
        },
        ["WebP"] = new()
        {
            Name = "WebP",
            Extensions = [".webp"],
            MagicBytes =
            [
                ("WEBP"u8.ToArray(), 0),
                //("RIFF"u8.ToArray(), 0),
            ]
        }
    };

    public static FileClass GetFileClass(string filepath, bool verifyFileSignature = false)
    {
        var detectedFormat = GetFileFormat(filepath);
        if (detectedFormat == null)
            return FileClass.Generic;

        foreach (var fileClass in FileClassFormats)
        {
            if (fileClass.Value.Contains(detectedFormat.Name))
            {
                if (!verifyFileSignature)
                    return fileClass.Key;

                // TODO: Make this part of the "peek" in GetFileFormat().
                // File formats don't suffer from name conflict, so disambiguation is quite unnecessary.
                using var filestream = File.OpenRead(filepath);
                if (HasMatchingSignature(filestream, detectedFormat))
                    return fileClass.Key;
            }
        }
        return FileClass.Generic;
    }

    // TODO: Peek files, in case of extension conflict.
    private static FormatSignature? GetFileFormat(string filename)
    {
        var ext = Path.GetExtension(filename);
        foreach (var formatSig in FormatSignatures.Values)
        {
            if (formatSig.Extensions.Contains(ext))
            {
                return formatSig;
            }
        }
        return null;
    }

    private static bool HasMatchingSignature(Stream filestream, FormatSignature formatSignature)
    {
        if (formatSignature.MagicBytes.Count == 0)
            return true;

        foreach (var (magicBytes, offset) in formatSignature.MagicBytes)
        {
            filestream.Position = offset;
            var bytes = new byte[magicBytes.Length];
            filestream.ReadExactly(bytes);
            if (bytes.SequenceEqual(magicBytes))
                return true;
        }
        return false;
    }

    public class FormatSignature
    {
        public string Name = string.Empty;
        public List<string> Extensions = [];
        public List<(byte[] HeaderBytes, long Offset)> MagicBytes = [];
    }
}

using System.Windows.Media.Imaging;
using Utils.FileSystem;

namespace ChatClient.MVVM.Model;

public class AttachmentModel
{
    public Guid Id { get; set; }
    public string Filename { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public FileClass FileClass { get; set; }
    public bool IsAvailable { get; set; }

    public string Filepath { get; set; } = string.Empty;

    public bool IsImage => FileClass == FileClass.Image;
    public BitmapImage ImageData { get; set; } = null!;
}

using ChatClient.MVVM.Core;
using System.Windows.Media.Imaging;
using Utils.FileSystem;

namespace ChatClient.MVVM.Model;

public class AttachmentModel : ObservableObject
{
    public Guid Id { get; set; }
    public string Filename { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public FileClass FileClass { get; set; }

    public MessageModel? OwningMessage { get; set; } = null;

    /* Local */
    public string Filepath { get; set; } = string.Empty;

    /* UI */
    public bool IsImage => FileClass == FileClass.Image;

    private BitmapImage? _imageData = null;
    public BitmapImage? ImageData
    {
        get => _imageData;
        set
        {
            if (_imageData != value)
            {
                _imageData = value;
                OnPropertyChanged();
            }
        }
    }
}

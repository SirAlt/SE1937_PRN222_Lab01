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

    private bool _transferring;
    public bool IsTransferring
    {
        get => _transferring;
        set
        {
            if (_transferring != value)
            {
                _transferring = value;
                OnPropertyChanged();
            }
        }
    }

    private long _progressByte;

    public long ProgressByte
    {
        get => _progressByte;
        set
        {
            if (_progressByte != value)
            {
                _progressByte = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }
    }

    public int ProgressPercentage => (int)(100 * _progressByte / SizeInBytes);
}

using ChatClient.MVVM.Core;
using ChatClient.MVVM.Stores;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ChatClient.MVVM.Model;

public class MessageModel : ObservableObject
{
    public Guid Id { get; set; }
    public UserModel Sender { get; set; } = null!;
    public DateTime Timestamp { get; set; }

    private string _content = string.Empty;
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<AttachmentModel> Attachments { get; set; } = [];

    public bool IsSystem => Sender.Uid.Equals(IdStore.Instance.SystemUid);
    public bool IsNativeOrigin => Sender.Uid.Equals(IdStore.Instance.NativeUid);
    public bool HasAttachment => Attachments.Count > 0;
    public bool HasImage => Attachments.Any(e => e.IsImage && e.ImageData != null);

    public MessageModel()
    {
        Attachments.CollectionChanged += OnAttachmentsChanged;
    }

    private void OnAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (AttachmentModel newItem in e.NewItems)
            {
                newItem.PropertyChanged += OnAttachmentPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (AttachmentModel oldItem in e.OldItems)
            {
                oldItem.PropertyChanged -= OnAttachmentPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(HasAttachment));
        OnPropertyChanged(nameof(HasImage));
    }

    private void OnAttachmentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AttachmentModel.FileClass)
            || e.PropertyName == nameof(AttachmentModel.ImageData))
        {
            OnPropertyChanged(nameof(HasImage));
        }
    }
}

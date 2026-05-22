using ChatClient.MVVM.Core;
using ChatClient.MVVM.Stores;
using System.Collections.ObjectModel;

namespace ChatClient.MVVM.Model;

public class MessageModel : ObservableObject
{
    public Guid Id { get; set; }
    public UserModel Sender { get; set; } = null!;
    public DateTime Timestamp { get; set; }

    private string _message = string.Empty;
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<AttachmentModel> Attachments { get; set; } = [];

    public bool IsSystem => Sender.Uid.Equals(IdStore.Instance.SystemUid);
    public bool IsNativeOrigin => Sender.Uid.Equals(IdStore.Instance.NativeUid);
    public bool HasAttachment => Attachments.Count != 0;
}

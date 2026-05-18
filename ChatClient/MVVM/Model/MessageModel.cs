using ChatClient.MVVM.Stores;

namespace ChatClient.MVVM.Model;

public class MessageModel
{
    public UserModel Sender { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public DateTime Time { get; set; }

    public bool IsSystem => Sender.UID.Equals(IdStore.Instance.SystemUID);
    public bool IsNativeOrigin => Sender.UID.Equals(IdStore.Instance.NativeUID);
}

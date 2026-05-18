using ChatClient.MVVM.Stores;

namespace ChatClient.MVVM.Model;

public class UserModel
{
    public Guid UID { get; set; }
    public string Username { get; set; } = string.Empty;

    public bool IsNative => UID.Equals(IdStore.Instance.NativeUID);
}

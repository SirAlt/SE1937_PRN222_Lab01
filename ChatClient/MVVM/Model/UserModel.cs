using ChatClient.MVVM.Stores;

namespace ChatClient.MVVM.Model;

public class UserModel
{
    public Guid Uid { get; set; }
    public string Username { get; set; } = string.Empty;

    public bool IsNative => Uid.Equals(IdStore.Instance.NativeUid);
}

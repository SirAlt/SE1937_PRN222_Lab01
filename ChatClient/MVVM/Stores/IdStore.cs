namespace ChatClient.MVVM.Stores;

public class IdStore
{
    public Guid NativeUid { get; set; }
    public Guid SystemUid { get; set; }

    private static readonly object _lock = new();
    private static IdStore? _instance;

    public static IdStore Instance
    {
        get
        {
            lock (_lock)
            {
                return _instance ??= new IdStore();
            }
        }
    }

    private IdStore() { }
}

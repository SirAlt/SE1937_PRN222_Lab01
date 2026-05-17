namespace ChatClient.MVVM.Model;

public class UserModel
{
    public static readonly UserModel System = new()
    {
        UID = Guid.Empty,
        Username = "System",
    };

    public Guid UID { get; set; }
    public string Username { get; set; } = string.Empty;
}

namespace ChatClient.MVVM.Model;

public class MessageModel
{
    public UserModel Sender { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
}

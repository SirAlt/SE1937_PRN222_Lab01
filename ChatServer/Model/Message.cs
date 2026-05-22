namespace ChatServer.Model;

public class Message
{
    public Guid Id { get; set; }
    public User Sender { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<Attachment> Attachments { get; set; } = [];
}

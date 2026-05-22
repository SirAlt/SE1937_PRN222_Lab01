using Utils.FileSystem;

namespace ChatServer.Model;

public class Attachment
{
    public Guid Id { get; set; }
    public string Filename { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public FileClass FileClass { get; set; }
    public bool IsAvailable { get; set; }

    public string Filepath { get; set; } = string.Empty;
}

using ChatServer.Network;
using System.Collections.Concurrent;

namespace ChatServer.Model;

public class User
{
    public Guid Uid { get; set; } = Guid.Empty;
    public string Username { get; set; } = string.Empty;

    public ClientConnection MainConnection { get; set; } = null!;
    public ConcurrentDictionary<string, ClientConnection> WorkerConnections { get; set; } = [];
}

using NetIO;
using Server;
using System.Net;
using System.Net.Sockets;

class Program
{
    static readonly object _lock = new();
    static readonly Dictionary<Guid, ClientConnection> _clients = [];

    public static void Main(string[] args)
    {
        using var server = new TcpListener(IPAddress.Any, 1337);
        server.Start();
        Console.WriteLine("Server has started.");

        while (true)
        {
            try
            {
                var clientSocket = server.AcceptTcpClient();
                var clientConn = new ClientConnection(clientSocket);

                clientConn.Register();
                lock (_lock) _clients.Add(clientConn.UID, clientConn);
                Console.WriteLine($"[{DateTime.Now}]\t'{clientConn.Username}' [{clientConn.UID}] has joined the chat.");
                BroadcastConnection(clientConn);

                clientConn.ClientChatted += OnClientChatted;
                clientConn.ClientDisconnected += OnClientDisconnected;

                clientConn.BeginListen();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accepting client: " + ex.Message);
            }
        }
    }

    private static void OnClientChatted(object? sender, EventArgs e)
    {
        if (sender is not ClientConnection clientConn)
            return;

        var chatMsg = clientConn.ReadNextMessageSection();
        Console.WriteLine($"[{DateTime.Now}]\t'Received chat message from {clientConn.Username}': {chatMsg}");
        BroadcastChatMessage(clientConn, chatMsg);
    }

    private static void OnClientDisconnected(object? sender, EventArgs e)
    {
        if (sender is not ClientConnection clientConn)
            return;

        clientConn.Terminate();
        lock (_lock) _clients.Remove(clientConn.UID);
        Console.WriteLine($"[{DateTime.Now}]\t'{clientConn.Username}' [{clientConn.UID}] has left the chat.");
        BroadcastDisconnect(clientConn);
    }

    private static void BroadcastConnection(ClientConnection newClient)
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                client.Send(OpCode.NewUser, newClient.UID.ToString());
                foreach (var c in _clients.Values)
                {
                    client.Send(OpCode.UserListUpdate, c.UID.ToString(), c.Username);
                }
            }
        }
    }

    private static void BroadcastChatMessage(ClientConnection sender, string chatMsg)
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                client.Send(OpCode.Chat, sender.UID.ToString(), chatMsg);
            }
        }
    }

    private static void BroadcastDisconnect(ClientConnection leaver)
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                client.Send(OpCode.Disconnect, leaver.UID.ToString());
            }
        }
    }
}
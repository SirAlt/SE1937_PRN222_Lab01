using NetProtocol;
using Server;
using System.Net;
using System.Net.Sockets;

class Program
{
    static readonly object _lock = new();
    static readonly Dictionary<Guid, ClientConnection> _clients = [];

    static readonly Guid _systemUID = new();

    public static void Main(string[] args)
    {
        using var server = new TcpListener(IPAddress.Any, 1337);
        server.Start();
        Console.WriteLine("Server is running.");
        DisplayHostAddress(server);

        while (true)
        {
            try
            {
                var clientSocket = server.AcceptTcpClient();
                var clientConn = new ClientConnection(clientSocket);

                clientConn.Register();
                lock (_lock) _clients.Add(clientConn.UID, clientConn);
                Console.WriteLine($"[{DateTime.Now}]\t'{clientConn.Username}' [{clientConn.UID}] has joined the chat.");
                SendUidInfo(clientConn);
                SendUserList(clientConn);
                BroadcastNewUser(clientConn);

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

    private static void DisplayHostAddress(TcpListener server)
    {
        var publicIPAddr = GetPublicIPAddress().Result;
        Console.WriteLine($"Public IP Address:\n\t{publicIPAddr}");

        Console.WriteLine("Local IP Address(es):");
        var ips = new List<IPAddress>();
        var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in hostEntry.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                ips.Add(ip);
        }
        ips.ForEach(ip => Console.WriteLine($"\t{ip}"));

        Console.WriteLine($"Port: {((IPEndPoint)server.LocalEndpoint).Port}");

    }

    private static async Task<IPAddress?> GetPublicIPAddress()
    {
        var publicIpString = await new HttpClient().GetStringAsync("http://ipinfo.io/ip");
        if (IPAddress.TryParse(publicIpString, out var ipAddress))
            return ipAddress;
        else return null;
    }

    private static void OnClientChatted(object? sender, EventArgs e)
    {
        if (sender is not ClientConnection clientConn)
            return;

        var chatMsg = clientConn.ReadNextMessageSection();
        Console.WriteLine($"[{DateTime.Now}]\tReceived chat message from '{clientConn.Username}': {chatMsg}");
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

    private static void SendUidInfo(ClientConnection target)
    {
        lock (_lock)
        {
            target.Send(OpCode.UIDInfo, _systemUID.ToString(), target.UID.ToString());
        }
    }

    private static void SendUserList(ClientConnection target)
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                target.Send(OpCode.UserList, client.UID.ToString(), client.Username);
            }
        }
    }

    private static void BroadcastNewUser(ClientConnection newClient)
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                client.Send(OpCode.NewUser, newClient.UID.ToString(), newClient.Username);
            }
        }
    }

    private static void BroadcastChatMessage(ClientConnection sender, string chatMsg)
    {
        lock (_lock)
        {
            foreach (var client in _clients.Values)
            {
                client.Send(OpCode.Chat, sender.UID.ToString(), chatMsg, DateTime.Now.ToString());
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
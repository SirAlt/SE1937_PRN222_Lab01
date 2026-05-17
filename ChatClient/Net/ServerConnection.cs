using ChatClient.MVVM.Utils;
using NetIO;
using System.Net;
using System.Net.Sockets;

namespace ChatClient.Net;

public class ServerConnection
{
    public event EventHandler? ConnectedToServer;
    public event EventHandler? UserListUpdated;
    public event EventHandler? UserChatted;
    public event EventHandler? UserDisconnected;

    public bool Connected => _tcpClient.Connected;

    private readonly TcpClient _tcpClient;
    private PacketReader? _packetReader;
    private Task? _listenerTask;

    public ServerConnection()
    {
        _tcpClient = new TcpClient();
    }

    public void ConnectToServer(IPAddress ip, int port, string username)
    {
        if (_tcpClient.Connected)
            return;

        try
        {

            _tcpClient.Connect(ip, port);
            _packetReader = new PacketReader(_tcpClient.GetStream());

            if (!string.IsNullOrEmpty(username))
            {
                var registerPacket = new PacketBuilder()
                    .WriteOpCode(OpCode.NewUser)
                    .WriteMessage(username)
                    .Build();
                _tcpClient.GetStream().Write(registerPacket);
            }
            _listenerTask = Task.Run(Listen);
        }
        catch (Exception ex)
        {
            ViewUtils.Error("Error connecting to server: " + ex.Message, "Error");
        }
    }

    private void Listen()
    {
        while (true)
        {
            if (_packetReader == null)
                break;

            var opcode = _packetReader.ReadOpCode();
            switch (opcode)
            {
                case OpCode.NewUser:
                    ConnectedToServer?.Invoke(this, EventArgs.Empty);
                    break;
                case OpCode.UserListUpdate:
                    UserListUpdated?.Invoke(this, EventArgs.Empty);
                    break;
                case OpCode.Chat:
                    UserChatted?.Invoke(this, EventArgs.Empty);
                    break;
                case OpCode.Disconnect:
                    UserDisconnected?.Invoke(this, EventArgs.Empty);
                    break;
                case OpCode.EOS:
                    goto DISCONNECT;
                default:
                    Console.WriteLine("Hmm...");
                    break;
            }
        }
    DISCONNECT:;
    }

    public string ReadNextMessageSection()
    {
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");
        return _packetReader.ReadMessage();
    }

    public void SendChat(string msg)
    {
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");

        var chatPacket = new PacketBuilder()
            .WriteOpCode(OpCode.Chat)
            .WriteMessage(msg)
            .Build();
        _tcpClient.GetStream().Write(chatPacket);
    }

    public void DisconnectFromServer()
    {
        _tcpClient.Client.Shutdown(SocketShutdown.Both);
        _listenerTask?.Wait();  // We've closed the socket, wait for the listener thread to die gracefully, lest we get WSAECONNABORTED (socket closed by WinSock) when it tries to read the NetworkStream.
        _tcpClient.Close();
    }
}

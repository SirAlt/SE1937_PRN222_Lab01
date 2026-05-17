using NetIO;
using System.Net.Sockets;

namespace Server;

public class ClientConnection(TcpClient tcpClient)
{
    public event EventHandler? ClientChatted;
    public event EventHandler? ClientDisconnected;

    public Guid UID { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;

    private readonly TcpClient _tcpClient = tcpClient;
    private readonly PacketReader _packetReader = new(tcpClient.GetStream());

    public void Register()
    {
        var opcode = _packetReader.ReadOpCode();
        if (opcode != OpCode.NewUser)
            throw new Exception("Invalid opcode for registration.");
        Username = _packetReader.ReadMessage();
    }

    public string ReadNextMessageSection()
    {
        if (!_tcpClient.Connected)
            throw new Exception("Connection to client lost.");
        return _packetReader.ReadMessage();
    }

    public void Send(OpCode opcode, params string[] messages)
    {
        if (!_tcpClient.Connected)
            throw new Exception("Connection to client lost.");

        var builder = new PacketBuilder();

        builder.WriteOpCode(opcode);
        foreach (var message in messages)
        {
            builder.WriteMessage(message);
        }

        var packet = builder.Build();
        _tcpClient.GetStream().Write(packet);
    }

    public void BeginListen() => Task.Run(Listen);

    private void Listen()
    {
        while (true)
        {
            try
            {
                if (_packetReader == null)
                    break;

                var opcode = _packetReader.ReadOpCode();
                switch (opcode)
                {
                    case OpCode.Chat:
                        ClientChatted?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.EOS:
                        goto DISCONNECT;
                    case OpCode.NewUser:
                    case OpCode.UserListUpdate:
                    case OpCode.Disconnect:
                        Console.WriteLine($"User '{Username}' [{UID}] just tried to be sneaky. Interesting...");
                        break;
                    default:
                        Console.WriteLine("Eh?");
                        break;
                }
            }
            catch (IOException)
            {
                goto DISCONNECT;
            }
        }
    DISCONNECT:
        ClientDisconnected?.Invoke(this, EventArgs.Empty);
    }

    public void Terminate()
    {
        _tcpClient.Client.Shutdown(SocketShutdown.Both);
        _tcpClient.Close();
    }
}

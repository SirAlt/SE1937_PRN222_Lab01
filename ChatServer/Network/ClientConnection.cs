using ChatServer.Model;
using NetProtocol;
using System.Net.Sockets;

namespace ChatServer.Network;

public class ClientConnection(User user, TcpClient tcpClient)
{
    public event EventHandler? ClientChatted;
    public event EventHandler? FileTransferred;
    public event EventHandler? FileRequested;
    public event EventHandler? ClientDisconnected;

    public User User { get; set; } = user;
    public bool IsMain { get; set; }

    private readonly TcpClient _tcpClient = tcpClient;
    private readonly PacketReader _packetReader = new(tcpClient.GetStream());

    public void BeginListen()
    {
        if (IsMain)
            Task.Run(ProcessMainConnection);
        else
            Task.Run(ProcessWorkerConnection);
    }

    private void ProcessMainConnection()
    {
        while (true)
        {
            try
            {
                if (_packetReader == null)
                    break;

                var opcode = _packetReader.ReadOpCode();
                Console.WriteLine($">>> Server: Chat listener received opcode [{opcode}]");
                switch (opcode)
                {
                    case OpCode.Chat:
                        ClientChatted?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.EOS:
                        goto DISCONNECT;
                    default:
                        Console.WriteLine("Eh?");
                        break;
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O error handling message from client {User.Username} [{User.Uid}]: " + ex.Message);
                goto DISCONNECT;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling message from client {User.Username} [{User.Uid}]: " + ex.Message);
                goto DISCONNECT;
            }
        }
    DISCONNECT:
        ClientDisconnected?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessWorkerConnection()
    {
        while (true)
        {
            try
            {
                if (_packetReader == null)
                    break;

                var opcode = _packetReader.ReadOpCode();
                Console.WriteLine($">>> Server: Worker listener received opcode [{opcode}]");
                switch (opcode)
                {
                    case OpCode.FileTransfer:
                        FileTransferred?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.FileRequest:
                        FileRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.EOS:
                        goto DISCONNECT;
                    default:
                        Console.WriteLine("Eh? - Worker ver.");
                        break;
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O error handling worker socket #{Environment.CurrentManagedThreadId} of client {User.Username} [{User.Uid}]: " + ex.Message);
                goto DISCONNECT;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling worker socket #{Environment.CurrentManagedThreadId} of client {User.Username} [{User.Uid}]: " + ex.Message);
                goto DISCONNECT;
            }
        }
    DISCONNECT:
        Console.WriteLine($"Worker socket #{Environment.CurrentManagedThreadId} of client '{User.Username}' [{User.Uid}] has finished.");
    }

    public string ReadNextMessageSection()
    {
        if (!_tcpClient.Connected)
            throw new Exception("Connection to client lost.");
        return _packetReader.ReadMessageSection();
    }

    public async Task ReadNextDataSectionAsync(Stream output)
    {
        if (!_tcpClient.Connected)
            throw new Exception("Connection to client lost.");
        await _packetReader.ReadDataSectionAsync(output);
    }

    public async Task Send(OpCode opcode, params string[] messages)
    {
        if (!_tcpClient.Connected)
            throw new Exception("Connection to client lost.");

        var builder = new PacketBuilder();

        if (opcode > OpCode.Partial)
        {
            builder.WriteOpCode(opcode);
        }

        foreach (var message in messages)
        {
            await builder.WriteMessageSectionAsync(message);
        }

        var packet = builder.Build();
        await _tcpClient.GetStream().WriteAsync(packet);
    }

    public async Task SendFile(string inputFilepath)
    {
        if (!_tcpClient.Connected)
            throw new Exception("Connection to client lost.");

        using var fs = new FileStream(inputFilepath!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.Asynchronous);
        var ns = _tcpClient.GetStream();

        var lenBuffer = BitConverter.GetBytes(fs.Length);
        await ns.WriteAsync(lenBuffer);

        await fs.CopyToAsync(ns);
    }

    public void Terminate()
    {
        _tcpClient.Client.Shutdown(SocketShutdown.Both);
        _tcpClient.Close();
    }
}

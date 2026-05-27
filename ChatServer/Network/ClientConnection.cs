using ChatServer.Model;
using NetProtocol;
using System.Net.Sockets;
using Utils;

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
    private CancellationTokenSource? _listenerCancelTknSrc;

    public void BeginListen()
    {
        if (_listenerCancelTknSrc != null && !_listenerCancelTknSrc.IsCancellationRequested)
            return; // Already listening
        _listenerCancelTknSrc = new CancellationTokenSource();
        if (IsMain)
            Task.Run(() => ProcessChatConnection(_listenerCancelTknSrc.Token));
        else
            Task.Run(() => ProcessWorkerConnection(_listenerCancelTknSrc.Token));
    }

    private void ProcessChatConnection(CancellationToken ct)
    {
        if (_packetReader == null)
            return;

        while (true)
        {
            try
            {
                if (ct.IsCancellationRequested)
                    return;

                var opcode = _packetReader.ReadOpCode();
                Logger.Log(Source.Server, Level.DEBUG, $"Socket listener (type: Chat) received opcode [{opcode}].");
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
            catch (Exception ex)
            {
                Logger.Log(Source.Server, Level.ERROR, $"Socket listener (type: Chat) of client '{User.Username}' [{User.Uid}] failed with error -- " + ex.Message);
                _tcpClient.Close();
            }
        }
    DISCONNECT:
        Logger.Log(Source.Server, Level.DEBUG, $"Socket listener (type: Chat) of client '{User.Username}' [{User.Uid}] has terminated.");
        ClientDisconnected?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessWorkerConnection(CancellationToken ct)
    {
        if (_packetReader == null)
            return;

        try
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                    return;

                var opcode = _packetReader.ReadOpCode();
                Logger.Log(Source.Server, Level.DEBUG, $"Socket listener (type: Worker) received opcode [{opcode}].");
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
        }
        catch (Exception ex)
        {
            Logger.Log(Source.Server, Level.ERROR, $"Socket listener (type: Worker) of client '{User.Username}' [{User.Uid}] failed with error -- " + ex.Message);
            _tcpClient.Close();
        }
    DISCONNECT:
        Logger.Log(Source.Server, Level.DEBUG, $"Socket listener (type: Worker) of client '{User.Username}' [{User.Uid}] has terminated.");
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

        using var fs = new FileStream(inputFilepath!, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var ns = _tcpClient.GetStream();

        var lenBuffer = BitConverter.GetBytes(fs.Length);
        await ns.WriteAsync(lenBuffer);

        await fs.CopyToAsync(ns);
    }

    public void Terminate()
    {
        try
        {
            _listenerCancelTknSrc?.Cancel();
        }
        finally
        {
            _listenerCancelTknSrc?.Dispose();
            _tcpClient.Close();
        }
    }
}

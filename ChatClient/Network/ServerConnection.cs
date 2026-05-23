using ChatClient.MVVM.Utils;
using NetProtocol;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Utils;

namespace ChatClient.Network;

public class ServerConnection
{
    public event EventHandler? UserJoined;
    public event EventHandler? UidInfoReceived;
    public event EventHandler? UserListUpdated;
    public event EventHandler? UserChatted;
    public event EventHandler? FileRequestAnswered;
    public event EventHandler? UserLeft;

    public readonly SemaphoreSlim FileTransferGoAheadSignal = new(0);

    public bool IsMain { get; private set; }

    public bool Connected => _tcpClient.Connected;
    public IPAddress? IP => ((IPEndPoint?)_tcpClient.Client.RemoteEndPoint)?.Address;
    public int? Port => ((IPEndPoint?)_tcpClient.Client.RemoteEndPoint)?.Port;

    private readonly TcpClient _tcpClient;
    private PacketReader? _packetReader;
    private CancellationTokenSource? _listenerCancelTknSrc;

    public ServerConnection()
    {
        _tcpClient = new TcpClient();
    }

    public async Task ConnectToServerAsChat(IPAddress ip, int port, string username)
    {
        if (_tcpClient.Connected)
            return;

        await _tcpClient.ConnectAsync(ip, port);
        _packetReader = new PacketReader(_tcpClient.GetStream());

        var packet = new PacketBuilder()
            .WriteOpCode(OpCode.RegisterNew)
            .WriteMessageSection(username)
            .Build();
        _tcpClient.GetStream().Write(packet);

        IsMain = true;
    }

    public async Task ConnectToServerAsWorker(IPAddress ip, int port, Guid uid)
    {
        if (_tcpClient.Connected)
            return;

        await _tcpClient.ConnectAsync(ip, port);
        _packetReader = new PacketReader(_tcpClient.GetStream());

        var packet = new PacketBuilder()
            .WriteOpCode(OpCode.RegisterWorker)
            .WriteMessageSection(uid.ToString())
            .Build();
        _tcpClient.GetStream().Write(packet);

        IsMain = false;
    }

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

        try
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                    return;

                var opcode = _packetReader.ReadOpCode();
                Debug.WriteLine($">>> Client: Chat listener received opcode [{opcode}]");
                switch (opcode)
                {
                    case OpCode.NewUser:
                        UserJoined?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.UidInfo:
                        UidInfoReceived?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.UserList:
                        UserListUpdated?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.Chat:
                        UserChatted?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.FileTransferGoAhead:
                        FileTransferGoAheadSignal.Release();
                        break;
                    case OpCode.Disconnect:
                        UserLeft?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.EOS:
                        goto DISCONNECT;
                    default:
                        ViewUtils.Error("Hmm...", "OpCode Error");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($">>> [CLIENT][ERROR]: Chat listener failed with error -- " + ex.Message);
            _tcpClient.Close();
        }
    DISCONNECT:;    // Die gracefully (｡•́⩍•̀｡)
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
                Debug.WriteLine($">>> Client: Worker listener received opcode [{opcode}]");
                switch (opcode)
                {
                    case OpCode.FileRequestResponse:
                        FileRequestAnswered?.Invoke(this, EventArgs.Empty);
                        break;
                    case OpCode.EOS:
                        goto DISCONNECT;
                    default:
                        ViewUtils.Error("Hmm...", "OpCode Error");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($">>> [CLIENT][ERROR]: Worker listener failed with error -- " + ex.Message);
            _tcpClient.Close();
        }
    DISCONNECT:;    // Die gracefully (｡•́⩍•̀｡)
    }

    public string ReadNextMessageSection()
    {
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");
        return _packetReader.ReadMessageSection();
    }

    public async Task ReadNextDataSectionAsync(Stream output)
    {
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");
        await _packetReader.ReadDataSectionAsync(output);
    }

    public async Task Send(OpCode opcode = 0, params string[] messages)
    {
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");

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

    public async Task SendFileAsAttachment(
        Guid messageId, Guid attachmentId,
        string filepath,
        EventHandler<ProgressEventArgs>? progressUpdateHandler = null)
    {
        using var filestream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        await SendFileAsAttachment(messageId, attachmentId, filestream, progressUpdateHandler);
    }

    public async Task SendFileAsAttachment(
        Guid messageId, Guid attachmentId,
        Stream dataStream,
        EventHandler<ProgressEventArgs>? progressUpdateHandler = null)
    {
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");

        var ns = _tcpClient.GetStream();

        /* Metadata */
        var packet = new PacketBuilder()
            .WriteOpCode(OpCode.FileTransfer)
            .WriteMessageSection(messageId.ToString())
            .WriteMessageSection(attachmentId.ToString())
            .Build();
        await ns.WriteAsync(packet);
        Debug.WriteLine($">>> Client: Message [{messageId}] & attachment [{attachmentId}] IDs sent.");

        /* Binary data */
        Debug.WriteLine(">>> Client: Attachment data transmission start.");
        var lenBuffer = BitConverter.GetBytes(dataStream.Length);
        await ns.WriteAsync(lenBuffer);

        using var ps = new ProgressStream(dataStream);
        ps.ProgressUpdated += progressUpdateHandler;
        await ps.CopyToAsync(ns);
        Debug.WriteLine(">>> Client: Attachment data transmission finish.");
    }

    public void DisconnectFromServer()
    {
        try
        {
            _tcpClient.Client.Shutdown(SocketShutdown.Both);
            // We've disabled the socket, kill the listener before disposing, else we get a WSAECONNABORTED error (socket closed by WinSock)
            // or a SocketDisposed exception the next time it tries to read from the disabled/disposed socket.
            _listenerCancelTknSrc?.Cancel();
        }
        finally
        {
            _listenerCancelTknSrc?.Dispose();
            _tcpClient.Close();
        }
    }
}

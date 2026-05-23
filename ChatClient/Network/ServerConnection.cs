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
    private Task? _listenerTask;

    public ServerConnection()
    {
        _tcpClient = new TcpClient();
    }

    public async Task ConnectToServerAsChat(IPAddress ip, int port, string username)
    {
        if (_tcpClient.Connected)
            return;

        try
        {
            await _tcpClient.ConnectAsync(ip, port);
            _packetReader = new PacketReader(_tcpClient.GetStream());

            var packet = new PacketBuilder()
                .WriteOpCode(OpCode.RegisterNew)
                .WriteMessageSection(username)
                .Build();
            _tcpClient.GetStream().Write(packet);

            IsMain = true;
        }
        catch (Exception ex)
        {
            ViewUtils.Error("Error connecting to server: " + ex.Message, "Error");
        }
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
        if (IsMain)
            _listenerTask = Task.Run(ProcessChatConn);
        else
            _listenerTask = Task.Run(ProcessWorkerConn);
    }

    private void ProcessChatConn()
    {
        while (true)
        {
            try
            {
                if (_packetReader == null)
                    break;

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
            catch (Exception)
            {
                //_tcpClient.Client.Shutdown(SocketShutdown.Both);
                _tcpClient.Close();
            }
        }
    DISCONNECT:;    // Die gracefully (｡•́⩍•̀｡)
    }

    private void ProcessWorkerConn()
    {
        while (true)
        {
            try
            {
                if (_packetReader == null)
                    break;

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
            catch (Exception)
            {
                //_tcpClient.Client.Shutdown(SocketShutdown.Both);
                _tcpClient.Close();
            }
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
        if (!_tcpClient.Connected || _packetReader == null)
            throw new Exception("Not connected to server.");

        var ns = _tcpClient.GetStream();

        /* ID */
        var packet = new PacketBuilder()
            .WriteOpCode(OpCode.FileTransfer)
            .WriteMessageSection(messageId.ToString())
            .WriteMessageSection(attachmentId.ToString())
            .Build();
        await ns.WriteAsync(packet);
        Debug.WriteLine($">>> Client: Message [{messageId}] & attachment [{attachmentId}] IDs sent.");

        using var fs = new FileStream(filepath!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.Asynchronous);

        /* Binary data */
        Debug.WriteLine(">>> Client: Attachment data transmission start.");
        var lenBuffer = BitConverter.GetBytes(fs.Length);
        await ns.WriteAsync(lenBuffer);

        using var ps = new ProgressStream(fs);
        ps.ProgressUpdated += progressUpdateHandler;
        await ps.CopyToAsync(ns);
        Debug.WriteLine(">>> Client: Attachment data transmission finish.");
    }

    public void DisconnectFromServer(bool waitForListener = false)
    {
        _tcpClient.Client.Shutdown(SocketShutdown.Both);
        // We've closed the socket, wait for the listener thread to die gracefully,
        // else we get WSAECONNABORTED (socket closed by WinSock) when it tries to read the NetworkStream.
        if (waitForListener) _listenerTask?.Wait();
        _tcpClient.Close();
    }
}

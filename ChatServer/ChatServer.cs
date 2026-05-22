using ChatServer.Model;
using ChatServer.Network;
using NetProtocol;
using System.Net;
using System.Net.Sockets;
using Utils.FileSystem;

namespace ChatServer;

public class ChatServer
{
    private static readonly SemaphoreSlim _semaphore = new(1);

    private static readonly Guid _systemUid = new();

    private static readonly Dictionary<Guid, User> _users = [];
    private static readonly Dictionary<Guid, Message> _messages = [];
    //private static readonly Dictionary<Guid, Attachment> _files = [];

    public static async Task Main(string[] args)
    {
        using var server = new TcpListener(IPAddress.Any, 1337);
        server.Start();
        Console.WriteLine("Server has started.");

        await DisplayHostAddress(server);

        while (true)
        {
            try
            {
                var clientSocket = server.AcceptTcpClient();
                var clientConn = await RegisterConn(clientSocket);

                if (clientConn.IsMain)
                {
                    await SendUidInfo(clientConn);
                    await SendUserList(clientConn);
                    await BroadcastNewUser(clientConn);

                    clientConn.ClientChatted += OnClientChatted;
                    clientConn.ClientDisconnected += OnClientDisconnected;
                }
                else
                {
                    clientConn.FileTransferred += OnFileTransferred;
                    clientConn.FileRequested += OnFileRequested;
                }

                clientConn.BeginListen();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accepting client: " + ex.Message);
            }
        }

        static async Task DisplayHostAddress(TcpListener server)
        {
            Console.WriteLine("============ IP INFO ============");

            var publicIpStr = await new HttpClient().GetStringAsync("http://ipinfo.io/ip");
            _ = IPAddress.TryParse(publicIpStr, out var publicIp);
            Console.WriteLine($"Public IP Address:\n\t{publicIp}");

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

            Console.WriteLine("============== *** ==============");
        }

        static async Task<ClientConnection> RegisterConn(TcpClient tcpClient)
        {
            var reader = new PacketReader(tcpClient.GetStream());
            var opcode = reader.ReadOpCode();
            switch (opcode)
            {
                case OpCode.RegisterNew:
                    var user = new User()
                    {
                        Uid = Guid.NewGuid(),
                        Username = reader.ReadMessageSection(),
                    };

                    await _semaphore.WaitAsync();
                    try
                    {
                        _users.Add(user.Uid, user);
                    }
                    finally { _semaphore.Release(); }
                    Console.WriteLine($"[{DateTime.Now}]\t'{user.Username}' [{user.Uid}] has joined the chat.");

                    var clientConn = new ClientConnection(user, tcpClient)
                    {
                        IsMain = true,
                    };
                    user.MainConnection = clientConn;
                    return clientConn;
                case OpCode.RegisterWorker:
                    var uid = Guid.Parse(reader.ReadMessageSection());
                    user = _users[uid];
                    clientConn = new ClientConnection(user, tcpClient)
                    {
                        IsMain = false,
                    };
                    user.WorkerConnections.TryAdd(uid.ToString() + Environment.CurrentManagedThreadId, clientConn);
                    return clientConn;
                default:
                    throw new Exception("Invalid opcode for registration.");
            }
        }
    }

    private static async Task SendUidInfo(ClientConnection target)
    {
        await _semaphore.WaitAsync();
        try
        {
            await target.Send(OpCode.UidInfo, _systemUid.ToString(), target.User.Uid.ToString());
        }
        finally { _semaphore.Release(); }
    }

    private static async Task SendUserList(ClientConnection target)
    {
        await _semaphore.WaitAsync();
        try
        {
            foreach (var user in _users.Values)
            {
                await target.Send(OpCode.UserList, user.Uid.ToString(), user.Username);
            }
        }
        finally { _semaphore.Release(); }
    }

    private static async Task BroadcastNewUser(ClientConnection newClient)
    {
        await _semaphore.WaitAsync();
        try
        {
            var tasks = new List<Task>();
            foreach (var user in _users.Values)
            {
                tasks.Add(user.MainConnection.Send(OpCode.NewUser, newClient.User.Uid.ToString(), newClient.User.Username));
            }
            await Task.WhenAll(tasks);
        }
        finally { _semaphore.Release(); }
    }

    private static async void OnClientChatted(object? sender, EventArgs e)
    {
        try
        {
            if (sender is not ClientConnection clientConn)
                return;

            Console.WriteLine(">>> Server: Message read start.");
            var msgId = clientConn.ReadNextMessageSection();
            var msgContent = clientConn.ReadNextMessageSection();
            var msg = new Message()
            {
                Id = Guid.Parse(msgId),
                Sender = clientConn.User,
                Timestamp = DateTime.Now,
                Content = msgContent,
            };
            Console.WriteLine(">>> Server: Message read finish.");

            _semaphore.Wait();
            try
            {
                _messages.Add(msg.Id, msg);
            }
            finally { _semaphore.Release(); }

            var attachmentCount = int.Parse(clientConn.ReadNextMessageSection());
            Console.WriteLine(">>> Server: Attachment count received.");
            for (int i = 0; i < attachmentCount; i++)
            {
                var atcId = clientConn.ReadNextMessageSection();

                var filename = clientConn.ReadNextMessageSection();
                var safeFilename = FileInfoHelper.SanitizeFilenameWin32(filename);
                var filePath = GetUniqueFilePath();

                var size = clientConn.ReadNextMessageSection();

                var attachment = new Attachment()
                {
                    Id = Guid.Parse(atcId),
                    Filename = filename,
                    SizeInBytes = long.Parse(size),
                    FileClass = FileTypeHelper.GetFileClass(filename),
                    IsAvailable = false,

                    OwningMessage = msg,

                    Filepath = filePath,
                };
                msg.Attachments.Add(attachment);
                Console.WriteLine(">>> Server: Attachment info received.");

                string GetUniqueFilePath()
                {
                    var safeFnWoExt = Path.GetFileNameWithoutExtension(safeFilename);
                    var ext = Path.GetExtension(safeFilename);
                    string filepath;
                    do
                    {
                        filepath = /*Path.GetTempPath()*/ Path.GetFullPath("C:\\Users\\Admin\\Desktop\\test\\server\\") + safeFnWoExt + DateTime.Now.ToString("_yyyyMMddHHmmssffff") + ext;
                    } while (Path.Exists(filepath));
                    return filepath;
                }
            }
            await clientConn.Send(OpCode.FileTransferGoAhead);

            Console.WriteLine($"[{DateTime.Now}]\tReceived chat message from '{clientConn.User.Username}' w/ {attachmentCount} attachment(s): {msgContent}");

            Console.WriteLine(">>> Server: Message relay start.");
            BroadcastChatMessage(clientConn, msg).Wait();
            Console.WriteLine(">>> Server: Message relay finish.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error receiving chat message from client: " + ex.Message);
        }
    }

    private static async Task BroadcastChatMessage(ClientConnection sender, Message msg)
    {
        if (msg.Attachments.Count > byte.MaxValue)
            throw new Exception("Too many attachments. How did this even happen?");

        await _semaphore.WaitAsync();
        try
        {
            var tasks = new List<Task>();
            foreach (var target in _users.Values)
            {
                tasks.Add(RelayChatMessage(target.MainConnection));
            }
            await Task.WhenAll(tasks);
        }
        finally { _semaphore.Release(); }

        async Task RelayChatMessage(ClientConnection target)
        {
            await target.Send(OpCode.Chat, msg.Id.ToString(), sender.User.Uid.ToString(), DateTime.Now.ToString(), msg.Content);

            await target.Send(OpCode.Partial, msg.Attachments.Count.ToString());
            foreach (var attachment in msg.Attachments)
            {
                await target.Send
                    (OpCode.Partial,
                    attachment.Id.ToString(),
                    attachment.Filename,
                    attachment.SizeInBytes.ToString(),
                    attachment.FileClass.ToString());
            }
        }
    }

    private static void OnFileTransferred(object? sender, EventArgs e)
    {
        if (sender is not ClientConnection clientConn)
            return;

        try
        {
            var atc = GetAttachment(clientConn);
            Console.WriteLine($">>> Server: Message [{atc.OwningMessage?.Id}] will receive attachment '{atc.Filename}' [{atc.Id}] shortly.");

            using (var fs = new FileStream(atc.Filepath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                Console.WriteLine(">>> Server: File data read start.");
                clientConn.ReadNextDataSectionAsync(fs).Wait();
                Console.WriteLine(">>> Server: File data read finish.");
            }

            Console.WriteLine(">>> Server: File check start.");
            atc.FileClass = FileTypeHelper.GetFileClass(atc.Filepath, verifyFileSignature: true);
            atc.IsAvailable = true;
            Console.WriteLine(">>> Server: File check finish.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error receiving file from client: " + ex.Message);
        }
    }

    private static async void OnFileRequested(object? sender, EventArgs e)
    {
        if (sender is not ClientConnection clientConn)
            return;

        try
        {
            var atc = GetAttachment(clientConn);
            Console.WriteLine($">>> Server: Checking attachment [{atc.Id}] of message [{atc.OwningMessage?.Id}].");
            if (!atc.IsAvailable)
            {
                //throw new Exception($"File '{atc.Filepath}' [{atc.Id}] is not currently available.");
                Console.WriteLine($">>> Server: Request denied -- attachment [{atc.Id}] of message [{atc.OwningMessage?.Id}] is not available.");
                await clientConn.Send(OpCode.FileRequestResponse, false.ToString());
                return;
            }

            Console.WriteLine($">>> Server: Request accepted -- attachment [{atc.Id}] of message [{atc.OwningMessage?.Id}] will be transferred.");
            await clientConn.Send(OpCode.FileRequestResponse, true.ToString());
            Console.WriteLine(">>> Server: File data write start.");
            await clientConn.SendFile(atc.Filepath);
            Console.WriteLine(">>> Server: File data write finish.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error responding to file request from client: " + ex.Message);
        }
    }

    private static Attachment GetAttachment(ClientConnection clientConn)
    {
        Console.WriteLine(">>> Server: File IDs read start.");
        var msgId = Guid.Parse(clientConn.ReadNextMessageSection());
        var atcId = Guid.Parse(clientConn.ReadNextMessageSection());
        Console.WriteLine(">>> Server: File IDs read finish.");

        Console.WriteLine(">>> Server: Verifying message ID.");
        var msg = _messages[msgId];
        Console.WriteLine(">>> Server: Verifying attachment ID.");
        var atc = msg.Attachments.FirstOrDefault(e => e.Id == atcId)
            ?? throw new Exception($"Invalid message and attachment ID. Message [{msgId}] does not contain attachment [{atcId}].");
        return atc;
    }

    private static async void OnClientDisconnected(object? sender, EventArgs e)
    {
        if (sender is not ClientConnection clientConn)
            return;
        try
        {
            clientConn.Terminate();
            foreach (var workerConn in clientConn.User.WorkerConnections.Values)
            {
                workerConn.Terminate();
            }

            await _semaphore.WaitAsync();
            try
            {
                _users.Remove(clientConn.User.Uid);
            }
            finally { _semaphore.Release(); }

            Console.WriteLine($"[{DateTime.Now}]\t'{clientConn.User.Username}' [{clientConn.User.Uid}] has left the chat.");
            await BroadcastDisconnect(clientConn);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error disconnecting client: " + ex.Message);
        }
    }

    private static async Task BroadcastDisconnect(ClientConnection leaver)
    {
        await _semaphore.WaitAsync();
        try
        {
            var tasks = new List<Task>();
            foreach (var client in _users.Values)
            {
                tasks.Add(client.MainConnection.Send(OpCode.Disconnect, leaver.User.Uid.ToString()));
            }
            await Task.WhenAll(tasks);
        }
        finally { _semaphore.Release(); }
    }
}
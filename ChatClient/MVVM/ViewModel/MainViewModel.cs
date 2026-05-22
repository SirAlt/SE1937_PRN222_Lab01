using ChatClient.MVVM.Core;
using ChatClient.MVVM.Model;
using ChatClient.MVVM.Stores;
using ChatClient.MVVM.Utils;
using ChatClient.Network;
using NetProtocol;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Media.Imaging;
using Utils.FileSystem;

namespace ChatClient.MVVM.ViewModel;

public class MainViewModel : ObservableObject
{
    private IPAddress _ip = IPAddress.Loopback;
    public IPAddress IP
    {
        get => _ip;
        set
        {
            if (_ip != value)
            {
                _ip = value;
                OnPropertyChanged();
            }
        }
    }

    private int _port = 1337;
    public int Port
    {
        get => _port;
        set
        {
            if (_port != value)
            {
                _port = value;
                OnPropertyChanged();
            }
        }
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged();
            }
        }
    }

    private MessageModel _newMessage = new()
    {
        Id = Guid.NewGuid(),
        //Message = "La diddo didda!",
        //Attachments = [new() {
        //    Filename = "The Throngler and the O Poor Throngled",
        //    SizeInBytes = 1024,
        //    IsImage = true,
        //}],
    };
    public MessageModel NewMessage
    {
        get => _newMessage;
        set
        {
            if (_newMessage != value)
            {
                _newMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<UserModel> Users { get; set; } = [];
    public ObservableCollection<MessageModel> Messages { get; set; } = [];

    public RelayCommand ConnectToServerCommand { get; set; }
    public RelayCommand SendChatCommand { get; set; }
    public RelayCommand DisconnectCommand { get; set; }
    public RelayCommand AttachFileCommand { get; set; }
    public RelayCommand RemoveAttachmentCommand { get; set; }
    public RelayCommand DownloadFileCommand { get; set; }

    private UserModel? _system;
    private UserModel System => _system ??= new()
    {
        Uid = IdStore.Instance.SystemUid,
        Username = "SYSTEM",
    };

    private ServerConnection? _serverConn;

    public MainViewModel()
    {
        ConnectToServerCommand = new(
            async o =>
            {
                _serverConn = new ServerConnection();
                _serverConn.UidInfoReceived += OnUidInfoReceived;
                _serverConn.UserListUpdated += OnUserListUpdated;
                _serverConn.UserJoined += OnUserJoined;
                _serverConn.UserChatted += OnUserChat;
                _serverConn.UserLeft += OnUserDisconnect;

                if (IP == null || IPAddress.None.Equals(IP) || IPAddress.Any.Equals(IP))
                {
                    ViewUtils.Warn("You likely entered a malformed IP address. Try again.", "Invalid IP address");
                    return;
                }
                if (Port < 1 || Port > 65535)
                {
                    ViewUtils.Warn("You have entered an invalid port number. Try again.", "Invalid port number");
                    return;
                }
                await _serverConn.ConnectToServerAsChat(IP!, Port, Username!);
            },
            o => (_serverConn == null || !_serverConn.Connected) && IP != null && Port != default && !string.IsNullOrWhiteSpace(Username)
            );

        SendChatCommand = new(
            async o =>
            {
                if (NewMessage.Attachments.Count > byte.MaxValue)
                    throw new Exception("Hold it right there, sport! That's too many attachments for one messsage, dunchathink?");

                Debug.WriteLine(">>> Client: Send start.");
                var msg = NewMessage;
                NewMessage = new() { Id = Guid.NewGuid() };

                await _serverConn!.Send(OpCode.Chat, msg.Id.ToString(), msg.Message);
                Debug.WriteLine($">>> Client: Message [{msg.Id}] sent.");

                /* File info */
                await _serverConn.Send(OpCode.Partial, msg.Attachments.Count.ToString());
                Debug.WriteLine(">>> Client: Attachment count sent.");
                foreach (var attachment in msg.Attachments)
                {
                    await _serverConn.Send(OpCode.Partial, attachment.Id.ToString(), attachment.Filename, attachment.SizeInBytes.ToString());
                    Debug.WriteLine(">>> Client: Attachment info sent.");
                }

                // If we don't wait here, we face a race condition between the server reading the 'attachment info' (above)
                // and the IDs (below). If the IDs happen to get read first, server will think the attachment doesn't exist.
                Debug.WriteLine(">>> Client: Waiting for go-ahead signal.");
                await _serverConn.FileTransferGoAheadSignal.WaitAsync();

                /* File data */
                Debug.WriteLine(">>> Client: Go-ahead signal received.");
                Parallel.ForEach(msg.Attachments, async attachment =>
                {
                    var workerConn = await SpawnWorkerSocket();
                    Debug.WriteLine(">>> Client: Worker spawned.");
                    await workerConn.SendFileAsAttachment(msg.Id, attachment.Id, attachment.Filepath);
                    workerConn.DisconnectFromServer();
                    Debug.WriteLine(">>> Client: Worker terminated.");
                });
                // Do NOT release the semaphore. Only the socket listener can do that.
            },
            o => _serverConn != null && _serverConn.Connected
                && (!string.IsNullOrWhiteSpace(NewMessage.Message) || NewMessage.Attachments.Count > 0)
            );

        AttachFileCommand = new(
            async o =>
            {
                var fileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpeg;*.jpg;*.jpe;*.png;*.webp" + "|All Files|*.*"
                };
                if (fileDialog.ShowDialog() == true)
                {
                    var filepath = fileDialog.FileName;
                    var attachment = new AttachmentModel()
                    {
                        Id = Guid.NewGuid(),
                        Filename = Path.GetFileName(filepath),
                        SizeInBytes = new FileInfo(filepath).Length,
                        FileClass = FileTypeHelper.GetFileClass(filepath),

                        Filepath = filepath,
                    };

                    if (attachment.IsImage)
                    {
                        var image = attachment.ImageData ??= new();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        using var fs = File.OpenRead(filepath);
                        image.StreamSource = fs;
                        image.EndInit();
                    }

                    NewMessage.Attachments.Add(attachment);
                }
            }
            );

        RemoveAttachmentCommand = new(
            o =>
            {
                if (o is not AttachmentModel attachment)
                    return;
                NewMessage.Attachments.Remove(attachment);
            }
            );

        DownloadFileCommand = new(
            o =>
            {
                if (o is not AttachmentModel attachment)
                    return;


            }
            );

        DisconnectCommand = new(
            o =>
            {
                _serverConn!.DisconnectFromServer();

                var goodbyeMsg = new MessageModel()
                {
                    Sender = System,
                    Message = "You have left the chat.",
                };
                Messages.Add(goodbyeMsg);
                Users.Clear();
            },
            o => _serverConn != null && _serverConn.Connected
            );
    }

    private async Task<ServerConnection> SpawnWorkerSocket()
    {
        if (_serverConn == null || !_serverConn.Connected)
            throw new Exception("Not connected to server.");

        var _workerConn = new ServerConnection();
        await _workerConn.ConnectToServerAsWorker(_serverConn.IP!, _serverConn.Port!.Value, IdStore.Instance.NativeUid);
        return _workerConn;
    }

    private void OnUidInfoReceived(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        IdStore.Instance.SystemUid = Guid.Parse(serverConn.ReadNextMessageSection());
        IdStore.Instance.NativeUid = Guid.Parse(serverConn.ReadNextMessageSection());
    }

    private void OnUserListUpdated(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = Guid.Parse(serverConn.ReadNextMessageSection());
        var user = new UserModel()
        {
            Uid = uid,
            Username = serverConn.ReadNextMessageSection(),
        };

        if (!Users.Any(u => u.Uid == user.Uid))
        {
            Application.Current.Dispatcher.Invoke(() => Users.Add(user));
        }
    }

    private void OnUserJoined(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = Guid.Parse(serverConn.ReadNextMessageSection());
        var newbie = new UserModel()
        {
            Uid = uid,
            Username = serverConn.ReadNextMessageSection(),
        };

        var newbieMsg = new MessageModel()
        {
            Sender = System,
            Message = newbie.IsNative
                        ? "You have joined the chat."
                        : $"'{newbie.Username}' has joined the chat.",
        };
        Application.Current.Dispatcher.Invoke(() => Messages.Add(newbieMsg));

        if (!Users.Any(u => u.Uid == newbie.Uid))
        {
            Application.Current.Dispatcher.Invoke(() => Users.Add(newbie));
        }
    }

    private void OnUserChat(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var msgId = serverConn.ReadNextMessageSection();
        var uid = serverConn.ReadNextMessageSection();
        var time = serverConn.ReadNextMessageSection();
        var msg = serverConn.ReadNextMessageSection();

        var senderUser = Users.FirstOrDefault(
            u => u.Uid == Guid.Parse(uid),
            UndefinedUser);

        if (!DateTime.TryParse(time, out var timestamp))
            timestamp = DateTime.Now;

        var message = new MessageModel()
        {
            Id = Guid.Parse(msgId),
            Sender = senderUser,
            Timestamp = timestamp,
            Message = msg,
        };

        var atcCnt = serverConn.ReadNextMessageSection();
        var attachmentCount = int.Parse(atcCnt);
        for (int i = 0; i < attachmentCount; i++)
        {
            var attachId = serverConn.ReadNextMessageSection();
            var filename = serverConn.ReadNextMessageSection();
            var sizeInBytes = serverConn.ReadNextMessageSection();
            var isAvailable = serverConn.ReadNextMessageSection();
            var fileClass = serverConn.ReadNextMessageSection();

            var attachment = new AttachmentModel()
            {
                Id = Guid.Parse(attachId),
                Filename = filename,
                SizeInBytes = long.Parse(sizeInBytes),
                IsAvailable = bool.Parse(isAvailable),
                FileClass = (FileClass)Enum.Parse(typeof(FileClass), fileClass),
            };
            message.Attachments.Add(attachment);
        }

        Application.Current.Dispatcher.Invoke(() => Messages.Add(message));
    }

    private void OnUserDisconnect(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = serverConn.ReadNextMessageSection();
        var leaver = Users.FirstOrDefault(u => u.Uid == Guid.Parse(uid));
        if (leaver == null) return;

        var leaverMsg = new MessageModel()
        {
            Sender = System,
            Message = leaver.IsNative
                        ? "You have left the chat."
                        : $"'{leaver.Username}' has left the chat.",
        };
        Application.Current.Dispatcher.Invoke(() => Messages.Add(leaverMsg));
        Application.Current.Dispatcher.Invoke(() => Users.Remove(leaver));
    }

    private static readonly UserModel UndefinedUser = new()
    {
        Uid = Guid.Empty,
        Username = string.Empty,
    };
}

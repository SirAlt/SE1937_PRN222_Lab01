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
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Utils;
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
        //Content = "La diddo didda!",
        //Attachments = [new() {
        //    Filename = "The Throngler and the O Poor Throngled",
        //    SizeInBytes = 1337,
        //    FileClass = FileClass.Image,
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
    public RelayCommand PasteClipboardCommand { get; set; }
    public RelayCommand AttachFileCommand { get; set; }
    public RelayCommand RemoveAttachmentCommand { get; set; }
    public RelayCommand DownloadFileCommand { get; set; }
    public RelayCommand SaveImageCommand { get; set; }

    private UserModel? _system;
    private UserModel System => _system ??= new()
    {
        Uid = IdStore.Instance.SystemUid,
        Username = "SYSTEM",
    };

    private UserModel? _nativeUser;

    private ServerConnection? _serverConn;

    public MainViewModel()
    {
        ConnectToServerCommand = new(
            async o =>
            {
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

                try
                {
                    _serverConn = new ServerConnection();
                    _serverConn.UidInfoReceived += OnUidInfoReceived;
                    _serverConn.UserListUpdated += OnUserListUpdated;
                    _serverConn.UserJoined += OnUserJoined;
                    _serverConn.UserChatted += OnUserChat;
                    _serverConn.UserLeft += OnUserDisconnect;

                    await _serverConn.ConnectToServerAsChat(IP!, Port, Username!);
                    _serverConn.BeginListen();
                }
                catch (Exception ex)
                {
                    ViewUtils.Error("Error connecting to server: " + ex.Message, "Error");
                }
            },
            o => (_serverConn == null || !_serverConn.Connected) && IP != null && Port != default && !string.IsNullOrWhiteSpace(Username)
            );

        SendChatCommand = new(
            async o =>
            {
                Logger.Log(Source.Client, Level.DEBUG, $"Send command initiated for message [{NewMessage.Id}]");
                var msg = NewMessage;
                NewMessage = new()
                {
                    Id = Guid.NewGuid(),
                    Sender = _nativeUser!,
                };

                msg.Timestamp = DateTime.Now;
                Messages.Add(msg);
                Logger.Log(Source.Client, Level.DEBUG, $"Added local version of message [{msg.Id}] to chatlist.");

                await _serverConn!.Send(OpCode.Chat, msg.Id.ToString(), msg.Content);
                Logger.Log(Source.Client, Level.DEBUG, $"Message [{msg.Id}] transmitted.");

                /* File info */
                await _serverConn.Send(OpCode.Partial, msg.Attachments.Count.ToString());
                Logger.Log(Source.Client, Level.DEBUG, $"Attachment count [{msg.Attachments.Count}] of message [{msg.Id}] transmitted.");
                foreach (var attachment in msg.Attachments)
                {
                    await _serverConn.Send(OpCode.Partial, attachment.Id.ToString(), attachment.Filename, attachment.SizeInBytes.ToString());
                    Logger.Log(Source.Client, Level.DEBUG, $"Metadata of attachment '{attachment.Filename}' [{attachment.Id}] of message [{msg.Id}] transmitted.");
                }

                // If we don't wait here, we face a race condition between the server reading the 'attachment info' (above)
                // and the IDs (below). If the IDs happen to get read first, server will think the attachment doesn't exist.
                Logger.Log(Source.Client, Level.DEBUG, $"All attachment metadata of message [{msg.Id}] transmitted. Waiting for go-ahead signal.");
                await _serverConn.FileTransferGoAheadSignal.WaitAsync();
                // Do NOT release the semaphore. Only the socket listener can do that.

                /* File data */
                Logger.Log(Source.Client, Level.DEBUG, $"Go-ahead signal for attachments of message [{msg.Id}] received.");
                Parallel.ForEach(msg.Attachments, async attachment =>
                {
                    var workerConn = await SpawnWorkerSocket();
                    Logger.Log(Source.Client, Level.DEBUG, $"Worker socket for transmission of attachment '{attachment.Filename}' [{attachment.Id}] of message [{msg.Id}] spawned.");

                    attachment.IsTransferring = true;
                    if (attachment.IsInMemory && attachment.ImageData != null)
                    {
                        Logger.Log(Source.Client, Level.DEBUG, $"Attachment '{attachment.Filename}' [{attachment.Id}] of message [{msg.Id}] is available in memory.");
                        // Would be lovely if we could just grab the Stream used to create the BitmapImage,
                        // but not disposing of that thing ASAP is prime ground for a memory leak.
                        using var ms = new MemoryStream();
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(attachment.ImageData));
                        encoder.Save(ms);
                        ms.Position = 0;

                        await workerConn.SendFileAsAttachment(
                            msg.Id, attachment.Id, ms,
                            (s, e) => attachment.ProgressByte = e.Progress);
                    }
                    else
                    {
                        Logger.Log(Source.Client, Level.DEBUG, $"Attachment '{attachment.Filename}' [{attachment.Id}] of message [{msg.Id}] will be fetched from disk.");
                        await workerConn.SendFileAsAttachment(
                            msg.Id, attachment.Id, attachment.Filepath,
                            (s, e) => attachment.ProgressByte = e.Progress);
                    }
                    attachment.IsTransferring = false;
                    Logger.Log(Source.Client, Level.DEBUG, $"Attachment '{attachment.Filename}' [{attachment.Id}] of message [{msg.Id}] transmitted.");

                    workerConn.DisconnectFromServer();
                    Logger.Log(Source.Client, Level.DEBUG, $"Worker socket for transmission of attachment '{attachment.Filename}' [{attachment.Id}] of message [{msg.Id}] terminated.");
                });
            },
            o => _serverConn != null && _serverConn.Connected && _nativeUser != null
                && (!string.IsNullOrWhiteSpace(NewMessage.Content) || NewMessage.Attachments.Count > 0)
            );

        PasteClipboardCommand = new(
            o =>
            {
                if (o is not RichTextBox rtb)
                    return;

                if (Clipboard.ContainsImage())
                {
                    var bitmapSource = Clipboard.GetImage();
                    var bitmapImage = new BitmapImage();

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    ms.Position = 0;

                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    var filename = Path.GetRandomFileName();
                    filename = filename.Replace(Path.GetExtension(filename), ".png");

                    var attachment = new AttachmentModel()
                    {
                        Id = Guid.NewGuid(),
                        Filename = filename,
                        SizeInBytes = ms.Length,
                        FileClass = FileClass.Image,

                        OwningMessage = NewMessage,

                        IsInMemory = true,

                        ImageData = bitmapImage,
                    };
                    NewMessage.Attachments.Add(attachment);
                }
                else
                {
                    rtb.Paste();
                }
            }
            );

        AttachFileCommand = new(
            async o =>
            {
                var fileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpeg;*.jpg;*.jpe;*.png;*.webp" + "|All Files|*.*",
                    FilterIndex = 2,
                    Multiselect = true,
                    RestoreDirectory = true,
                };
                if (fileDialog.ShowDialog() == false) return;

                foreach (var filepath in fileDialog.FileNames)
                {
                    var attachment = new AttachmentModel()
                    {
                        Id = Guid.NewGuid(),
                        Filename = Path.GetFileName(filepath),
                        SizeInBytes = new FileInfo(filepath).Length,
                        FileClass = FileTypeHelper.GetFileClass(filepath),

                        OwningMessage = NewMessage,

                        Filepath = filepath,
                    };

                    if (attachment.IsImage)
                    {
                        var image = attachment.ImageData = new();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
                        image.StreamSource = fs;
                        image.EndInit();
                        image.Freeze();
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
            async o =>
            {
                if (o is not AttachmentModel attachment)
                    return;

                var workerConn = await SpawnWorkerSocket();
                workerConn.FileRequestAnswered += ReceiveFile;
                workerConn.BeginListen();


                Logger.Log(Source.Client, Level.DEBUG, $"Requesting attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}].");
                await workerConn.Send(OpCode.FileRequest, attachment.OwningMessage!.Id.ToString(), attachment.Id.ToString());
                Logger.Log(Source.Client, Level.DEBUG, $"Waiting for availability response on attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}].");

                void ReceiveFile(object? sender, EventArgs e)
                {
                    var avail = bool.Parse(workerConn.ReadNextMessageSection());
                    if (!avail)
                    {
                        Logger.Log(Source.Client, Level.DEBUG, $"Attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}] is NOT AVAILABLE.");
                        ViewUtils.Warn("This file is not available for download. Try again later.", "File unavailable");
                        return;
                    }

                    Logger.Log(Source.Client, Level.DEBUG, $"Attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}] is AVAILABLE.");
                    var fileDialog = new Microsoft.Win32.SaveFileDialog()
                    {
                        FileName = attachment.Filename,
                        Filter = "All Files|*.*",
                        RestoreDirectory = true,
                        OverwritePrompt = true,
                    };
                    if (fileDialog.ShowDialog() == false)
                    {
                        workerConn.DisconnectFromServer();
                        return;
                    }
                    Logger.Log(Source.Client, Level.DEBUG, $"Path '{fileDialog.FileName}' chosen by user to save attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}].");

                    using var fs = new FileStream(fileDialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                    var ps = new ProgressStream(fs, attachment.SizeInBytes);
                    ps.ProgressUpdated += (s, e) =>
                    {
                        attachment.ProgressByte = e.Progress;
                    };
                    Logger.Log(Source.Client, Level.DEBUG, $"Transmission of attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}] is starting.");
                    attachment.IsTransferring = true;
                    workerConn.ReadNextDataSectionAsync(ps).Wait();
                    attachment.IsTransferring = false;
                    Logger.Log(Source.Client, Level.DEBUG, $"Transmission of attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}] has finished.");

                    workerConn.DisconnectFromServer();
                    Logger.Log(Source.Client, Level.DEBUG, $"Worker socket for transmission of attachment '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage?.Id}] terminated.");
                }
            },
            o => _serverConn != null && _serverConn.Connected
            );

        SaveImageCommand = new(
            o =>
            {
                if (o is not AttachmentModel attachment)
                    return;

                var fileDialog = new Microsoft.Win32.SaveFileDialog()
                {
                    FileName = attachment.Filename,
                    Filter = "JPEG Image|*.jpg" + "|PNG Image|*.png" + "|WEBP File|*.webp",
                    RestoreDirectory = true,
                    OverwritePrompt = true,
                };
                if (fileDialog.ShowDialog() == false) return;

                var ext = Path.GetExtension(fileDialog.FileName);
                BitmapEncoder encoder;
                switch (ext)
                {
                    case ".jpg":
                    case ".jpeg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    default:
                        ViewUtils.Warn("The selected image format is not supported.", "Unsupported format");
                        return;
                }
                using var fileStream = new FileStream(fileDialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                encoder.Frames.Add(BitmapFrame.Create(attachment.ImageData));
                encoder.Save(fileStream);
            },
            o => _serverConn != null && _serverConn.Connected
            && o is AttachmentModel { IsImage: true, ImageData: not null }
            );

        DisconnectCommand = new(
            o =>
            {
                _serverConn!.DisconnectFromServer();

                var goodbyeMsg = new MessageModel()
                {
                    Sender = System,
                    Content = "You have left the chat.",
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

        var uid = serverConn.ReadNextMessageSection();
        var username = serverConn.ReadNextMessageSection();

        var user = new UserModel()
        {
            Uid = Guid.Parse(uid),
            Username = username,
        };

        if (!Users.Any(u => u.Uid == user.Uid))
        {
            Application.Current.Dispatcher.Invoke(() => Users.Add(user));
        }

        if (_nativeUser == null && user.Uid == IdStore.Instance.NativeUid)
        {
            NewMessage.Sender = _nativeUser = user;
        }
    }

    private void OnUserJoined(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = serverConn.ReadNextMessageSection();
        var username = serverConn.ReadNextMessageSection();

        var newbie = new UserModel()
        {
            Uid = Guid.Parse(uid),
            Username = username,
        };

        var newbieMsg = new MessageModel()
        {
            Sender = System,
            Content = newbie.IsNative
                        ? "You have joined the chat."
                        : $"'{newbie.Username}' has joined the chat.",
        };
        Application.Current.Dispatcher.Invoke(() => Messages.Add(newbieMsg));

        if (!Users.Any(u => u.Uid == newbie.Uid))
        {
            Application.Current.Dispatcher.Invoke(() => Users.Add(newbie));
        }
    }

    private async void OnUserChat(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        bool isEcho = false;

        var msgId = serverConn.ReadNextMessageSection();
        var uid = serverConn.ReadNextMessageSection();
        var time = serverConn.ReadNextMessageSection();
        var content = serverConn.ReadNextMessageSection();

        var messageId = Guid.Parse(msgId);
        var message = Messages.FirstOrDefault(e => e.Id == messageId);
        ;

        var senderUser = Users.FirstOrDefault(
            u => u.Uid == Guid.Parse(uid),
            UndefinedUser);

        if (!DateTime.TryParse(time, out var timestamp))
            timestamp = DateTime.Now;

        if (isEcho = message != null)
        {
            message!.Sender = senderUser;
            message.Timestamp = timestamp;
            message.Content = content;

            message.IsOnServer = true;
        }
        else
        {
            message = new MessageModel()
            {
                Id = messageId,
                Sender = senderUser,
                Timestamp = timestamp,
                Content = content,

                IsOnServer = true,
            };
        }

        var atcCnt = serverConn.ReadNextMessageSection();
        var attachmentCount = int.Parse(atcCnt);
        for (int i = 0; i < attachmentCount; i++)
        {
            var atcId = serverConn.ReadNextMessageSection();
            var filename = serverConn.ReadNextMessageSection();
            var sizeInBytes = serverConn.ReadNextMessageSection();
            var fileClass = serverConn.ReadNextMessageSection();

            AttachmentModel? attachment;
            var attachmentId = Guid.Parse(atcId);
            if (isEcho && (attachment = message.Attachments.FirstOrDefault(e => e.Id == attachmentId)) != null)
            {
                attachment.Filename = filename;
                attachment.SizeInBytes = long.Parse(sizeInBytes);
                attachment.FileClass = (FileClass)Enum.Parse(typeof(FileClass), fileClass);
            }
            else
            {
                attachment = new AttachmentModel()
                {
                    Id = attachmentId,
                    Filename = filename,
                    SizeInBytes = long.Parse(sizeInBytes),
                    FileClass = (FileClass)Enum.Parse(typeof(FileClass), fileClass),

                    OwningMessage = message,
                };
                message.Attachments.Add(attachment);
            }

            if (!isEcho && attachment.IsImage)
            {
                _ = AutoRequestImageFile();
            }

            async Task AutoRequestImageFile()
            {
                int retryCount = 10;
                int delay = 3_000;

                var workerConn = await SpawnWorkerSocket();
                workerConn.FileRequestAnswered += ReceiveImage;
                workerConn.BeginListen();

                await workerConn.Send(OpCode.FileRequest, attachment.OwningMessage!.Id.ToString(), attachment.Id.ToString());
                Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Auto-requested image '{attachment.Filename}' [{attachment.Id}] of message [{attachment.OwningMessage!.Id}].");

                void ReceiveImage(object? sender, EventArgs e)
                {
                    Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Received availability response for image '{attachment.Filename}' [{attachment.Id}].");
                    var avail = bool.Parse(workerConn.ReadNextMessageSection());
                    if (!avail)
                    {
                        Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Image '{attachment.Filename}' [{attachment.Id}] not available. Retries remaining: {retryCount}.");
                        if (retryCount-- > 0)
                        {
                            Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Will try to download image '{attachment.Filename}' [{attachment.Id}] again. Delay: {delay}ms");
                            Task.Delay(delay).Wait();

                            _ = workerConn.Send(OpCode.FileRequest, attachment.OwningMessage!.Id.ToString(), attachment.Id.ToString());
                            Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Auto-requested image [{attachment.Id}] of message [{attachment.OwningMessage!.Id}].");
                        }
                        else
                        {
                            Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Out of lives. Game over. Goodbye, '{attachment.Filename}' [{attachment.Id}]!");
                            attachment.ImageData = null;
                        }
                        return;
                    }

                    Logger.Log(Source.Client, Level.DEBUG, $">>> Client: '{attachment.Filename}' [{attachment.Id}] is available. Yay.");

                    var ms = new MemoryStream();
                    var ps = new ProgressStream(ms, attachment.SizeInBytes);
                    ps.ProgressUpdated += (s, e) => { };
                    Logger.Log(Source.Client, Level.DEBUG, $">>> Client: File transmission of '{attachment.Filename}' [{attachment.Id}] start.");
                    workerConn.ReadNextDataSectionAsync(ps).Wait();
                    Logger.Log(Source.Client, Level.DEBUG, $">>> Client: File transmission of '{attachment.Filename}' [{attachment.Id}] finish.");

                    workerConn.DisconnectFromServer();
                    Logger.Log(Source.Client, Level.DEBUG, $">>> Client: Worker socket disconnected.");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            ms.Position = 0;

                            var image = attachment.ImageData = new();
                            image.BeginInit();
                            image.BaseUri = null;
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = ms;
                            image.EndInit();
                            image.Freeze();
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(Source.Client, Level.ERROR, $"Error auto-downloading image '{attachment.Filename}' [{attachment.Id}] -- " + ex.Message);
                        }
                        finally
                        {
                            ms.Close();
                        }
                    });
                }
            }
        }

        if (!isEcho)
        {
            Application.Current.Dispatcher.Invoke(() => Messages.Add(message));
        }
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
            Content = leaver.IsNative
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

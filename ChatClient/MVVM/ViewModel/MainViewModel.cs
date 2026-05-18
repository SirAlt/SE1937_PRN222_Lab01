using ChatClient.MVVM.Core;
using ChatClient.MVVM.Model;
using ChatClient.MVVM.Stores;
using ChatClient.MVVM.Utils;
using ChatClient.Network;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;

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

    private string _message = string.Empty;
    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<UserModel> Users { get; set; } = [];
    public ObservableCollection<MessageModel> Messages { get; set; } = [];

    public RelayCommand ConnectToServerCommand { get; set; }
    public RelayCommand SendChatCommand { get; set; }
    public RelayCommand DisconnectCommand { get; set; }

    private UserModel? _system;
    private UserModel System => _system ??= new()
    {
        UID = IdStore.Instance.SystemUID,
        Username = "SYSTEM",
    };

    private ServerConnection? _serverConn;

    public MainViewModel()
    {
        ConnectToServerCommand = new(
            o =>
            {
                _serverConn = new ServerConnection();
                _serverConn.UserJoined += OnUserJoined;
                _serverConn.UidInfoReceived += OnUidInfoReceived;
                _serverConn.UserListUpdated += OnUserListUpdated;
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
                _serverConn.ConnectToServer(IP!, Port, Username!);
            },
            o => (_serverConn == null || !_serverConn.Connected) && IP != null && Port != default && !string.IsNullOrWhiteSpace(Username)
            );

        SendChatCommand = new(
            o =>
            {
                _serverConn?.SendChat(Message);
                Message = string.Empty;
            },
            o => _serverConn != null && _serverConn.Connected && !string.IsNullOrWhiteSpace(Message)
            );

        DisconnectCommand = new(
            o =>
            {
                _serverConn?.DisconnectFromServer();

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

    private void OnUidInfoReceived(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        IdStore.Instance.SystemUID = Guid.Parse(serverConn.ReadNextMessageSection());
        IdStore.Instance.NativeUID = Guid.Parse(serverConn.ReadNextMessageSection());
    }

    private void OnUserListUpdated(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = Guid.Parse(serverConn.ReadNextMessageSection());
        var user = new UserModel()
        {
            UID = uid,
            Username = serverConn.ReadNextMessageSection(),
        };

        if (!Users.Any(u => u.UID == user.UID))
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
            UID = uid,
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

        if (!Users.Any(u => u.UID == newbie.UID))
        {
            Application.Current.Dispatcher.Invoke(() => Users.Add(newbie));
        }
    }

    private void OnUserChat(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = serverConn.ReadNextMessageSection();
        var msg = serverConn.ReadNextMessageSection();
        var t = serverConn.ReadNextMessageSection();

        var src = Users.FirstOrDefault(
            u => u.UID == Guid.Parse(uid),
            UndefinedUser);

        if (!DateTime.TryParse(t, out var time))
            time = DateTime.Now;

        var message = new MessageModel()
        {
            Sender = src,
            Message = msg,
            Time = time,
        };
        Application.Current.Dispatcher.Invoke(() => Messages.Add(message));
    }

    private void OnUserDisconnect(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = serverConn.ReadNextMessageSection();
        var leaver = Users.FirstOrDefault(u => u.UID == Guid.Parse(uid));
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
        UID = Guid.Empty,
        Username = string.Empty,
    };
}

using ChatClient.MVVM.Core;
using ChatClient.MVVM.Model;
using ChatClient.MVVM.Utils;
using ChatClient.Net;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;

namespace ChatClient.MVVM.ViewModel;

public class MainViewModel
{
    public IPAddress IP { get; set; } = IPAddress.Loopback;
    public int Port { get; set; } = 1337;
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public ObservableCollection<UserModel> Users { get; set; } = [];
    public ObservableCollection<MessageModel> Messages { get; set; } = [];

    public RelayCommand ConnectToServerCommand { get; set; }
    public RelayCommand SendChatCommand { get; set; }
    public RelayCommand DisconnectCommand { get; set; }

    private readonly ServerConnection _serverConn;

    public MainViewModel()
    {
        _serverConn = new ServerConnection();
        _serverConn.ConnectedToServer += OnConnectedToServer;
        _serverConn.UserListUpdated += OnUserListUpdated;
        _serverConn.UserChatted += OnUserChat;
        _serverConn.UserDisconnected += OnUserDisconnect;

        ConnectToServerCommand = new(
            o =>
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
                _serverConn.ConnectToServer(IP!, Port, Username!);
            },
            o => !_serverConn.Connected && IP != null && Port != default && !string.IsNullOrWhiteSpace(Username)
            );

        SendChatCommand = new(
            o => _serverConn.SendChat(Message),
            o => _serverConn.Connected && !string.IsNullOrWhiteSpace(Message)
            );

        DisconnectCommand = new(
            o => _serverConn.DisconnectFromServer(),
            o => _serverConn.Connected
            );
    }

    private void OnConnectedToServer(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var welcomeMsg = new MessageModel()
        {
            Sender = UserModel.System,
            Message = $"Welcome to the chat."
        };
        Application.Current.Dispatcher.Invoke(() => Messages.Add(welcomeMsg));
    }

    private void OnUserListUpdated(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var newbie = new UserModel()
        {
            UID = Guid.Parse(_serverConn.ReadNextMessageSection()),
            Username = _serverConn.ReadNextMessageSection(),
        };
        if (Users.Any(u => u.UID == newbie.UID)) return;

        var newbieMsg = new MessageModel()
        {
            Sender = UserModel.System,
            Message = $"'{newbie.Username}' has joined the chat."
        };
        Application.Current.Dispatcher.Invoke(() => Users.Add(newbie));
        Application.Current.Dispatcher.Invoke(() => Messages.Add(newbieMsg));
    }

    private void OnUserChat(object? sender, EventArgs e)
    {
        if (sender is not ServerConnection serverConn)
            return;

        var uid = serverConn.ReadNextMessageSection();
        var msg = serverConn.ReadNextMessageSection();

        var src = Users.FirstOrDefault(
            u => u.UID == Guid.Parse(uid),
            UndefinedUser);

        var message = new MessageModel()
        {
            Sender = src,
            Message = msg,
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
            Sender = UserModel.System,
            Message = $"'{leaver.Username}' has left the chat.",
        };
        Application.Current.Dispatcher.Invoke(() => Users.Remove(leaver));
        Application.Current.Dispatcher.Invoke(() => Messages.Add(leaverMsg));
    }

    private static readonly UserModel UndefinedUser = new()
    {
        UID = Guid.Empty,
        Username = string.Empty,
    };
}

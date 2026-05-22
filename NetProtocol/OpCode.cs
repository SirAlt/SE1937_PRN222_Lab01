namespace NetProtocol;

public enum OpCode
{
    EOS = -1,
    Partial = 0,
    RegisterNew = 1,
    RegisterWorker,
    UidInfo,
    UserList,
    NewUser,
    Chat,
    Disconnect,
    FileTransfer,
    FileTransferGoAhead,
    FileRequest,
    FileRequestResponse,
}

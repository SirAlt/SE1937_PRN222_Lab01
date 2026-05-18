using System.Net.Sockets;
using System.Text;

namespace NetProtocol;

public class PacketReader(NetworkStream stream)
{
    public OpCode ReadOpCode()
    {
        return (OpCode)stream.ReadByte();
    }

    public string ReadMessage()
    {
        var lenBuffer = new byte[4];
        stream.ReadExactly(lenBuffer, 0, 4);
        var length = BitConverter.ToInt32(lenBuffer);

        var msgBuffer = new byte[length];
        stream.ReadExactly(msgBuffer, 0, length);
        var msg = Encoding.UTF8.GetString(msgBuffer);

        return msg;
    }
}

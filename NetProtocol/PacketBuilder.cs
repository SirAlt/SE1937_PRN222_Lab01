using System.IO;
using System.Text;

namespace NetProtocol;

public class PacketBuilder
{
    private readonly MemoryStream _ms;

    public PacketBuilder()
    {
        _ms = new MemoryStream();
    }

    public PacketBuilder WriteOpCode(OpCode opcode)
    {
        _ms.Position = 0;
        _ms.WriteByte((byte)opcode);
        return this;
    }

    public PacketBuilder WriteMessage(string msg)
    {
        if (_ms.Position == 0) _ms.Position = 1;
        var buffer = Encoding.UTF8.GetBytes(msg);
        _ms.Write(BitConverter.GetBytes(buffer.Length));
        _ms.Write(buffer);
        return this;
    }

    public byte[] Build()
    {
        return _ms.ToArray();
    }
}

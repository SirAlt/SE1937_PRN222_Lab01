using System.IO;
using System.Text;

namespace NetIO;

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
        _ms.Write(BitConverter.GetBytes(msg.Length));
        _ms.Write(Encoding.UTF8.GetBytes(msg));
        return this;
    }

    public byte[] Build()
    {
        return _ms.ToArray();
    }
}

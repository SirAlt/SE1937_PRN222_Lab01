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

    public PacketBuilder WriteMessageSection(string msg)
    {
        var buffer = Encoding.UTF8.GetBytes(msg);
        _ms.Write(BitConverter.GetBytes(buffer.Length));
        _ms.Write(buffer);
        return this;
    }

    public async Task<PacketBuilder> WriteMessageSectionAsync(string msg)
    {
        var buffer = Encoding.UTF8.GetBytes(msg);
        await _ms.WriteAsync(BitConverter.GetBytes(buffer.Length));
        await _ms.WriteAsync(buffer);
        return this;
    }

    public PacketBuilder WriteDataSection(byte[] data)
    {
        if (_ms.Position == 0) _ms.Position = 1;
        _ms.Write(BitConverter.GetBytes(data.Length));
        _ms.Write(data);
        return this;
    }

    public byte[] Build()
    {
        return _ms.ToArray();
    }
}

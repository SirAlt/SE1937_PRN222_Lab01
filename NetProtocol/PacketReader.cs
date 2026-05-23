using System.Net.Sockets;
using System.Text;

namespace NetProtocol;

public class PacketReader(NetworkStream stream)
{
    public const byte MessageHeaderLength = 4;
    public const byte DataHeaderLength = 8;

    public OpCode ReadOpCode()
    {
        return (OpCode)stream.ReadByte();
    }

    public string ReadMessageSection()
    {
        var lenBuffer = new byte[MessageHeaderLength];
        stream.ReadExactly(lenBuffer, 0, lenBuffer.Length);
        var length = BitConverter.ToInt32(lenBuffer);

        var msgBuffer = new byte[length];
        stream.ReadExactly(msgBuffer, 0, length);
        var msg = Encoding.UTF8.GetString(msgBuffer);

        return msg;
    }

    public async Task ReadDataSectionAsync(Stream output)
    {
        var lenBuffer = new byte[DataHeaderLength];
        await stream.ReadExactlyAsync(lenBuffer, 0, lenBuffer.Length);
        var length = BitConverter.ToInt64(lenBuffer);

        var bufferSize = 65536;
        var dataBuffer = new byte[bufferSize];
        var remaining = length;
        while (remaining > 0)
        {
            var byteCount = (int)Math.Min(remaining, dataBuffer.Length);
            await stream.ReadExactlyAsync(dataBuffer, 0, byteCount);
            await output.WriteAsync(dataBuffer.AsMemory(0, byteCount));
            remaining -= byteCount;
        }
    }
}

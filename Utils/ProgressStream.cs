namespace Utils;

public class ProgressStream(Stream backingStream) : Stream
{
    public event EventHandler<ProgressEventArgs>? ProgressUpdated;

    private long _position = 0;
    private long _length = backingStream.Length;

    public override bool CanRead => backingStream.CanRead;
    public override bool CanWrite => backingStream.CanWrite;
    public override bool CanSeek => backingStream.CanSeek;

    public override long Length => backingStream.Length;
    public override long Position { get => backingStream.Position; set => backingStream.Position = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = backingStream.Read(buffer, offset, count);
        _position += bytesRead;
        ProgressUpdated?.Invoke(this, new ProgressEventArgs(1d * _position / _length));
        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        backingStream.Write(buffer, offset, count);
        _position += count;
        ProgressUpdated?.Invoke(this, new ProgressEventArgs(1d * _position / _length));
    }

    public override long Seek(long offset, SeekOrigin origin) => backingStream.Seek(offset, origin);

    public override void SetLength(long value) => backingStream.SetLength(value);

    public override void Flush() => backingStream.Flush();
}

public class ProgressEventArgs(double progressPercentage) : EventArgs
{
    public double Progress => progressPercentage;
}

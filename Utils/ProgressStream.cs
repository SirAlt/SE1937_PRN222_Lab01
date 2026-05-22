namespace Utils;

public class ProgressStream(Stream backingStream, long length = -1) : Stream
{
    public event EventHandler<ProgressEventArgs>? ProgressUpdated;

    private readonly long _length = length > 0 ? length : backingStream.Length;
    private long _position = 0;

    public override bool CanRead => backingStream.CanRead;
    public override bool CanWrite => backingStream.CanWrite;
    public override bool CanSeek => backingStream.CanSeek;

    public override long Length => _length;
    public override long Position { get => backingStream.Position; set => backingStream.Position = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = backingStream.Read(buffer, offset, count);
        _position += bytesRead;
        ProgressUpdated?.Invoke(this, new ProgressEventArgs(_position, 1d * _position / _length));
        return bytesRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        backingStream.Write(buffer, offset, count);
        _position += count;
        ProgressUpdated?.Invoke(this, new ProgressEventArgs(_position, 1d * _position / _length));
    }

    public override long Seek(long offset, SeekOrigin origin) => backingStream.Seek(offset, origin);

    public override void SetLength(long value) => backingStream.SetLength(value);

    public override void Flush() => backingStream.Flush();
}

public class ProgressEventArgs(long progress, double progressPercentage) : EventArgs
{
    public long Progress => progress;
    public double ProgressPercentage => progressPercentage;
}

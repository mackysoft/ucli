namespace MackySoft.Tests;

internal sealed class ThrowingReadStream : Stream
{
    private readonly Exception exception;

    public ThrowingReadStream (Exception exception)
    {
        this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush ()
    {
        throw new NotSupportedException();
    }

    public override int Read (
        byte[] buffer,
        int offset,
        int count)
    {
        throw exception;
    }

    public override Task<int> ReadAsync (
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        return Task.FromException<int>(exception);
    }

    public override ValueTask<int> ReadAsync (
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromException<int>(exception);
    }

    public override long Seek (
        long offset,
        SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength (long value)
    {
        throw new NotSupportedException();
    }

    public override void Write (
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
    }
}

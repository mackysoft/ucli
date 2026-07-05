namespace MackySoft.Tests;

internal sealed class ConcurrentWriteDetectingStream : Stream
{
    private readonly MemoryStream output = new();

    private int activeWriteCount;

    private int hasOverlappingWrite;

    public bool HasOverlappingWrite => Volatile.Read(ref hasOverlappingWrite) != 0;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public byte[] ToArray ()
    {
        lock (output)
        {
            return output.ToArray();
        }
    }

    public override void Flush ()
    {
    }

    public override Task FlushAsync (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public override int Read (
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
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
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    public override async ValueTask WriteAsync (
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref activeWriteCount) > 1)
        {
            Volatile.Write(ref hasOverlappingWrite, 1);
        }

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
            lock (output)
            {
                output.Write(buffer.Span);
            }
        }
        finally
        {
            Interlocked.Decrement(ref activeWriteCount);
        }
    }
}

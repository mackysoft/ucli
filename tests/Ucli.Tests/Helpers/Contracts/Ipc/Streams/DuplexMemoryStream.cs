namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class DuplexMemoryStream : Stream
{
    private readonly byte[] input;
    private readonly MemoryStream output = new();
    private int inputOffset;

    public DuplexMemoryStream (byte[] input)
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public bool FailWrites { get; set; }

    public int FailWriteCount { get; set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public byte[] GetWrittenBytes ()
    {
        return output.ToArray();
    }

    public override void Flush ()
    {
    }

    public override int Read (
        byte[] buffer,
        int offset,
        int count)
    {
        if (inputOffset >= input.Length)
        {
            return 0;
        }

        var bytesRead = Math.Min(count, input.Length - inputOffset);
        input.AsSpan(inputOffset, bytesRead).CopyTo(buffer.AsSpan(offset, bytesRead));
        inputOffset += bytesRead;
        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync (
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (inputOffset < input.Length)
        {
            var bytesRead = Math.Min(buffer.Length, input.Length - inputOffset);
            input.AsMemory(inputOffset, bytesRead).CopyTo(buffer);
            inputOffset += bytesRead;
            return bytesRead;
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        return 0;
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
        if (ShouldFailWrite())
        {
            throw new IOException("Simulated caller disconnect.");
        }

        output.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync (
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFailWrite())
        {
            throw new IOException("Simulated caller disconnect.");
        }

        output.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    private bool ShouldFailWrite ()
    {
        if (FailWrites)
        {
            return true;
        }

        if (FailWriteCount <= 0)
        {
            return false;
        }

        FailWriteCount--;
        return true;
    }
}

namespace MackySoft.Ucli.Tests.Supervisor;

internal sealed class SupervisorControlledWriteStream : Stream
{
    private readonly MemoryStream requestStream;

    private readonly TaskCompletionSource writeStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource writeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource disposeStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource disposeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int firstWriteEntered;

    public SupervisorControlledWriteStream (byte[] requestBytes)
    {
        ArgumentNullException.ThrowIfNull(requestBytes);
        requestStream = new MemoryStream(requestBytes, writable: false);
    }

    public Task WriteStarted => writeStarted.Task;

    public Task DisposeStarted => disposeStarted.Task;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void CompleteWrite ()
    {
        writeCompletion.TrySetResult();
    }

    public void CompleteDispose ()
    {
        disposeCompletion.TrySetResult();
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
        return requestStream.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync (
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return requestStream.ReadAsync(buffer, cancellationToken);
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
        BlockFirstWrite();
    }

    public override ValueTask WriteAsync (
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        BlockFirstWrite();
        return ValueTask.CompletedTask;
    }

    protected override void Dispose (bool disposing)
    {
        if (disposing)
        {
            disposeStarted.TrySetResult();
            disposeCompletion.Task.GetAwaiter().GetResult();
            requestStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BlockFirstWrite ()
    {
        if (Interlocked.Exchange(ref firstWriteEntered, 1) != 0)
        {
            return;
        }

        writeStarted.TrySetResult();
        writeCompletion.Task.GetAwaiter().GetResult();
    }
}

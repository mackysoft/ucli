namespace MackySoft.Ucli.Tests.Supervisor;

internal enum SupervisorControlledReadMode
{
    AsynchronousCancellationAware,
    AsynchronousIgnoringCancellation,
    SynchronousBeforeValueTaskReturn,
}

internal sealed class SupervisorControlledReadStream : Stream
{
    private readonly SupervisorControlledReadMode mode;

    private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<int> readCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource readReturned = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SupervisorControlledReadStream (SupervisorControlledReadMode mode)
    {
        this.mode = mode;
    }

    public Task ReadStarted => readStarted.Task;

    public Task ReadReturned => readReturned.Task;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public void CompleteRead ()
    {
        readCompletion.TrySetResult(0);
    }

    public void FailRead (Exception exception)
    {
        readCompletion.TrySetException(exception);
    }

    public override void Flush ()
    {
    }

    public override int Read (
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
    }

    public override ValueTask<int> ReadAsync (
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        readStarted.TrySetResult();
        if (mode == SupervisorControlledReadMode.SynchronousBeforeValueTaskReturn)
        {
            var bytesRead = readCompletion.Task.GetAwaiter().GetResult();
            readReturned.TrySetResult();
            return new ValueTask<int>(bytesRead);
        }

        return new ValueTask<int>(CompleteReadAsync(cancellationToken));
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
    }

    private async Task<int> CompleteReadAsync (CancellationToken cancellationToken)
    {
        using var cancellationRegistration = mode == SupervisorControlledReadMode.AsynchronousCancellationAware
            ? cancellationToken.Register(
                static state =>
                {
                    var (completion, token) = ((TaskCompletionSource<int>, CancellationToken))state!;
                    completion.TrySetCanceled(token);
                },
                (readCompletion, cancellationToken))
            : default;
        try
        {
            return await readCompletion.Task.ConfigureAwait(false);
        }
        finally
        {
            readReturned.TrySetResult();
        }
    }
}

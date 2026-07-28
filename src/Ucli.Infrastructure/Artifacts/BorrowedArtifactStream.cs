namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Exposes a time-bounded, non-owning read-only or write-only view of one publisher-owned stream. </summary>
internal sealed class BorrowedArtifactStream : Stream
{
    private readonly Stream inner;
    private readonly bool allowRead;
    private readonly bool allowWrite;
    private int isActive = 1;

    private BorrowedArtifactStream (
        Stream inner,
        bool allowRead,
        bool allowWrite)
    {
        this.inner = inner;
        this.allowRead = allowRead;
        this.allowWrite = allowWrite;
    }

    public override bool CanRead => IsActive && allowRead && inner.CanRead;

    public override bool CanSeek => IsActive && inner.CanSeek;

    public override bool CanWrite => IsActive && allowWrite && inner.CanWrite;

    public override long Length
    {
        get
        {
            EnsureActive();
            return inner.Length;
        }
    }

    public override long Position
    {
        get
        {
            EnsureActive();
            return inner.Position;
        }

        set
        {
            EnsureActive();
            inner.Position = value;
        }
    }

    public static BorrowedArtifactStream CreateReadOnly (Stream inner)
    {
        return new BorrowedArtifactStream(
            inner ?? throw new ArgumentNullException(nameof(inner)),
            allowRead: true,
            allowWrite: false);
    }

    public static BorrowedArtifactStream CreateWriteOnly (Stream inner)
    {
        return new BorrowedArtifactStream(
            inner ?? throw new ArgumentNullException(nameof(inner)),
            allowRead: false,
            allowWrite: true);
    }

    public void Invalidate ()
    {
        Volatile.Write(ref isActive, 0);
    }

    public override void Flush ()
    {
        EnsureWritable();
        inner.Flush();
    }

    public override Task FlushAsync (CancellationToken cancellationToken)
    {
        EnsureWritable();
        return inner.FlushAsync(cancellationToken);
    }

    public override int Read (
        byte[] buffer,
        int offset,
        int count)
    {
        EnsureReadable();
        return inner.Read(buffer, offset, count);
    }

    public override int Read (Span<byte> buffer)
    {
        EnsureReadable();
        return inner.Read(buffer);
    }

    public override Task<int> ReadAsync (
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        EnsureReadable();
        return inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync (
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        EnsureReadable();
        return inner.ReadAsync(buffer, cancellationToken);
    }

    public override int ReadByte ()
    {
        EnsureReadable();
        return inner.ReadByte();
    }

    public override long Seek (
        long offset,
        SeekOrigin origin)
    {
        EnsureActive();
        return inner.Seek(offset, origin);
    }

    public override void SetLength (long value)
    {
        EnsureWritable();
        inner.SetLength(value);
    }

    public override void Write (
        byte[] buffer,
        int offset,
        int count)
    {
        EnsureWritable();
        inner.Write(buffer, offset, count);
    }

    public override void Write (ReadOnlySpan<byte> buffer)
    {
        EnsureWritable();
        inner.Write(buffer);
    }

    public override Task WriteAsync (
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync (
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        return inner.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte (byte value)
    {
        EnsureWritable();
        inner.WriteByte(value);
    }

    protected override void Dispose (bool disposing)
    {
        // The publisher retains the underlying stream. Callback disposal therefore does not transfer ownership.
    }

    private void EnsureActive ()
    {
        if (!IsActive)
        {
            throw new ObjectDisposedException(
                nameof(BorrowedArtifactStream),
                "The borrowed artifact stream is no longer valid.");
        }
    }

    private bool IsActive => Volatile.Read(ref isActive) != 0;

    private void EnsureReadable ()
    {
        EnsureActive();
        if (!allowRead)
        {
            throw new NotSupportedException("The borrowed artifact stream is write-only.");
        }
    }

    private void EnsureWritable ()
    {
        EnsureActive();
        if (!allowWrite)
        {
            throw new NotSupportedException("The borrowed artifact stream is read-only.");
        }
    }
}

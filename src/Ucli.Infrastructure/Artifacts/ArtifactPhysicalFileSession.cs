using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Retains one owned file handle and the physical repository path identities to which it is bound. </summary>
internal sealed class ArtifactPhysicalFileSession : IDisposable
{
    private const int FileReadBufferSize = 4096;
    private readonly FileStream stream;
    private bool isDisposed;

    private ArtifactPhysicalFileSession (
        ArtifactPhysicalFileRequest request,
        FileStream stream,
        FileSystemNodeIdentity identity,
        ArtifactPhysicalPathSnapshot binding)
    {
        Request = request;
        this.stream = stream;
        Binding = binding;
        Identity = identity;
    }

    internal ArtifactPhysicalPathSnapshot Binding { get; private set; }

    internal FileSystemNodeIdentity Identity { get; }

    internal ArtifactPhysicalFileRequest Request { get; private set; }

    public static ArtifactPhysicalFileSession Open (
        ArtifactPhysicalFileRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var beforeOpen = ArtifactPhysicalPathSnapshot.Capture(request);
        var stream = OpenRead(request.RepositoryFile.Target);
        try
        {
            return CompleteOpen(request, stream, beforeOpen);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a publisher-owned private file beside the destination and retains its read/write handle.
    /// </summary>
    public static ArtifactPhysicalFileSession CreateNewBeside (
        ContainedPath repositoryDestinationFile,
        string subject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (repositoryDestinationFile is null)
        {
            throw new ArgumentNullException(nameof(repositoryDestinationFile));
        }

        var beforeCreate = ArtifactPhysicalDirectorySnapshot.Capture(
            repositoryDestinationFile,
            subject);
        var destinationDirectory = GetParent(repositoryDestinationFile);
        var stream = FileUtilities.OpenAtomicReadWriteTemporaryFileInDirectory(
            destinationDirectory,
            out var temporaryPath);
        try
        {
            return CompleteCreate(
                repositoryDestinationFile,
                subject,
                temporaryPath,
                stream,
                beforeCreate);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public async ValueTask WriteAsync (
        Func<Stream, CancellationToken, ValueTask> writeAsync,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (writeAsync is null)
        {
            throw new ArgumentNullException(nameof(writeAsync));
        }

        EnsureStillBound();
        stream.SetLength(0);
        stream.Position = 0;
        var borrowedStream = BorrowedArtifactStream.CreateWriteOnly(stream);
        try
        {
            await writeAsync(borrowedStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            borrowedStream.Invalidate();
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        EnsureStillBound();
    }

    public async ValueTask<ImmutableArtifactFileReadBoundary.Measurement> MeasureAsync (
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        EnsureStillBound();
        var measurement = await ArtifactFileMeasurementReader
            .MeasureAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        EnsureStillBound();
        return measurement;
    }

    public async ValueTask ValidateAsync (
        Func<Stream, CancellationToken, ValueTask> validateAsync,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (validateAsync is null)
        {
            throw new ArgumentNullException(nameof(validateAsync));
        }

        EnsureStillBound();
        stream.Position = 0;
        var borrowedStream = BorrowedArtifactStream.CreateReadOnly(stream);
        try
        {
            await validateAsync(borrowedStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            borrowedStream.Invalidate();
        }

        EnsureStillBound();
    }

    public void MoveBindingTo (
        ContainedPath destinationFile,
        string subject)
    {
        ThrowIfDisposed();
        Request = ArtifactPhysicalFileRequest.Create(destinationFile, subject);
        var movedBinding = ArtifactPhysicalPathSnapshot.Capture(Request);
        Binding.Directories.EnsureSamePathAs(movedBinding.Directories, subject);
        movedBinding.EnsureLeafIs(Identity, subject);
        EnsureHandleIdentity();
        Binding = movedBinding;
    }

    public void EnsureSameNodeAs (
        ArtifactPhysicalFileSession other,
        string subject)
    {
        ThrowIfDisposed();
        if (other is null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        other.ThrowIfDisposed();
        if (Identity != other.Identity)
        {
            throw new IOException(
                $"{subject} reopened path does not identify the published physical node: {Request.RepositoryFile.Target.Value}");
        }
    }

    public void EnsureStillBound ()
    {
        ThrowIfDisposed();
        var current = ArtifactPhysicalPathSnapshot.Capture(Request);
        Binding.EnsureSamePathAs(current, Request.Subject);
        current.EnsureLeafIs(Identity, Request.Subject);
        EnsureHandleIdentity();
    }

    public void Dispose ()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        stream.Dispose();
    }

    private static ArtifactPhysicalFileSession CompleteOpen (
        ArtifactPhysicalFileRequest request,
        FileStream stream,
        ArtifactPhysicalPathSnapshot beforeOpen)
    {
        var handleIdentity = FileSystemNodeIdentityReader.ReadHandle(stream, request.Subject);
        request.EnsureRegularSingleEntryFile(handleIdentity);
        var afterOpen = ArtifactPhysicalPathSnapshot.Capture(request);
        beforeOpen.EnsureSamePathAs(afterOpen, request.Subject);
        afterOpen.EnsureLeafIs(handleIdentity, request.Subject);
        return new ArtifactPhysicalFileSession(request, stream, handleIdentity, afterOpen);
    }

    private static ArtifactPhysicalFileSession CompleteCreate (
        ContainedPath repositoryDestinationFile,
        string subject,
        AbsolutePath temporaryPath,
        FileStream stream,
        ArtifactPhysicalDirectorySnapshot beforeCreate)
    {
        var temporaryFile = ContainedPath.Create(
            repositoryDestinationFile.BoundaryRoot,
            temporaryPath);
        var request = ArtifactPhysicalFileRequest.Create(temporaryFile, subject);
        var handleIdentity = FileSystemNodeIdentityReader.ReadHandle(stream, subject);
        request.EnsureRegularSingleEntryFile(handleIdentity);
        var binding = ArtifactPhysicalPathSnapshot.Capture(request);
        beforeCreate.EnsureSamePathAs(binding.Directories, subject);
        binding.EnsureLeafIs(handleIdentity, subject);
        return new ArtifactPhysicalFileSession(request, stream, handleIdentity, binding);
    }

    private static FileStream OpenRead (AbsolutePath file)
    {
        return new FileStream(
            file.Value,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            FileReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static AbsolutePath GetParent (ContainedPath file)
    {
        if (!file.Target.TryGetParent(out var parent))
        {
            throw new ArgumentException(
                "Artifact destination parent directory could not be resolved.",
                nameof(file));
        }

        return parent;
    }

    private void EnsureHandleIdentity ()
    {
        var currentIdentity = FileSystemNodeIdentityReader.ReadHandle(stream, Request.Subject);
        if (currentIdentity != Identity)
        {
            throw new IOException(
                $"{Request.Subject} open handle changed physical node identity: {Request.RepositoryFile.Target.Value}");
        }
    }

    private void ThrowIfDisposed ()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(ArtifactPhysicalFileSession));
        }
    }
}

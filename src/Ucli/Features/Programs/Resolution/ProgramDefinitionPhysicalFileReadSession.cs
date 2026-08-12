using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Programs.Resolution;

/// <summary> Captures the physical path state required to read one Program definition file. </summary>
internal sealed class ProgramDefinitionPhysicalPathSnapshot
{
    private readonly IReadOnlyList<PathIdentity> identityChain;

    private ProgramDefinitionPhysicalPathSnapshot (
        AbsolutePath target,
        IReadOnlyList<PathIdentity> identityChain)
    {
        Target = target;
        this.identityChain = identityChain;
    }

    /// <summary> Captures the current path identity chain between a resolved boundary and target. </summary>
    public static ProgramDefinitionPhysicalPathSnapshot Capture (AbsolutePath boundaryRoot, AbsolutePath target)
    {
        ArgumentNullException.ThrowIfNull(boundaryRoot);
        ArgumentNullException.ThrowIfNull(target);

        var reverse = new Stack<AbsolutePath>();
        for (var current = target; ;)
        {
            reverse.Push(current);
            if (current.IsSameAs(boundaryRoot))
            {
                break;
            }

            if (!current.TryGetParent(out var parent) || !boundaryRoot.IsSameOrAncestorOf(parent))
            {
                throw new IOException("Program definition path escaped its physical reference root.");
            }

            current = parent;
        }

        var identityChain = reverse
            .Select(static current => new PathIdentity(
                current,
                FileSystemNodeIdentityReader.ReadPath(current, "Program definition file")))
            .ToArray();
        return new ProgramDefinitionPhysicalPathSnapshot(target, identityChain);
    }

    /// <summary> Gets the resolved physical target. </summary>
    public AbsolutePath Target { get; }

    /// <summary> Gets a result when the captured nodes cannot be read as a regular file. </summary>
    public ProgramDefinitionFileReadResult? GetReadabilityFailure ()
    {
        if (identityChain.Take(identityChain.Count - 1).Any(static item => !item.Identity.IsDirectory || item.Identity.IsReparsePoint))
        {
            return new ProgramDefinitionFileReadUnavailable("Program definition file reference ancestors must resolve to non-reparse directories.");
        }

        if (!identityChain[^1].Identity.IsRegularFile || identityChain[^1].Identity.IsReparsePoint)
        {
            return new ProgramDefinitionFileReadUnavailable("Program definition file must resolve to a regular file.");
        }

        return null;
    }

    /// <summary> Gets whether every captured path still resolves to the same physical node. </summary>
    public bool HasSameIdentityChain ()
    {
        foreach (var item in identityChain)
        {
            var actual = FileSystemNodeIdentityReader.ReadPath(item.Path, "Program definition file");
            if (!actual.IsSamePhysicalNodeAs(item.Identity))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary> Gets whether an opened file handle identifies the captured leaf. </summary>
    public bool IsSameRegularFile (FileSystemNodeIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return identity.IsRegularFile
            && !identity.IsReparsePoint
            && identity.IsSamePhysicalNodeAs(identityChain[^1].Identity);
    }

    private sealed record PathIdentity (AbsolutePath Path, FileSystemNodeIdentity Identity);
}

/// <summary> Holds an opened Program definition file handle until its contents and path identity are confirmed. </summary>
internal sealed class ProgramDefinitionPhysicalFileReadSession : IAsyncDisposable
{
    private readonly ProgramDefinitionPhysicalPathSnapshot snapshot;
    private readonly FileStream stream;
    private byte[]? content;
    private bool readStarted;
    private bool completed;

    private ProgramDefinitionPhysicalFileReadSession (ProgramDefinitionPhysicalPathSnapshot snapshot, FileStream stream)
    {
        this.snapshot = snapshot;
        this.stream = stream;
    }

    /// <summary> Opens the captured target and verifies the handle still represents its captured leaf. </summary>
    public static ProgramDefinitionFileReadResult? TryOpen (
        ProgramDefinitionPhysicalPathSnapshot snapshot,
        out ProgramDefinitionPhysicalFileReadSession? session)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var stream = new FileStream(
            snapshot.Target.Value,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            var handleIdentity = FileSystemNodeIdentityReader.ReadHandle(stream, "Program definition file");
            if (!snapshot.IsSameRegularFile(handleIdentity))
            {
                stream.Dispose();
                session = null;
                return new ProgramDefinitionFileReadChangedDuringRead();
            }

            session = new ProgramDefinitionPhysicalFileReadSession(snapshot, stream);
            return null;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary> Reads the opened file into this session while retaining its physical handle. </summary>
    public async ValueTask ReadContentAsync (CancellationToken cancellationToken)
    {
        if (readStarted || completed)
        {
            throw new InvalidOperationException("Program definition file read session can only read once.");
        }

        readStarted = true;
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        content = output.ToArray();
    }

    /// <summary> Confirms the captured path identity after reading and creates the closed read result. </summary>
    public ProgramDefinitionFileReadResult CompleteRead ()
    {
        if (completed || content is null)
        {
            throw new InvalidOperationException("Program definition file read session must complete after one successful read.");
        }

        completed = true;
        var completedContent = content;
        content = null;
        if (!snapshot.HasSameIdentityChain())
        {
            return new ProgramDefinitionFileReadChangedDuringRead();
        }

        return new ProgramDefinitionFileReadSuccess(completedContent, snapshot.Target);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync () => stream.DisposeAsync();
}

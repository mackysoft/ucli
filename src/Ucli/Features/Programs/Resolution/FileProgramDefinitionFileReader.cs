using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Programs.Resolution;

/// <summary> Reads Program definition documents from a physically confirmed filesystem boundary. </summary>
internal sealed class FileProgramDefinitionFileReader : IProgramDefinitionFileReader
{
    public async ValueTask<ProgramDefinitionFileReadResult> ReadAsync (
        ContainedPath path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var physicalRoot = ResolvePhysicalPath(path.BoundaryRoot);
            var physicalTarget = ResolvePhysicalPath(path.Target);
            if (!physicalRoot.IsSameOrAncestorOf(physicalTarget))
            {
                return new ProgramDefinitionFileReadOutsideBoundary();
            }

            var before = CaptureIdentityChain(physicalRoot, physicalTarget);
            if (before.Take(before.Count - 1).Any(static item => !item.Identity.IsDirectory || item.Identity.IsReparsePoint))
            {
                return new ProgramDefinitionFileReadUnavailable("Program definition file reference ancestors must resolve to non-reparse directories.");
            }

            if (!before[^1].Identity.IsRegularFile || before[^1].Identity.IsReparsePoint)
            {
                return new ProgramDefinitionFileReadUnavailable("Program definition file must resolve to a regular file.");
            }

            await using var stream = new FileStream(
                physicalTarget.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var handleIdentity = FileSystemNodeIdentityReader.ReadHandle(stream, "Program definition file");
            if (!handleIdentity.IsRegularFile
                || handleIdentity.IsReparsePoint
                || !handleIdentity.IsSamePhysicalNodeAs(before[^1].Identity))
            {
                return new ProgramDefinitionFileReadChangedDuringRead();
            }

            var content = await ReadAllAsync(stream, cancellationToken).ConfigureAwait(false);
            if (!HasSameIdentityChain(before))
            {
                return new ProgramDefinitionFileReadChangedDuringRead();
            }

            return new ProgramDefinitionFileReadSuccess(content, physicalTarget);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or PlatformNotSupportedException)
        {
            return new ProgramDefinitionFileReadUnavailable($"Failed to read Program definition file '{path.Target.Value}'. {exception.Message}");
        }
    }

    private static AbsolutePath ResolvePhysicalPath (AbsolutePath path)
    {
        var root = Path.GetPathRoot(path.Value)
            ?? throw new IOException($"Program definition path has no filesystem root: {path.Value}");
        var current = AbsolutePath.Parse(root);
        var relative = path.Value[root.Length..];
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = AbsolutePath.Resolve(current, segment);
            var target = TryResolveLinkTarget(current.Value);
            if (target is not null)
            {
                current = ResolvePhysicalPath(AbsolutePath.Parse(target));
            }
        }

        return current;
    }

    private static string? TryResolveLinkTarget (string path)
    {
        FileSystemInfo info = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
    }

    private static IReadOnlyList<PathIdentity> CaptureIdentityChain (AbsolutePath root, AbsolutePath target)
    {
        var reverse = new Stack<AbsolutePath>();
        for (var current = target; ;)
        {
            reverse.Push(current);
            if (current.IsSameAs(root))
            {
                break;
            }

            if (!current.TryGetParent(out var parent) || !root.IsSameOrAncestorOf(parent))
            {
                throw new IOException("Program definition path escaped its physical reference root.");
            }

            current = parent;
        }

        return reverse
            .Select(static current => new PathIdentity(
                current,
                FileSystemNodeIdentityReader.ReadPath(current, "Program definition file")))
            .ToArray();
    }

    private static bool HasSameIdentityChain (IReadOnlyList<PathIdentity> expected)
    {
        foreach (var item in expected)
        {
            var actual = FileSystemNodeIdentityReader.ReadPath(item.Path, "Program definition file");
            if (!actual.IsSamePhysicalNodeAs(item.Identity))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<byte[]> ReadAllAsync (FileStream stream, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    private sealed record PathIdentity (AbsolutePath Path, FileSystemNodeIdentity Identity);
}

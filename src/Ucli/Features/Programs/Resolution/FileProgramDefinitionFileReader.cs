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
            if (!PhysicalPathResolver.TryResolve(
                    path,
                    SymbolicLinkHandling.Follow,
                    MissingPathHandling.Reject,
                    out var resolution,
                    out var failure))
            {
                return CreateResolutionFailureResult(path, failure);
            }

            var physicalRoot = resolution.ResolvedPath.BoundaryRoot;
            var physicalTarget = resolution.ResolvedPath.Target;
            var snapshot = ProgramDefinitionPhysicalPathSnapshot.Capture(physicalRoot, physicalTarget);
            if (snapshot.GetReadabilityFailure() is { } readabilityFailure)
            {
                return readabilityFailure;
            }

            if (ProgramDefinitionPhysicalFileReadSession.TryOpen(snapshot, out var session) is { } openFailure)
            {
                return openFailure;
            }

            await using var openedSession = session!;
            var content = await openedSession.ReadContentAsync(cancellationToken).ConfigureAwait(false);
            return openedSession.CompleteRead(content);
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

    private static ProgramDefinitionFileReadResult CreateResolutionFailureResult (
        ContainedPath requestedPath,
        FileSystemOperationFailure failure)
    {
        return failure.Kind switch
        {
            FileSystemOperationFailureKind.OutsideBoundary => new ProgramDefinitionFileReadOutsideBoundary(),
            FileSystemOperationFailureKind.ConcurrentChange => new ProgramDefinitionFileReadChangedDuringRead(),
            FileSystemOperationFailureKind.EntryNotFound
                or FileSystemOperationFailureKind.AccessDenied
                or FileSystemOperationFailureKind.LinkCycle
                or FileSystemOperationFailureKind.UnexpectedEntryKind
                or FileSystemOperationFailureKind.PlatformNotSupported
                or FileSystemOperationFailureKind.IoFailure => new ProgramDefinitionFileReadUnavailable(
                    $"Failed to resolve Program definition file '{requestedPath.Target.Value}'. {failure.Message}"),
            _ => throw new InvalidOperationException($"Unexpected Program definition path resolution failure: {failure.Kind}."),
        };
    }
}

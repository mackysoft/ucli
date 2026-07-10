using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorManifestStoreTestSupport
{
    public static SupervisorManifestStore CreateFileBacked (TimeProvider timeProvider)
    {
        return new SupervisorManifestStore(
            timeProvider,
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllTextAtomicallyAsync(path, contents, cancellationToken),
            static path => FileUtilities.DeleteIfExists(path));
    }
}

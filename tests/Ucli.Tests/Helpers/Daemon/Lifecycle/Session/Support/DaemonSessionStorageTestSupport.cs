using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonSessionStorageTestSupport
{
    public static DaemonSessionStore CreateStore ()
    {
        return new DaemonSessionStore();
    }

    public static async Task WriteJsonAsync (
        string storageRoot,
        string projectFingerprint,
        string json,
        CancellationToken cancellationToken = default)
    {
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(sessionPath, json, cancellationToken).ConfigureAwait(false);
    }
}

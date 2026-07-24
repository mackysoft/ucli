using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonSessionStorageTestSupport
{
    public static DaemonSessionStore CreateStore ()
    {
        return new DaemonSessionStore();
    }

    public static async Task WriteJsonAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        string json,
        CancellationToken cancellationToken = default)
    {
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath.Value)!);
        await File.WriteAllTextAsync(sessionPath.Value, json, cancellationToken).ConfigureAwait(false);
    }
}

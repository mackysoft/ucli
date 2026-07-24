using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class ObservedPluginMarkerCacheStore
{
    private TaskCompletionSource<bool> nextWrite =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ObservedPluginMarkerCacheStore ()
    {
        CacheStore = new UnityUcliPluginMarkerCacheStore(
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken),
            WriteAllTextAtomicallyAsync,
            static path => FileUtilities.DeleteIfExists(path));
    }

    public UnityUcliPluginMarkerCacheStore CacheStore { get; }

    public Task ExpectWriteAsync ()
    {
        nextWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return nextWrite.Task;
    }

    private async ValueTask WriteAllTextAtomicallyAsync (
        AbsolutePath path,
        string contents,
        CancellationToken cancellationToken)
    {
        try
        {
            await FileUtilities.WriteAllTextAtomicallyAsync(path, contents, cancellationToken);
        }
        finally
        {
            nextWrite.TrySetResult(true);
        }
    }
}

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project.Plugin.Marker;

namespace MackySoft.Ucli.UnityIntegration.Project.Plugin.Cache;

/// <summary> Coordinates fingerprint-scoped plugin marker cache read/write/delete behavior around locator workflows. </summary>
internal sealed class UnityUcliPluginMarkerCacheCoordinator
{
    private readonly UnityUcliPluginMarkerCacheStore pluginMarkerCacheStore;

    private readonly UnityUcliPluginMarkerValidator pluginMarkerValidator;

    private readonly SemaphoreSlim cacheMutationGate = new(1, 1);

    private long cacheMutationVersion;

    /// <summary> Initializes a new instance of the <see cref="UnityUcliPluginMarkerCacheCoordinator" /> class. </summary>
    public UnityUcliPluginMarkerCacheCoordinator (
        UnityUcliPluginMarkerCacheStore pluginMarkerCacheStore,
        UnityUcliPluginMarkerValidator pluginMarkerValidator)
    {
        this.pluginMarkerCacheStore = pluginMarkerCacheStore ?? throw new ArgumentNullException(nameof(pluginMarkerCacheStore));
        this.pluginMarkerValidator = pluginMarkerValidator ?? throw new ArgumentNullException(nameof(pluginMarkerValidator));
    }

    /// <summary> Tries to resolve one valid marker from cache. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One located marker result when cache succeeded; otherwise <see langword="null" />. </returns>
    public async ValueTask<UnityUcliPluginLocateResult?> TryLocateFromCache (
        string unityProjectRoot,
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);

        var cacheReadResult = await pluginMarkerCacheStore.ReadOrNull(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cacheReadResult.IsSuccess)
        {
            if (cacheReadResult.Error!.Kind == ExecutionErrorKind.InvalidArgument)
            {
                DeleteBestEffort(storageRoot, projectFingerprint);
            }

            return null;
        }

        var cache = cacheReadResult.Cache;
        if (cache == null)
        {
            return null;
        }

        if (!string.Equals(cache.PluginId, UnityUcliPluginLocator.ExpectedPluginId, StringComparison.Ordinal)
            || cache.ProtocolVersion != UnityUcliPluginLocator.ExpectedProtocolVersion)
        {
            DeleteBestEffort(storageRoot, projectFingerprint);
            return null;
        }

        if (!pluginMarkerValidator.TryResolveCachedMarkerPath(
                unityProjectRoot,
                cache.ProjectRelativeMarkerPath,
                out var cachedMarkerPath)
            || string.IsNullOrWhiteSpace(cachedMarkerPath))
        {
            DeleteBestEffort(storageRoot, projectFingerprint);
            return null;
        }

        var markerError = await pluginMarkerValidator.ValidateMarker(cachedMarkerPath, cancellationToken).ConfigureAwait(false);
        if (markerError != null)
        {
            DeleteBestEffort(storageRoot, projectFingerprint);
            return null;
        }

        return UnityUcliPluginLocateResult.Found(cachedMarkerPath, UnityUcliPluginLocator.ExpectedProtocolVersion);
    }

    /// <summary> Queues one best-effort cache write for one resolved marker path. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="markerPath"> The resolved absolute marker path. </param>
    public void WriteBestEffort (
        string unityProjectRoot,
        string storageRoot,
        string projectFingerprint,
        string markerPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(markerPath);

        if (!pluginMarkerValidator.TryCreateProjectRelativeMarkerPath(
                unityProjectRoot,
                markerPath,
                out var projectRelativeMarkerPath)
            || string.IsNullOrWhiteSpace(projectRelativeMarkerPath))
        {
            return;
        }

        var cache = new UnityUcliPluginMarkerCache(
            projectRelativeMarkerPath,
            UnityUcliPluginLocator.ExpectedPluginId,
            UnityUcliPluginLocator.ExpectedProtocolVersion);

        // NOTE:
        // marker cache is an optimization layer under .ucli/local and must not change
        // the primary command outcome when persistence is unavailable or delayed.
        var cacheMutationVersion = Interlocked.Increment(ref this.cacheMutationVersion);
        _ = PersistCacheWriteBestEffort(
            storageRoot,
            projectFingerprint,
            cache,
            cacheMutationVersion);
    }

    /// <summary> Queues one best-effort cache delete. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    public void DeleteBestEffort (
        string storageRoot,
        string projectFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);

        // NOTE:
        // stale marker cache should never block fallback scanning.
        var cacheMutationVersion = Interlocked.Increment(ref this.cacheMutationVersion);
        _ = PersistCacheDeleteBestEffort(
            storageRoot,
            projectFingerprint,
            cacheMutationVersion);
    }

    private async Task PersistCacheWriteBestEffort (
        string storageRoot,
        string projectFingerprint,
        UnityUcliPluginMarkerCache cache,
        long cacheMutationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);
        ArgumentNullException.ThrowIfNull(cache);

        await cacheMutationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (cacheMutationVersion != Interlocked.Read(ref this.cacheMutationVersion))
            {
                return;
            }

            _ = await pluginMarkerCacheStore.Write(
                    storageRoot,
                    projectFingerprint,
                    cache,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            cacheMutationGate.Release();
        }
    }

    private async Task PersistCacheDeleteBestEffort (
        string storageRoot,
        string projectFingerprint,
        long cacheMutationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);

        await cacheMutationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (cacheMutationVersion != Interlocked.Read(ref this.cacheMutationVersion))
            {
                return;
            }

            _ = pluginMarkerCacheStore.DeleteIfExists(storageRoot, projectFingerprint);
        }
        finally
        {
            cacheMutationGate.Release();
        }
    }
}
